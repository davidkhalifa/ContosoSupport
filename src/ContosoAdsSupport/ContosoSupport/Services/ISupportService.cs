using ContosoSupport.Models;

namespace ContosoSupport.Services
{
    public interface ISupportService
    {
        Task CreateAsync(SupportCase supportCase);
        Task<IEnumerable<SupportCase>> GetAsync(int? pageNumber = 1);
        Task<IEnumerable<SupportCase>> GetAsync(
            string? assignedTo = null,
            bool? unassigned = null,
            string? assignmentMethod = null,
            int limit = 50,
            int offset = 0);
        Task<SupportCase?> GetAsync(string id);
        Task<long> GetDocumentCountAsync();
        Task RemoveAsync(string id);
        Task RemoveAsync(SupportCase supportCase);
        Task UpdateAsync(string id, SupportCase supportCase);
    }
}