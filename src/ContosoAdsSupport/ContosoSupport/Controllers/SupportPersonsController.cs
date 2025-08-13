using ContosoSupport.InstrumentationHelpers;
using ContosoSupport.Models;
using ContosoSupport.Services;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace ContosoSupport.Controllers
{
    [Route("{subscriptionId}/{resourceGroup}/{resourceId}/supportpersons")]
    [ApiController]
    public class SupportPersonsController : ControllerBase
    {
        private readonly ISupportPersonService supportPersonService;
        private readonly ILogger<SupportPersonsController> logger;

        public SupportPersonsController(ISupportPersonService supportPersonService, ILogger<SupportPersonsController> logger)
        {
            this.supportPersonService = supportPersonService;
            this.logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetSupportPersonsAsync(
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            string? specialization = null,
            string? seniority = null,
            bool? available = null,
            int limit = 50,
            int offset = 0,
            string sortBy = "name",
            string sortOrder = "asc")
        {
            using Activity? activity = CreateActivity(nameof(GetSupportPersonsAsync), subscriptionId, resourceGroup, resourceId);

            try
            {
                var supportPersons = await supportPersonService.GetAsync(
                    specialization, seniority, available, limit, offset, sortBy, sortOrder).ConfigureAwait(false);
                
                var total = await supportPersonService.GetDocumentCountAsync().ConfigureAwait(false);

                var response = new
                {
                    success = true,
                    data = supportPersons,
                    pagination = new
                    {
                        total = total,
                        limit = limit,
                        offset = offset,
                        hasNext = offset + limit < total,
                        hasPrevious = offset > 0
                    }
                };

                activity?.SetStatus(ActivityStatusCode.Ok);
                return Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get support persons");
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        [HttpGet("{alias}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetSupportPersonAsync(
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            string alias)
        {
            using Activity? activity = CreateActivity(nameof(GetSupportPersonAsync), subscriptionId, resourceGroup, resourceId, alias);

            try
            {
                var supportPerson = await supportPersonService.GetAsync(alias).ConfigureAwait(false);

                if (supportPerson == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = new
                        {
                            code = "SUPPORT_PERSON_NOT_FOUND",
                            message = $"Support person with alias '{alias}' was not found"
                        }
                    });
                }

                activity?.SetStatus(ActivityStatusCode.Ok);
                return Ok(new { success = true, data = supportPerson });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get support person {Alias}", alias);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(object), 201)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 409)]
        public async Task<IActionResult> CreateSupportPersonAsync(
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            [FromBody] SupportPerson supportPerson)
        {
            using Activity? activity = CreateActivity(nameof(CreateSupportPersonAsync), subscriptionId, resourceGroup, resourceId, supportPerson?.Alias);

            try
            {
                await supportPersonService.CreateAsync(supportPerson!).ConfigureAwait(false);

                activity?.SetStatus(ActivityStatusCode.Ok);
                return CreatedAtAction(
                    nameof(GetSupportPersonAsync),
                    new { subscriptionId, resourceGroup, resourceId, alias = supportPerson!.Alias },
                    new { success = true, data = supportPerson });
            }
            catch (SupportPersonValidationException validationEx)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "VALIDATION_ERROR",
                        message = "Invalid input data",
                        details = validationEx.Errors.Select(e => new { field = e.Field, message = e.Message })
                    }
                });
            }
            catch (ConflictException conflictEx)
            {
                return Conflict(new
                {
                    success = false,
                    error = new
                    {
                        code = conflictEx.Code,
                        message = conflictEx.Message
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create support person");
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        [HttpPut("{alias}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> UpdateSupportPersonAsync(
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            string alias,
            [FromBody] SupportPerson supportPerson)
        {
            using Activity? activity = CreateActivity(nameof(UpdateSupportPersonAsync), subscriptionId, resourceGroup, resourceId, alias);

            try
            {
                await supportPersonService.UpdateAsync(alias, supportPerson).ConfigureAwait(false);

                activity?.SetStatus(ActivityStatusCode.Ok);
                return Ok(new { success = true, data = supportPerson });
            }
            catch (SupportPersonValidationException validationEx)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "VALIDATION_ERROR",
                        message = "Invalid input data",
                        details = validationEx.Errors.Select(e => new { field = e.Field, message = e.Message })
                    }
                });
            }
            catch (NotFoundException notFoundEx)
            {
                return NotFound(new
                {
                    success = false,
                    error = new
                    {
                        code = notFoundEx.Code,
                        message = notFoundEx.Message
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update support person {Alias}", alias);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        [HttpDelete("{alias}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 409)]
        public async Task<IActionResult> DeleteSupportPersonAsync(
            string subscriptionId,
            string resourceGroup,
            string resourceId,
            string alias)
        {
            using Activity? activity = CreateActivity(nameof(DeleteSupportPersonAsync), subscriptionId, resourceGroup, resourceId, alias);

            try
            {
                await supportPersonService.RemoveAsync(alias).ConfigureAwait(false);

                activity?.SetStatus(ActivityStatusCode.Ok);
                return NoContent();
            }
            catch (ConflictException conflictEx)
            {
                return Conflict(new
                {
                    success = false,
                    error = new
                    {
                        code = conflictEx.Code,
                        message = conflictEx.Message,
                        details = conflictEx.Details
                    }
                });
            }
            catch (NotFoundException notFoundEx)
            {
                return NotFound(new
                {
                    success = false,
                    error = new
                    {
                        code = notFoundEx.Code,
                        message = notFoundEx.Message
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete support person {Alias}", alias);
                activity?.RecordException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        private Activity? CreateActivity(string name, string subscriptionId, string resourceGroup, string resourceId, string? id = null)
        {
            Activity? activity = TelemetryHelper.ActivitySource.StartActivity(name, ActivityKind.Internal);

            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("resourceType", "Contoso.Support/ticketingSystem");
                activity.SetTag("resourceId", $"subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Contoso.Support/ticketingSystem/{resourceId}");
                activity.SetTag("entityType", nameof(SupportPerson));
                if (!string.IsNullOrEmpty(id))
                    activity.SetTag("entityId", id);
            }

            return activity;
        }
    }
}