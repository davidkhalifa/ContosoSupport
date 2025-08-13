# Chapter 4: Observability (Telemetry)

Welcome back! In our previous chapter, [Chapter 3: Data Persistence Layer](03_data_persistence_layer_.md), we learned how our ContosoSupport application securely stores all its important information in Azure Cosmos DB, acting as its long-term memory. We now know that support cases and support person details are safely stored and retrieved.

But how do we know if our application is actually running well? What if it suddenly becomes slow? What if customers start getting errors they didn't before? If our application is like a car, just knowing it has an engine and a fuel tank isn't enough. We need to know if the engine is running smoothly, how fast it's going, or if a warning light comes on!

This is where **Observability** (or **Telemetry**) comes in.

### Why Do We Need to "Watch" Our Application?

Imagine you've launched your ContosoSupport application, and it's handling thousands of customer requests every day. Everything seems fine, but then one day, a customer calls and says: "It's taking forever to create a new support case!" Or, "My request failed with a strange error!"

How do you find out what's going on without directly asking the application or stopping it to poke around? You can't just be *inside* the application watching every little thing.

Observability is like adding a sophisticated **diagnostic system** to our application. It gives us the tools to constantly **monitor and understand** how the application is performing and behaving, even from a distance. It's crucial for:

*   **Troubleshooting**: When things go wrong, it helps us quickly find the root cause.
*   **Performance**: Identifying if parts of our system are slow (bottlenecks).
*   **Reliability**: Ensuring our application stays up and running reliably for customers.

### The Three Pillars of Observability

Think of our car's diagnostic system. It doesn't just give one type of information. It gives different views:
1.  **Dashboard Gauges (Metrics)**: Shows current speed, fuel level, engine temperature. Quick numbers about the system's state.
2.  **Journey Recorder (Traces)**: A detailed log of where the car went, how long it stopped, which roads it took. A step-by-step story of a single trip.
3.  **Error Log (Logs)**: Records specific events, like "Engine light turned on" or "Tire pressure low." Detailed messages about what happened at a certain time.

In application observability, these "views" are called the **Three Pillars**:

1.  **Logs**: Detailed messages about events that happen in the application.
2.  **Metrics**: Numbers that represent the system's performance or health over time.
3.  **Traces**: The journey of a single request as it moves through different parts of the application.

Our ContosoSupport application uses something called **OpenTelemetry** to collect this information. OpenTelemetry is like a universal language for collecting diagnostic data, so different tools can understand it. Then, a **Geneva Monitoring Agent** acts like a specialized delivery service, sending all this collected data to a central monitoring system where we can see it on dashboards, search through logs, and analyze traces.

Let's explore each pillar in the context of our ContosoSupport application.

### 1. Logs: The Application's Diary

**What they are**: Logs are simply text messages written by the application to describe what it's doing, what's happening, or if there's an error. They are like a ship's captain writing notes in a logbook: "9:00 AM - Departed port," "10:30 AM - Encountered rough seas," "11:00 AM - Engine trouble!"

**What they tell us**: Logs help us answer "What happened?" They are great for debugging specific issues or understanding the sequence of events leading to a problem.

**How ContosoSupport uses them**: Our application uses a standard .NET feature called `ILogger` to write log messages. For example, if there's a problem creating a support case, it logs a warning.

Here's a simplified look at how logs are used in our `SupportCasesController`:

```csharp
// File: src/ContosoAdsSupport/ContosoSupport/Controllers/SupportCasesController.cs

// ... (other code) ...

namespace ContosoSupport.Controllers
{
    public class SupportCasesController(...) : ControllerBase
    {
        private readonly ILogger logger; // Our "logbook writer"

        // ... (constructor and other methods) ...

        [HttpPost] 
        public async Task<IActionResult> PostSupportCaseAsync(...)
        {
            try
            {
                // ... (code to create support case) ...
            }
            catch (Exception ex) // If something goes wrong
            {
                // Here we use a helper to log the failure
                SupportCaseLoggingHelper.LogCreateFailure(logger, subscriptionId, resourceGroup, resourceId, supportCase?.Id, ex);
                throw;
            }
            return Accepted();
        }
    }
}
```

*   `SupportCaseLoggingHelper.LogCreateFailure`: This is a dedicated method that creates a structured log message. It's like having pre-printed forms for specific events in the logbook.

Our `Program.cs` file configures how these logs are collected and sent:

