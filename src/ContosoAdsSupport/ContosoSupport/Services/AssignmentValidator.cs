using ContosoSupport.Models;

namespace ContosoSupport.Services
{
    public class AssignmentValidator
    {
        private readonly ISupportPersonService? supportPersonService;

        public AssignmentValidator(ISupportPersonService? supportPersonService = null)
        {
            this.supportPersonService = supportPersonService;
        }

        public void ValidateAssignment(string? caseId, string? supportPersonAlias)
        {
            if (string.IsNullOrEmpty(supportPersonAlias))
                return; // Null assignments are allowed per BR-003

            // BR-001, BR-006, BR-007: Support person must exist and be active
            if (!SupportPersonExists(supportPersonAlias) || !IsActive(supportPersonAlias))
            {
                throw new ValidationException("INVALID_SUPPORT_PERSON", 
                    $"Cannot assign support person '{supportPersonAlias}' - person not found or inactive",
                    "assignedSupportPerson",
                    supportPersonAlias);
            }

            // Check case status allows assignment
            if (!string.IsNullOrEmpty(caseId) && !CanAssignToCase(caseId))
            {
                throw new ValidationException("CASE_NOT_ASSIGNABLE");
            }
        }

        public void ValidateReasoning(string? reasoning)
        {
            // BR-008: Length validation (0-2000 characters)
            if (!string.IsNullOrEmpty(reasoning) && reasoning.Length > 2000)
            {
                throw new ValidationException("REASONING_TOO_LONG", 
                    "Reasoning text cannot exceed 2000 characters");
            }

            // BR-008: Content validation for inappropriate content
            if (!string.IsNullOrEmpty(reasoning) && ContainsInappropriateContent(reasoning))
            {
                throw new ValidationException("INAPPROPRIATE_REASONING_CONTENT",
                    "Reasoning text contains inappropriate content");
            }
        }

        private bool SupportPersonExists(string alias)
        {
            if (supportPersonService == null)
                return !string.IsNullOrEmpty(alias); // Fallback for demo

            // Use actual service to check existence
            try
            {
                return supportPersonService.ExistsAsync(alias).GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }
        }

        private bool IsActive(string alias)
        {
            if (supportPersonService == null)
                return true; // Fallback for demo

            // Use actual service to check if support person is active
            try
            {
                var supportPerson = supportPersonService.GetAsync(alias).GetAwaiter().GetResult();
                return supportPerson?.IsActive ?? false;
            }
            catch
            {
                return false;
            }
        }

        private bool CanAssignToCase(string caseId)
        {
            // TODO: Implement case status validation
            // For now, assume all cases can be assigned (placeholder implementation)
            return true;
        }

        private bool ContainsInappropriateContent(string text)
        {
            // Basic content validation per PRI-001, PRI-002 guidelines
            var inappropriatePatterns = new[] 
            { 
                "@", // Email addresses
                "customer name:", 
                "phone:",
                "personal assessment"
            };
            return inappropriatePatterns.Any(pattern => 
                text.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class ValidationException : Exception
    {
        public string Code { get; }
        public string? Field { get; }
        public string? ProvidedValue { get; }

        public ValidationException(string code, string? message = null, string? field = null, string? providedValue = null) 
            : base(message ?? code)
        {
            Code = code;
            Field = field;
            ProvidedValue = providedValue;
        }
    }
}