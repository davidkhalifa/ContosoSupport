using ContosoSupport.InstrumentationHelpers;
using ContosoSupport.Models;
using MongoDB.Driver;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace ContosoSupport.Services
{
    internal sealed class SupportPersonServiceCosmosDb : ISupportPersonService
    {
        private const string DatabaseName = "SupportCasesDb";
        private const string CollectionName = "SupportPersons";
        private readonly IMongoCollection<SupportPerson> supportPersons;
        private readonly IMongoCollection<SupportCase> supportCases;
        private readonly MongoClient client;
        private readonly SupportPersonValidator validator;

        public SupportPersonServiceCosmosDb(IConfiguration config)
        {
            client = new MongoClient(config.GetConnectionString(DatabaseName));
            var database = client.GetDatabase(DatabaseName);
            supportPersons = database.GetCollection<SupportPerson>(CollectionName);
            supportCases = database.GetCollection<SupportCase>("SupportCases");
            validator = new SupportPersonValidator();
        }

        public async Task<long> GetDocumentCountAsync()
        {
            return await supportPersons.CountDocumentsAsync(sp => true).ConfigureAwait(false);
        }

        public async Task<IEnumerable<SupportPerson>> GetAsync(
            string? specialization = null,
            string? seniority = null,
            bool? available = null,
            int limit = 50,
            int offset = 0,
            string sortBy = "name",
            string sortOrder = "asc")
        {
            using Activity? activity = createActivity(nameof(GetAsync), "get with filters");

            try
            {
                var filterBuilder = Builders<SupportPerson>.Filter;
                var filters = new List<FilterDefinition<SupportPerson>>();

                // Only return active support persons
                filters.Add(filterBuilder.Eq(x => x.IsActive, true));

                // Filter by specialization
                if (!string.IsNullOrEmpty(specialization))
                {
                    filters.Add(filterBuilder.AnyEq(x => x.Specializations, specialization));
                }

                // Filter by seniority
                if (!string.IsNullOrEmpty(seniority))
                {
                    filters.Add(filterBuilder.Eq(x => x.Seniority, seniority));
                }

                // Filter by availability (assuming max capacity of 10 for demo)
                if (available.HasValue && available.Value)
                {
                    filters.Add(filterBuilder.Lt(x => x.CurrentWorkload, 10));
                }

                var combinedFilter = filterBuilder.And(filters);

                // Ensure limit doesn't exceed max
                limit = Math.Min(limit, 100);

                // Build sort
                var sortDefinition = BuildSortDefinition(sortBy, sortOrder);

                var result = await supportPersons
                    .Find(combinedFilter)
                    .Sort(sortDefinition)
                    .Skip(offset)
                    .Limit(limit)
                    .ToListAsync()
                    .ConfigureAwait(false);

                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task<SupportPerson?> GetAsync(string alias)
        {
            using Activity? activity = createActivity(nameof(GetAsync), "get", alias);

            try
            {
                var result = await supportPersons
                    .Find(sp => sp.Alias == alias && sp.IsActive)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task CreateAsync(SupportPerson supportPerson)
        {
            using Activity? activity = createActivity(nameof(CreateAsync), "insert");

            try
            {
                // Validate the support person
                validator.ValidateSupportPerson(supportPerson);

                // Check for duplicate alias (BR-002)
                if (await ExistsAsync(supportPerson.Alias).ConfigureAwait(false))
                {
                    throw new ConflictException("ALIAS_ALREADY_EXISTS", 
                        $"Support person with alias '{supportPerson.Alias}' already exists");
                }

                // Check for duplicate email (BR-001)
                var existingByEmail = await supportPersons
                    .Find(sp => sp.Email == supportPerson.Email && sp.IsActive)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (existingByEmail != null)
                {
                    throw new SupportPersonValidationException(new List<ValidationError>
                    {
                        new ValidationError("email", "Email address is already in use")
                    });
                }

                // Set default values for new support person
                supportPerson.CurrentWorkload = 0;
                supportPerson.AverageResolutionTime = null;
                supportPerson.CustomerSatisfactionRating = null;
                supportPerson.IsActive = true;

                await supportPersons.InsertOneAsync(supportPerson).ConfigureAwait(false);
                activity?.SetTag("entityId", supportPerson.Alias);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task UpdateAsync(string alias, SupportPerson supportPerson)
        {
            using Activity? activity = createActivity(nameof(UpdateAsync), "update", alias);

            try
            {
                // Validate the support person (excluding alias since it's not changeable)
                validator.ValidateSupportPerson(supportPerson, isUpdate: true);

                // Ensure alias remains the same
                supportPerson.Alias = alias;

                // Check for duplicate email (excluding current person)
                var existingByEmail = await supportPersons
                    .Find(sp => sp.Email == supportPerson.Email && sp.Alias != alias && sp.IsActive)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (existingByEmail != null)
                {
                    throw new SupportPersonValidationException(new List<ValidationError>
                    {
                        new ValidationError("email", "Email address is already in use")
                    });
                }

                var result = await supportPersons
                    .ReplaceOneAsync(sp => sp.Alias == alias && sp.IsActive, supportPerson)
                    .ConfigureAwait(false);

                if (result.MatchedCount == 0)
                {
                    throw new NotFoundException("SUPPORT_PERSON_NOT_FOUND", 
                        $"Support person with alias '{alias}' was not found");
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task RemoveAsync(string alias)
        {
            using Activity? activity = createActivity(nameof(RemoveAsync), "delete", alias);

            try
            {
                // BR-006: Check for active assignments
                if (await HasActiveAssignmentsAsync(alias).ConfigureAwait(false))
                {
                    var activeTickets = await supportCases
                        .Find(sc => sc.AssignedSupportPerson == alias)
                        .Project(sc => sc.Id)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    throw new ConflictException("CANNOT_DELETE_ACTIVE_ASSIGNMENTS",
                        "Support person cannot be deleted while having active ticket assignments",
                        new { activeTickets = activeTickets.Count, ticketIds = activeTickets.Take(3).ToArray() });
                }

                // Soft delete by setting IsActive to false
                var update = Builders<SupportPerson>.Update.Set(sp => sp.IsActive, false);
                var result = await supportPersons
                    .UpdateOneAsync(sp => sp.Alias == alias && sp.IsActive, update)
                    .ConfigureAwait(false);

                if (result.MatchedCount == 0)
                {
                    throw new NotFoundException("SUPPORT_PERSON_NOT_FOUND", 
                        $"Support person with alias '{alias}' was not found");
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string alias)
        {
            return await supportPersons
                .Find(sp => sp.Alias == alias && sp.IsActive)
                .AnyAsync()
                .ConfigureAwait(false);
        }

        public async Task<bool> HasActiveAssignmentsAsync(string alias)
        {
            return await supportCases
                .Find(sc => sc.AssignedSupportPerson == alias)
                .AnyAsync()
                .ConfigureAwait(false);
        }

        private SortDefinition<SupportPerson> BuildSortDefinition(string sortBy, string sortOrder)
        {
            var sortBuilder = Builders<SupportPerson>.Sort;
            var ascending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

            return sortBy.ToLower() switch
            {
                "name" => ascending ? sortBuilder.Ascending(x => x.Name) : sortBuilder.Descending(x => x.Name),
                "seniority" => ascending ? sortBuilder.Ascending(x => x.Seniority) : sortBuilder.Descending(x => x.Seniority),
                "workload" => ascending ? sortBuilder.Ascending(x => x.CurrentWorkload) : sortBuilder.Descending(x => x.CurrentWorkload),
                "rating" => ascending ? sortBuilder.Ascending(x => x.CustomerSatisfactionRating) : sortBuilder.Descending(x => x.CustomerSatisfactionRating),
                _ => sortBuilder.Ascending(x => x.Name)
            };
        }

        private Activity? createActivity(string name, string dbStatement, string? id = null)
        {
            using Activity? activity = TelemetryHelper.ActivitySource.StartActivity(name, ActivityKind.Client);

            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("db.system", "CosmosDb");
                activity.SetTag("db.name", "SupportService");
                activity.SetTag("db.statement", dbStatement);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    activity.SetTag("entityId", id);
                }
            }

            return activity;
        }
    }

    public class ConflictException : Exception
    {
        public string Code { get; }
        public object? Details { get; }

        public ConflictException(string code, string message, object? details = null) : base(message)
        {
            Code = code;
            Details = details;
        }
    }

    public class NotFoundException : Exception
    {
        public string Code { get; }

        public NotFoundException(string code, string message) : base(message)
        {
            Code = code;
        }
    }
}