```csharp
// File: src/ContosoAdsSupport/ContosoSupport/Program.cs

var builder = WebApplication.CreateBuilder(args);

// ... (other setup) ...

builder.Logging
    .ClearProviders()
    .AddOpenTelemetry(options => // Tell OpenTelemetry to collect logs
    {
        options.ParseStateValues = true; 
        options.IncludeFormattedMessage = false; 
        options
            .SetResourceBuilder(resourceBuilder)
            .AddConsoleExporter() // Send logs to the console (for local development)
            .AddGenevaLogExporter(options => // Send logs to the Geneva Monitoring Agent
            {
                options.ConnectionString = "EtwSession=OpenTelemetry";
                options.TableNameMappings = new Dictionary<string, string>
                {
                    [typeof(ContosoSupport.Controllers.SupportCasesController).FullName!] = "DataPlaneControllerErrors"
                };
            });
    });

// ... (rest of the program) ...
```

*   `.AddOpenTelemetry()`: This line tells our application to use OpenTelemetry for logging.
*   `.AddConsoleExporter()`: This is useful during development; it just prints the logs to the console window where the application is running.
*   `.AddGenevaLogExporter()`: This is the important part for production. It sends all collected logs to the Geneva Monitoring Agent, which then forwards them to a central monitoring system for analysis. It even allows us to map logs from specific controllers to different "tables" in the monitoring system, organizing our data.

### 2. Metrics: The Application's Dashboard

**What they are**: Metrics are numerical measurements about the application's health and performance that change over time. They are like the gauges on a car dashboard: speedometer (how fast we are going), fuel gauge (how much fuel is left), temperature gauge (engine temperature).

**What they tell us**: Metrics help us answer "How much?" or "How fast?" They are great for spotting trends, setting up alerts (e.g., "alert me if the average response time goes above 1 second!"), and understanding the overall health of the system at a glance.

**How ContosoSupport uses them**: Our application measures how long it takes to respond to requests (called "latency"). This is a crucial metric for any API. It uses `TelemetryHelper.Meter` to create these metrics.

Let's look at the `PerfMetricsMiddleware` (Performance Metrics Middleware) which is like a "watchdog" that measures how long each request takes:

```csharp
// File: src/ContosoAdsSupport/ContosoSupport/Middleware/PerfMetricsMiddleware.cs

// ... (other using statements) ...

namespace ContosoSupport.Middleware
{
    public class PerfMetricsMiddleware
    {
        // This creates a "gauge" for measuring response latency in milliseconds
        private static readonly Histogram<long> LatencyHistogram = TelemetryHelper.Meter.CreateHistogram<long>("ResponseLatencyMs");

        private readonly RequestDelegate next; // The next step in processing the request

        // ... (constructor) ...

        public async Task Invoke(HttpContext context)
        {
            var timer = Stopwatch.StartNew(); // Start stopwatch
            try
            {
                await next.Invoke(context).ConfigureAwait(false); // Let the application process the request
            }
            finally // This block always runs, even if there's an error
            {
                timer.Stop(); // Stop stopwatch
                // Record the measured latency, along with helpful details like HTTP method and status code
                RecordLatencyMeasure(
                    latencyMeasurement: timer.ElapsedMilliseconds,
                    // ... (other details like host, subscriptionId, method, statusCode) ...
                    );
            }
        }

        private void RecordLatencyMeasure(...)
        {
            // Create a list of labels (tags) to describe this measurement, like "HttpMethod: GET", "StatusCode: 200"
            var latencyDimensions = new TagList()
            {
                { "HttpMethod", method },
                { "HttpStatusCode", statusCode },
                { "Operation", operationId },
                // ... (other tags) ...
            };

            // Record the latency measurement with its labels
            LatencyHistogram.Record(latencyMeasurement, in latencyDimensions);
        }
    }
}
```

*   `TelemetryHelper.Meter.CreateHistogram<long>("ResponseLatencyMs")`: This creates a special type of metric called a "histogram" which is good for measuring things like latency and also shows how often certain values occur (e.g., "most requests finish in 100-200ms").
*   `LatencyHistogram.Record(...)`: This is where the actual measurement is sent to OpenTelemetry. It includes not just the time, but also "tags" (like `HttpMethod`, `StatusCode`) that help filter and analyze the data later.

In `Program.cs`, we tell OpenTelemetry to collect metrics:

