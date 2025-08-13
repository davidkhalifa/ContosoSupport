using ContosoSupport.InstrumentationHelpers;
using ContosoSupport.Models;
using ContosoSupport.Services;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace ContosoSupport.Controllers
{
    [Route("{subscriptionId}/{resourceGroup}/{resourceId}/cases")]
    [ApiController]
    public class SupportCasesController(ISupportService supportService, ILogger<SupportCasesController> logger) : ControllerBase
    {
        private const string idTemplate = "{id}";
        private const string fail = "fail";

        private readonly ISupportService supportService = supportService;
        private readonly ILogger logger = logger;

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SupportCase>), 200)]
        public async Task<IActionResult> GetSupportCasesAsync(
            string subscriptionId, 
            string resourceGroup, 
            string resourceId, 
            int? pageNumber = 1,
            string? assignedTo = null,
            bool? unassigned = null,
            string? assignmentMethod = null,
            int limit = 50,
            int offset = 0)
        {
            using Activity? activity = createActivity(nameof(GetSupportCasesAsync), subscriptionId, resourceGroup, resourceId, SupportCaseAccessType.Read);

            IEnumerable<SupportCase>? supportCases;

            try
            {
                // If assignment filtering parameters are provided, use the filtered method
                if (!string.IsNullOrEmpty(assignedTo) || unassigned.HasValue || !string.IsNullOrEmpty(assignmentMethod) || limit != 50 || offset != 0)
                {
                    supportCases = await supportService.GetAsync(assignedTo, unassigned, assignmentMethod, limit, offset).ConfigureAwait(false);
                }
                else
                {
                    // Use the original pagination method for backward compatibility (NFR-005)
                    supportCases = await supportService.GetAsync(pageNumber).ConfigureAwait(false);
                }
            }
            catch (ValidationException validationEx)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = validationEx.Code,
                        message = validationEx.Message,
                        field = validationEx.Field,
                        providedValue = validationEx.ProvidedValue
                    }
                });
            }
            catch (Exception ex)
            {
                SupportCaseLoggingHelper.LogReadFailure(logger, subscriptionId, resourceGroup, resourceId, id: null, ex);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);                
                throw;
            }

            if (null == supportCases)
            {
                return NotFound(new
                {
                    success = false,
                    error = new
                    {
                        code = "CASES_NOT_FOUND",
                        message = "No support cases found matching the specified criteria"
                    }
                });
            }

            activity?.SetStatus(ActivityStatusCode.Ok);

            // Return response format per specification section 5.2.4
            return Ok(new { success = true, data = supportCases });
        }

        [HttpGet(idTemplate)]
        [ProducesResponseType(typeof(SupportCase), 200)]
        public async Task<IActionResult> GetSupportCaseAsync(string subscriptionId, string resourceGroup, string resourceId, string id)
        {
            using Activity? activity = createActivity(nameof(GetSupportCaseAsync), subscriptionId, resourceGroup, resourceId, SupportCaseAccessType.Read, id);

            SupportCase? supportCase;

            try
            {
                supportCase = await supportService.GetAsync(id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SupportCaseLoggingHelper.LogReadFailure(logger, subscriptionId, resourceGroup, resourceId, id, ex);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            if (null == supportCase)
            {
                return NotFound(new
                {
                    success = false,
                    error = new
                    {
                        code = "CASE_NOT_FOUND",
                        message = $"Support case with ID '{id}' was not found"
                    }
                });
            }

            activity?.SetStatus(ActivityStatusCode.Ok);

            // Return response format per specification section 5.1.2
            return Ok(new { success = true, data = supportCase });
        }

        [HttpPost]
        public async Task<IActionResult> PostSupportCaseAsync(string subscriptionId, string resourceGroup, string resourceId, [FromBody] SupportCase supportCase)
        {
            using Activity? activity = createActivity(nameof(PostSupportCaseAsync), subscriptionId, resourceGroup, resourceId, SupportCaseAccessType.Create, supportCase?.Id);

            try
            {
                await supportService.CreateAsync(supportCase!).ConfigureAwait(false);
            }
            catch (ValidationException validationEx)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = validationEx.Code,
                        message = validationEx.Message,
                        field = validationEx.Field,
                        providedValue = validationEx.ProvidedValue
                    }
                });
            }
            catch (Exception ex)
            {
                SupportCaseLoggingHelper.LogCreateFailure(logger, subscriptionId, resourceGroup, resourceId, supportCase?.Id, ex);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);

            return Accepted();
        }

        [HttpPut(idTemplate)]
        public async Task<IActionResult> UpdateSupportCaseAsync(string subscriptionId, string resourceGroup, string resourceId, string id, SupportCase supportCaseIn)
        {
            using Activity? activity = createActivity(nameof(UpdateSupportCaseAsync), subscriptionId, resourceGroup, resourceId, SupportCaseAccessType.Update, id);

            SupportCase? supportCase;

            try
            {
                supportCase = await supportService.GetAsync(id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SupportCaseLoggingHelper.LogReadFailure(logger, subscriptionId, resourceGroup, resourceId, id, ex);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            if (null == supportCase)
            {
                return NotFound(new
                {
                    success = false,
                    error = new
                    {
                        code = "CASE_NOT_FOUND",
                        message = $"Support case with ID '{id}' was not found"
                    }
                });
            }

            try
            {
                await supportService.UpdateAsync(id, supportCaseIn).ConfigureAwait(false);
            }
            catch (ValidationException validationEx)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = validationEx.Code,
                        message = validationEx.Message,
                        field = validationEx.Field,
                        providedValue = validationEx.ProvidedValue
                    }
                });
            }
            catch (Exception ex)
            {
                SupportCaseLoggingHelper.LogUpdateFailure(logger, subscriptionId, resourceGroup, resourceId, id, ex);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);

            return Accepted();
        }

        [HttpDelete(idTemplate)]
        public async Task<IActionResult> DeleteSupportCaseAsync(string subscriptionId, string resourceGroup, string resourceId, string id)
        {
            using Activity? activity = createActivity(nameof(DeleteSupportCaseAsync), subscriptionId, resourceGroup, resourceId, SupportCaseAccessType.Delete, id);

            SupportCase? supportCase;

            try
            {
                supportCase = await supportService.GetAsync(id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SupportCaseLoggingHelper.LogReadFailure(logger, subscriptionId, resourceGroup, resourceId, id, ex);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            if (null == supportCase)
            {
                return NotFound(new
                {
                    success = false,
                    error = new
                    {
                        code = "CASE_NOT_FOUND",
                        message = $"Support case with ID '{id}' was not found"
                    }
                });
            }

            try
            {
                await supportService.RemoveAsync(id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SupportCaseLoggingHelper.LogRemoveFailure(logger, subscriptionId, resourceGroup, resourceId, id, ex);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);

            return Ok();
        }

        [HttpGet(fail)]
        public async Task<IActionResult> GetFail500(string subscriptionId, string resourceGroup, string resourceId, string id)
        {
            using Activity? activity = createActivity(nameof(GetFail500), subscriptionId, resourceGroup, resourceId, SupportCaseAccessType.Read, id);

            await Task.Delay(4000).ConfigureAwait(false);

            activity?.SetStatus(ActivityStatusCode.Error);

            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        private Activity? createActivity(
            string name,
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            SupportCaseAccessType accessType,
            string? id = null)
        {
            Activity? activity = TelemetryHelper.ActivitySource.StartActivity(name, ActivityKind.Internal);

            if (activity?.IsAllDataRequested == true)
            {
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