namespace ContosoSupport.Services
{
    public interface IVmMetadataService
    {
        Task<string> GetComputeLocationAsync(string defaultRegion = "localhost");
    }
}