```csharp
// File: src/ContosoAdsSupport/ContosoSupport/Program.cs

// ... (other setup) ...

builder.Services.AddOpenTelemetry().WithMetrics(options => // Tell OpenTelemetry to collect metrics
{
    options
        .SetResourceBuilder(resourceBuilder)
        .AddMeter(TelemetryHelper.Meter.Name) // Collect metrics from our custom "Meter"
        .AddAspNetCoreInstrumentation() // Collect metrics automatically from ASP.NET Core (e.g., request counts)
        .AddConsoleExporter() // Send metrics to the console
        .AddGenevaMetricExporter(options => options.ConnectionString = "Account=ABCLab;Namespace=ContosoSupport"); // Send metrics to Geneva
});

// ... (rest of the program) ...
```

*   `.WithMetrics()`: This enables OpenTelemetry for metrics.
*   `.AddMeter(TelemetryHelper.Meter.Name)`: This tells OpenTelemetry to collect all metrics that we've defined using our `TelemetryHelper.Meter`.
*   `.AddAspNetCoreInstrumentation()`: This automatically collects standard metrics from our web application, like how many requests come in, or how many requests result in errors.
*   `.AddGenevaMetricExporter()`: This sends the collected metrics to the Geneva Monitoring Agent for centralized collection and visualization.

### 3. Traces: The Application's Journey Map

**What they are**: Traces (or distributed traces) show the end-to-end journey of a single request as it flows through different parts of our application, and even to other services or databases. They are like a detailed GPS log or a detective's case file, showing every step taken, how long each step took, and where it went next.

**What they tell us**: Traces help us answer "Why is this specific request slow?" or "What exact path did this user's action take?" They are fantastic for understanding complex interactions and finding bottlenecks across multiple services.

**How ContosoSupport uses them**: Our application uses `TelemetryHelper.ActivitySource` to create "activities," which represent a step in a trace. Each activity has a name, a start time, an end time, and can have "tags" (like extra details) and "events" (like mini-logs within the trace).

Let's revisit our `SupportCasesController` and `SupportPersonsController` from [Chapter 1: ContosoSupport API Application](01_contososupport_api_application_.md) and [Chapter 2: Support Case & Person Management](02_support_case___person_management_.md):

```csharp
// File: src/ContosoAdsSupport/ContosoSupport/Controllers/SupportCasesController.cs

// ... (other using statements) ...

using System.Diagnostics; // This namespace contains Activity

namespace ContosoSupport.Controllers
{
    public class SupportCasesController(...) : ControllerBase
    {
        // ... (constructor and other fields) ...

        [HttpPost]
        public async Task<IActionResult> PostSupportCaseAsync(string subscriptionId, string resourceGroup, string resourceId, [FromBody] SupportCase supportCase)
        {
            // Start a new "activity" (a step in our trace journey)
            using Activity? activity = createActivity(nameof(PostSupportCaseAsync), subscriptionId, resourceGroup, resourceId, SupportCaseAccessType.Create, supportCase?.Id);

            try
            {
                await supportService.CreateAsync(supportCase!).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // If an error occurs, record it in the current activity
                activity?.RecordException(ex); 
                // Set the activity's status to Error
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            // If everything is okay, set the activity's status to OK
            activity?.SetStatus(ActivityStatusCode.Ok);

            return Accepted();
        }

        // This helper method creates and sets up the activity
        private Activity? createActivity(
            string name,
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            SupportCaseAccessType accessType,
            string? id = null)
        {
            // Start an activity using our global ActivitySource
            Activity? activity = TelemetryHelper.ActivitySource.StartActivity(name, ActivityKind.Internal);

            if (activity?.IsAllDataRequested == true)
            {
                // Add useful tags (labels) to the activity
                activity.SetTag("resourceType", "Contoso.Support/ticketingSystem");
                activity.SetTag("resourceId", $"subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Contoso.Support/ticketingSystem/{resourceId}");
                activity.SetTag("entityType", nameof(SupportCase));
                if (!string.IsNullOrEmpty(id))
                    activity.SetTag("entityId", id);
                activity.SetTag("accessType", (int)accessType);
            }

            return activity;
        }
    }
}
```

