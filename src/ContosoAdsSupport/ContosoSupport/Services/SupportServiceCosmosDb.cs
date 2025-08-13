using ContosoSupport.InstrumentationHelpers;
using ContosoSupport.Models;
using MongoDB.Driver;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace ContosoSupport.Services
{
    internal sealed class SupportServiceCosmosDb : ISupportService
    {
        private const string DatabaseName = "SupportCasesDb";
        private const string CollectionName = "SupportCases";
        private readonly IMongoCollection<SupportCase> supportCases;
        private readonly MongoClient client;
        private readonly AssignmentValidator assignmentValidator;

        public SupportServiceCosmosDb(IConfiguration config, ISupportPersonService? supportPersonService = null)
        {
            client = new MongoClient(config.GetConnectionString(DatabaseName));
            supportCases = client.GetDatabase(DatabaseName).GetCollection<SupportCase>(CollectionName);
            assignmentValidator = new AssignmentValidator(supportPersonService);
        }

        public async Task<long> GetDocumentCountAsync()
        {
            return await supportCases.CountDocumentsAsync(SupportCase => true).ConfigureAwait(false);
        }

        //pageNumber starts from 1, assumes Pages of 10 items
        public async Task<IEnumerable<SupportCase>> GetAsync(int? pageNumber = 1)
        {
            using Activity? activity = createActivity(nameof(GetAsync), "get");

            int pageNum = pageNumber.HasValue
                ? (--pageNumber < 0 ? 0 : pageNumber).Value
                : 1;

            try
            {
                var result = await (supportCases.Find(SupportCase => true).Skip(pageNum * 10).Limit(10))
                    .ToListAsync().ConfigureAwait(false);

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

        // New method implementing assignment filtering per FR-004
        public async Task<IEnumerable<SupportCase>> GetAsync(
            string? assignedTo = null,
            bool? unassigned = null,
            int limit = 50,
            int offset = 0)
        {
            using Activity? activity = createActivity(nameof(GetAsync), "get with assignment filters");

            try
            {
                var filterBuilder = Builders<SupportCase>.Filter;
                var filters = new List<FilterDefinition<SupportCase>>();

                // Filter by assigned support person (FR-004)
                if (!string.IsNullOrEmpty(assignedTo))
                {
                    filters.Add(filterBuilder.Eq(x => x.AssignedSupportPerson, assignedTo));
                }

                // Filter by assignment status (FR-004)
                if (unassigned.HasValue)
                {
                    if (unassigned.Value)
                    {
                        // Find cases without assignment (null or empty)
                        filters.Add(filterBuilder.Or(
                            filterBuilder.Eq(x => x.AssignedSupportPerson, null),
                            filterBuilder.Eq(x => x.AssignedSupportPerson, "")));
                    }
                    else
                    {
                        // Find cases with assignment (not null and not empty)
                        filters.Add(filterBuilder.And(
                            filterBuilder.Ne(x => x.AssignedSupportPerson, null),
                            filterBuilder.Ne(x => x.AssignedSupportPerson, "")));
                    }
                }

                var combinedFilter = filters.Any() 
                    ? filterBuilder.And(filters) 
                    : filterBuilder.Empty;

                // Ensure limit doesn't exceed max per NFR-002
                limit = Math.Min(limit, 100);

                var result = await supportCases
                    .Find(combinedFilter)
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

        public async Task<SupportCase?> GetAsync(string id)
        {
            using Activity? activity = createActivity(nameof(GetAsync), "get", id);

            try
            {
                var result = await (await supportCases.FindAsync(SupportCase => SupportCase.Id == id).ConfigureAwait(false))
                    .FirstOrDefaultAsync().ConfigureAwait(false);
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

        public async Task CreateAsync(SupportCase supportCase)
        {
            using Activity? activity = createActivity(nameof(CreateAsync), "insert");

            try
            {
                // Validate assignment before creating per BR-001 through BR-009
                assignmentValidator.ValidateAssignment(supportCase.Id, supportCase.AssignedSupportPerson);
                assignmentValidator.ValidateReasoning(supportCase.SupportPersonAssignmentReasoning);

                await supportCases.InsertOneAsync(supportCase).ConfigureAwait(false);
                activity?.SetTag("entityId", supportCase.Id);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task UpdateAsync(string id, SupportCase supportCase)
        {
            using Activity? activity = createActivity(nameof(UpdateAsync), "update", id);

            try
            {
                // Validate assignment before updating per FR-006
                assignmentValidator.ValidateAssignment(id, supportCase.AssignedSupportPerson);
                assignmentValidator.ValidateReasoning(supportCase.SupportPersonAssignmentReasoning);

                var result = await supportCases.ReplaceOneAsync(SC => SC.Id == id, supportCase).ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public Task RemoveAsync(SupportCase supportCase)
        {
            if (string.IsNullOrWhiteSpace(supportCase?.Id))
                throw new ArgumentNullException(nameof(supportCase));

            return RemoveAsync(supportCase.Id);
        }

        public async Task RemoveAsync(string id)
        {
            using Activity? activity = createActivity(nameof(RemoveAsync), "delete", id);

            try
            {
                await supportCases.DeleteOneAsync(SC => SC.Id == id).ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
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
}
