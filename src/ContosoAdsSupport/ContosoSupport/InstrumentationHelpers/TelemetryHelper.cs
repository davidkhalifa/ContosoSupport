using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ContosoSupport.InstrumentationHelpers
{
    public static class TelemetryHelper
    {
        private static readonly string s_Version = typeof(TelemetryHelper).Assembly.GetName().Version!.ToString();

        // Meter from .NET is used to emit custom metrics.
        public static Meter Meter { get; } = new Meter("ContosoSupport.Metrics", s_Version);

        // ActivitySource from .NET is used to emit custom traces.
        public static ActivitySource ActivitySource { get; } = new("ContosoSupport.Traces", s_Version);
    }
}