*   `using Activity? activity = createActivity(...)`: This line starts a new `Activity`. The `using` statement ensures that the activity is automatically stopped (and its duration recorded) when the `PostSupportCaseAsync` method finishes.
*   `TelemetryHelper.ActivitySource.StartActivity(...)`: This is where the activity is actually created. `TelemetryHelper.ActivitySource` is a central place for defining all our traces.
*   `activity.SetTag(...)`: This adds key-value pairs to the trace, providing context like the type of resource being accessed (`SupportCase`), or its ID.
*   `activity?.RecordException(ex)` and `activity?.SetStatus(...)`: These are crucial for marking an activity as having an error and including the exception details, making troubleshooting much easier.

In `Program.cs`, we enable tracing:

```csharp
// File: src/ContosoAdsSupport/ContosoSupport/Program.cs

// ... (other setup) ...

builder.Services.AddOpenTelemetry().WithTracing(options => // Tell OpenTelemetry to collect traces
{
    options
        .SetResourceBuilder(resourceBuilder)
        .SetErrorStatusOnException() // Automatically mark trace as error if an unhandled exception occurs
        .AddAspNetCoreInstrumentation(options =>
        {
            // Filter out health check requests from traces
            options.Filter = (httpContext) => !httpContext.Request.Path.Equals("/");
        })
        .AddSource(TelemetryHelper.ActivitySource.Name, "Azure.*") // Collect traces from our custom source and Azure SDK
        .AddConsoleExporter() // Send traces to the console
        .AddGenevaTraceExporter(options => // Send traces to Geneva
        {
            options.ConnectionString = "EtwSession=OpenTelemetry";
        });

});

// ... (rest of the program) ...
```

*   `.WithTracing()`: This enables OpenTelemetry for tracing.
*   `.SetErrorStatusOnException()`: This is a convenient setting that automatically marks any trace as "Error" if an unhandled exception occurs within that operation.
*   `.AddAspNetCoreInstrumentation()`: This automatically creates traces for incoming web requests handled by ASP.NET Core.
*   `.AddSource(TelemetryHelper.ActivitySource.Name, "Azure.*")`: This tells OpenTelemetry to collect traces that originate from our custom `TelemetryHelper.ActivitySource` (where we manually create activities) and also from any Azure SDK operations (like talking to Cosmos DB), giving us a full picture.
*   `.AddGenevaTraceExporter()`: This sends the collected traces to the Geneva Monitoring Agent.

### The Observability Flow: From Request to Insight

Let's put all the pieces together and see how logs, metrics, and traces are generated and collected as a request flows through our system:

```mermaid
sequenceDiagram
    participant CustomerPortal as Customer Portal
    participant ContosoSupportAPI as ContosoSupport API
    participant SupportService as Support Case Service
    participant OpenTelemetrySDK as OpenTelemetry SDK
    participant GenevaAgent as Geneva Monitoring Agent
    participant MonitoringSystem as Monitoring System

    CustomerPortal->>ContosoSupportAPI: 1. "Create Support Case" Request
    ContosoSupportAPI->>OpenTelemetrySDK: 2. Emit "API Request" Trace (Activity starts)
    ContosoSupportAPI->>SupportService: 3. Call CreateAsync
    SupportService->>OpenTelemetrySDK: 4. Emit "Service Logic" Trace (Activity starts)
    SupportService->>OpenTelemetrySDK: 5. Emit "Validation" Trace (Activity starts)
    SupportService->>OpenTelemetrySDK: 6. Emit "Validation Failed" Log (if error)
    SupportService->>OpenTelemetrySDK: 7. Emit "Cosmos DB Call" Trace (Activity starts)
    OpenTelemetrySDK->>GenevaAgent: 8. Collect & Forward Logs, Metrics, Traces
    GenevaAgent->>MonitoringSystem: 9. Deliver All Data
    MonitoringSystem: 10. Visualize and Alert
    MonitoringSystem: Display Dashboard (Metrics: Latency, Request Rate)
    MonitoringSystem: Show Trace Waterfall (Trace: Steps of API call)
    MonitoringSystem: Allow Log Search (Logs: Error details)
```

**Detailed Steps:**

