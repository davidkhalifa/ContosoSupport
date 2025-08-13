using ContosoSupport.Models;

namespace ContosoSupport.Services
{
    public interface ISupportPersonService
    {
        Task CreateAsync(SupportPerson supportPerson);
        Task<IEnumerable<SupportPerson>> GetAsync(
            string? specialization = null,
            string? seniority = null,
            bool? available = null,
            int limit = 50,
            int offset = 0,
            string sortBy = "name",
            string sortOrder = "asc");
        Task<SupportPerson?> GetAsync(string alias);
        Task<long> GetDocumentCountAsync();
        Task RemoveAsync(string alias);
        Task UpdateAsync(string alias, SupportPerson supportPerson);
        Task<bool> ExistsAsync(string alias);
        Task<bool> HasActiveAssignmentsAsync(string alias);
    }
}