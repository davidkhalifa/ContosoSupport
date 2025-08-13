// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.


using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This specific exception is what will be thrown when code is running outside of Azure", Scope = "member", Target = "~M:ContosoSupport.Services.VmMetadataService.GetComputeLocationAsync(System.String)~System.Threading.Tasks.Task{System.String}")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Internal log messages, not user facing, no localization required", Scope = "type", Target = "~T:ContosoSupport.Middleware.PerfMetricsMiddleware")]
[assembly: SuppressMessage("Usage", "CA1801:Review unused parameters", Justification = "Dummy parameters to simulate values that might be passed to an RP", Scope = "type", Target = "~T:ContosoSupport.Controllers.SupportCasesController")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Dummy parameters to simulate values that might be passed to an RP", Scope = "type", Target = "~T:ContosoSupport.Controllers.SupportCasesController")]
[assembly: SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "The source of the null value is the configuration. Leaving to make it clear to an attendee debugging the error where the actual fix might be")]