1.  **Request Arrives**: The `Customer Portal` sends a request to the `ContosoSupport API` (e.g., to create a support case).
2.  **API Starts Tracing**: The `ContosoSupport API` (specifically, its ASP.NET Core instrumentation and our custom `ActivitySource`) immediately starts a new "Activity" to track this request's journey.
3.  **API Calls Service**: The API then calls the `Support Case Service` to perform the business logic.
4.  **Service Continues Tracing**: The `Support Case Service` (or its underlying components, like the validators or the database driver) also start their own nested "Activities" which are part of the main request's trace.
5.  **Logs and Metrics Emerge**: As various operations happen (e.g., validation, database calls), the application generates `Logs` (e.g., "Validation failed," "Case created") and `Metrics` (e.g., the `PerfMetricsMiddleware` records the total time taken for the API request).
6.  **OpenTelemetry Collects**: The `OpenTelemetry SDK` running within our application continuously collects all these logs, metrics, and trace activities as they are generated. It connects the "nested" activities to form a complete trace.
7.  **Geneva Agent Sends**: The `Geneva Monitoring Agent` periodically retrieves this collected data from the `OpenTelemetry SDK` and reliably sends it to the central `Monitoring System`.
8.  **Monitoring System Visualizes**: The `Monitoring System` (e.g., a dashboard or a log analytics platform) receives this data and allows us to:
    *   View **Metrics** on dashboards to see overall trends (e.g., average response time over the last hour).
    *   Examine **Traces** as a "waterfall" diagram, showing the exact steps a single request took and how long each step lasted, helping pinpoint where delays occur.
    *   Search through **Logs** to find specific error messages or events.

### The Core Components for Observability

Here's a quick summary of the key files and their roles in implementing observability:

| Component                 | Role / Responsibility                                      | Location (Simplified)                     |
| :------------------------ | :--------------------------------------------------------- | :---------------------------------------- |
| **`TelemetryHelper.cs`**  | Defines global `ActivitySource` and `Meter` for custom telemetry. | `InstrumentationHelpers/TelemetryHelper.cs` |
| **`Program.cs`**          | Configures OpenTelemetry to collect and export logs, metrics, and traces. | `Program.cs`                               |
| **`PerfMetricsMiddleware.cs`** | Measures API request latency and emits it as a metric.     | `Middleware/PerfMetricsMiddleware.cs`     |
| **`SupportCasesController.cs`** | Starts/stops traces for support case API operations, records exceptions. | `Controllers/SupportCasesController.cs`   |
| **`SupportPersonsController.cs`** | Starts/stops traces for support person API operations.     | `Controllers/SupportPersonsController.cs` |
| **`SupportCaseLoggingHelper.cs`** | Defines structured log messages for support case operations. | `Models/SupportCaseLoggingHelper.cs`      |

By combining these three pillars, our ContosoSupport application gains powerful diagnostic capabilities, allowing us to proactively detect, diagnose, and resolve issues, ensuring a smooth experience for our users.

### Conclusion

In this chapter, we explored the crucial concept of **Observability (Telemetry)**, learning how it equips our ContosoSupport application with a sophisticated diagnostic system. We specifically looked at:

*   The importance of monitoring our application for troubleshooting, performance, and reliability.
*   The **Three Pillars of Observability**: **Logs** (what happened?), **Metrics** (how much/fast?), and **Traces** (the journey of a single request).
*   How **OpenTelemetry** helps collect this data and how the **Geneva Monitoring Agent** sends it to a central system for analysis.
*   Specific code examples showing how logs, metrics, and traces are implemented and used within the `ContosoSupport` API Application.

Now that our application can securely store data and tell us how it's performing, the next step is to understand how we actually get this application up and running in a real-world, highly available environment. That's what we'll cover in the next chapter!

[Next Chapter: Service Fabric Application Deployment](05_service_fabric_application_deployment_.md)

---

<sub><sup>**References**: [[1]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/ContosoSupport/Controllers/SupportCasesController.cs), [[2]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/ContosoSupport/Controllers/SupportPersonsController.cs), [[3]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/ContosoSupport/InstrumentationHelpers/TelemetryHelper.cs), [[4]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/ContosoSupport/Middleware/PerfMetricsMiddleware.cs), [[5]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/ContosoSupport/Models/SupportCaseLoggingHelper.cs), [[6]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/ContosoSupport/Program.cs), [[7]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/SFClusterArmTemplate/Parameters/ServiceFabricCluster.parameters.json), [[8]](https://github.com/davidkhalifa/ContosoSupport/blob/c01f43d9f8c812eb393ce94a0c83eca726799fd7/src/ContosoAdsSupport/SFClusterArmTemplate/Templates/ServiceFabricCluster.template.json)</sup></sub>