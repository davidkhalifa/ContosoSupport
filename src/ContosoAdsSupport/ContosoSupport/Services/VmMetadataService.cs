using System.Text.Json;

namespace ContosoSupport.Services
{
    internal sealed class VmMetadataService : IVmMetadataService, IDisposable
    {
        private const string apiVersion = "2019-06-04";
        private const string metadataHeaderName = "Metadata";
        private const string metadataHeaderValue = "true";
        private const string computeObjectName = "compute";
        private const string locationPropertyName = "location";
        private readonly HttpClient client = new();

        public async Task<string> GetComputeLocationAsync(string defaultRegion = "localhost")
        {
            Uri endpointUri = new($"http://169.254.169.254/metadata/instance?api-version={apiVersion}");

            client.DefaultRequestHeaders.Add(metadataHeaderName, metadataHeaderValue);

            HttpResponseMessage response;

            try
            {
                response = await client.GetAsync(endpointUri).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                // We're not running in Azure right now
                return defaultRegion;
            }

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            return ExtractLocation(await JsonDocument.ParseAsync(responseStream).ConfigureAwait(false)) ?? defaultRegion;
        }

        public void Dispose()
        {
            client.Dispose();
        }

        private static string? ExtractLocation(JsonDocument json)
        {
            if (!json.RootElement.TryGetProperty(computeObjectName, out var computeObject)
                || !computeObject.TryGetProperty(locationPropertyName, out var locationProperty))
            {
                return null;
            }

            return locationProperty.GetString();
        }
    }
}
