using Azure.Identity;
using ContosoSupport.InstrumentationHelpers;
using ContosoSupport.Middleware;
using ContosoSupport.Services;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost
    .UseUrls(urls: builder.Configuration.GetValue<string>("Host:Urls")
    ?? throw new ArgumentNullException("Host:Urls"));

try
{
    builder.Configuration
        .AddAzureKeyVault(new Uri($"https://{builder.Configuration["KeyVaultName"]}.vault.azure.net/"), new DefaultAzureCredential());
}
catch (UriFormatException)
{
    // KeyVault setting not specified. You'll need to ensure that you configure this in the appsettings.json before continuing.
    throw new Exception("You must specify the Key Vault setting in the appsettings.json before continuing with the lab activity.");
}
catch (HttpRequestException e) when (e.Message.StartsWith("No such host is known."))
{
    // KeyVault does not exist, maybe there's a typo, maybe the KeyVault was deleted?
    throw new Exception("The Key Vault specified in the appsettings.json does not exist. Please ensure the name of the Key Vault in the appsettings.json matches the name of a Key Vault resource that currently exists in Azure.");
}

/*
 * Note: OpenTelemetry resource is configured here. Resource contains global
 * details about the process. For more details see:
 * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md.
 */
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: "ContosoSupport", serviceVersion: "1.0.0", serviceInstanceId: Environment.MachineName);

builder.Logging
    .ClearProviders()
    .AddOpenTelemetry(options =>
    {
        options.ParseStateValues = true; // <- Capture log state as OTel attributes
        options.IncludeFormattedMessage = false; // 
        options
            .SetResourceBuilder(resourceBuilder)
            .AddConsoleExporter()
            .AddGenevaLogExporter(options =>
            {
                options.ConnectionString = "EtwSession=OpenTelemetry";
                options.TableNameMappings = new Dictionary<string, string>
                {
                    [typeof(ContosoSupport.Controllers.SupportCasesController).FullName!] = "DataPlaneControllerErrors"
                };
            });
    });

const string AllowAll = "_myAllowAllowOrigins";

// Note: We are allowing all origins, headers and methods which is a security risk.
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        AllowAll,
        policyBuilder => policyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .Build());
});

builder.Services.AddMvc(options => options.EnableEndpointRouting = false);

builder.Services
    .AddSingleton<IVmMetadataService, VmMetadataService>()
    .AddSingleton<ISupportService, SupportServiceCosmosDb>()
    .AddSingleton<ISupportPersonService, SupportPersonServiceCosmosDb>()
    .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
    .AddHostedService<SupportServiceDataInitializer>();

// OpenTelemetry support in Azure SDK is currently experimental. See:
// https://devblogs.microsoft.com/azure-sdk/introducing-experimental-opentelemetry-support-in-the-azure-sdk-for-net/
AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

builder.Services.AddOpenTelemetry().WithTracing(options =>
{
    options
        .SetResourceBuilder(resourceBuilder)
        .SetErrorStatusOnException() // Set error status on exceptions
        .AddAspNetCoreInstrumentation(options =>
        {
            // Limit traces from AspNetCore to ignore health probes at "/"
            options.Filter = (httpContext) => !httpContext.Request.Path.Equals("/");
        })
        .AddSource(TelemetryHelper.ActivitySource.Name, "Azure.*")
        .AddConsoleExporter()
        .AddGenevaTraceExporter(options =>
        {
            options.ConnectionString = "EtwSession=OpenTelemetry";
        });

}).WithMetrics(options =>
{
    options
        .SetResourceBuilder(resourceBuilder)
        .AddMeter(TelemetryHelper.Meter.Name) // Collect all metrics from TelemetryHelper
        .AddAspNetCoreInstrumentation() // Collect all metrics from AspNetCore
        .AddConsoleExporter()
        .AddGenevaMetricExporter(options => options.ConnectionString = "Account=ABCLab;Namespace=ContosoSupport");
});

var app = builder.Build();

app.UseMiddleware<PerfMetricsMiddleware>();
app.UseMiddleware<LatencyInjectionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // app.UseHsts(); Note: Not using SSL for lab, but recommended.
}

app.UseCors(AllowAll);
// app.UseHttpsRedirection(); Note: Not using SSL for lab, but recommended.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMvc();

app.Run();