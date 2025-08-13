using ContosoSupport.InstrumentationHelpers;
using ContosoSupport.Models;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace ContosoSupport.Services
{
    internal sealed class SupportServiceInMemory : ISupportService
    {
        private readonly List<SupportCase> supportCases = [];
        private readonly object sync = new();
        private long nextId;

        public Task<long> GetDocumentCountAsync()
        {
            lock (sync)
            {
                return Task.FromResult((long)supportCases.Count);
            }
        }

        //pageNumber starts from 1, assumes Pages of 10 items
        public Task<IEnumerable<SupportCase>> GetAsync(int? pageNumber = 1)
        {
            int pageNum = pageNumber.HasValue
                ? (--pageNumber < 0 ? 0 : pageNumber).Value
                : 1;

            lock (sync)
            {
                return Task.FromResult(supportCases.Skip(pageNum * 10).Take(10));
            }

        }

        public Task<SupportCase?> GetAsync(string id)
        {
            //Create your Activity
            using Activity? activity = TelemetryHelper.ActivitySource.StartActivity(nameof(GetAsync), ActivityKind.Client);

            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("db.system", "in-memory");
                activity.SetTag("db.name", "SupportService");
                activity.SetTag("db.statement", "get");
                activity.SetTag("entityId", id);
            }

            try
            {
                SupportCase? result;
                lock (sync)
                {
                    result = supportCases.FirstOrDefault(s => s.Id == id);
                }
                if (activity != null)
                {
                    if (activity.IsAllDataRequested)
                        activity.SetTag("data_found", result != null);
                    //Capture the status code response
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                if (activity != null)
                {
                    if (activity.IsAllDataRequested)
                        activity.RecordException(ex);
                    //Capture the status code response
                    activity.SetStatus(ActivityStatusCode.Error);
                }
                throw;
            }
        }

        public Task CreateAsync(SupportCase supportCase)
        {
            if (supportCase is null)
                throw new ArgumentNullException(nameof(supportCase));

            if (string.IsNullOrWhiteSpace(supportCase.Id))
                supportCase.Id = Interlocked.Increment(ref nextId).ToString();

            lock (sync)
            {
                supportCases.Add(supportCase);
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(string id, SupportCase supportCase)
        {
            if (supportCase is null)
                throw new ArgumentNullException(nameof(supportCase));

            lock (sync)
            {
                var currentCase = supportCases.FirstOrDefault(s => s.Id == id);

                if (null == currentCase)
                {
                    CreateAsync(supportCase);
                }
                else
                {
                    currentCase.Description = supportCase.Description;
                    currentCase.IsComplete = supportCase.IsComplete;
                    currentCase.Owner = supportCase.Owner;
                    currentCase.Title = supportCase.Title;
                }
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(SupportCase supportCase)
        {
            if (string.IsNullOrWhiteSpace(supportCase?.Id))
                throw new ArgumentNullException(nameof(supportCase));

            return RemoveAsync(supportCase.Id);
        }

        public Task RemoveAsync(string id)
        {
            lock (sync)
            {
                var currentCase = supportCases.FirstOrDefault(s => s.Id == id);
                if (null != currentCase)
                {
                    supportCases.Remove(currentCase);
                }
            }

            return Task.CompletedTask;
        }
    }
}