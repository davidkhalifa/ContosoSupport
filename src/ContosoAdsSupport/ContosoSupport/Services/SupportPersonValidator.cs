using ContosoSupport.Models;
using System.Text.RegularExpressions;

namespace ContosoSupport.Services
{
    public class SupportPersonValidator
    {
        private static readonly string[] ValidSeniorityLevels = { "Junior", "MidLevel", "Senior", "Lead", "Manager" };
        private static readonly string[] ValidSpecializations = 
        {
            "Authentication", "Network Security", "Windows Server", "Database", 
            "Performance Tuning", "Cloud Services", "Azure Active Directory",
            "Email Systems", "Backup & Recovery", "Hardware", "Mobile Devices"
        };

        public void ValidateSupportPerson(SupportPerson supportPerson, bool isUpdate = false)
        {
            var errors = new List<ValidationError>();

            // BR-002: Alias validation
            if (!isUpdate && string.IsNullOrWhiteSpace(supportPerson.Alias))
            {
                errors.Add(new ValidationError("alias", "Alias is required"));
            }
            else if (!string.IsNullOrWhiteSpace(supportPerson.Alias))
            {
                if (supportPerson.Alias.Length < 3 || supportPerson.Alias.Length > 50)
                {
                    errors.Add(new ValidationError("alias", "Alias must be between 3-50 characters"));
                }
                if (!Regex.IsMatch(supportPerson.Alias, @"^[a-zA-Z0-9._-]+$"))
                {
                    errors.Add(new ValidationError("alias", "Alias can only contain alphanumeric characters, underscore, hyphen, and period"));
                }
            }

            // Name validation
            if (string.IsNullOrWhiteSpace(supportPerson.Name))
            {
                errors.Add(new ValidationError("name", "Name is required"));
            }
            else if (supportPerson.Name.Length < 2 || supportPerson.Name.Length > 100)
            {
                errors.Add(new ValidationError("name", "Name must be between 2-100 characters"));
            }

            // BR-001: Email validation
            if (string.IsNullOrWhiteSpace(supportPerson.Email))
            {
                errors.Add(new ValidationError("email", "Email is required"));
            }
            else if (!IsValidEmail(supportPerson.Email))
            {
                errors.Add(new ValidationError("email", "Invalid email format"));
            }

            // BR-003: Specializations validation
            if (supportPerson.Specializations == null || !supportPerson.Specializations.Any())
            {
                errors.Add(new ValidationError("specializations", "At least one specialization is required"));
            }
            else
            {
                if (supportPerson.Specializations.Count > 10)
                {
                    errors.Add(new ValidationError("specializations", "Maximum 10 specializations allowed"));
                }

                foreach (var spec in supportPerson.Specializations)
                {
                    if (string.IsNullOrWhiteSpace(spec) || spec.Length < 2 || spec.Length > 50)
                    {
                        errors.Add(new ValidationError("specializations", "Each specialization must be between 2-50 characters"));
                        break;
                    }
                    if (!ValidSpecializations.Contains(spec))
                    {
                        errors.Add(new ValidationError("specializations", $"Specialization '{spec}' is not from the approved list"));
                        break;
                    }
                }
            }

            // Seniority validation
            if (string.IsNullOrWhiteSpace(supportPerson.Seniority))
            {
                errors.Add(new ValidationError("seniority", "Seniority is required"));
            }
            else if (!ValidSeniorityLevels.Contains(supportPerson.Seniority))
            {
                errors.Add(new ValidationError("seniority", $"Seniority must be one of: {string.Join(", ", ValidSeniorityLevels)}"));
            }

            // Workload validation
            if (supportPerson.CurrentWorkload < 0 || supportPerson.CurrentWorkload > 100)
            {
                errors.Add(new ValidationError("current_workload", "Current workload must be between 0-100"));
            }

            // Rating validation
            if (supportPerson.CustomerSatisfactionRating.HasValue && 
                (supportPerson.CustomerSatisfactionRating < 1.0 || supportPerson.CustomerSatisfactionRating > 5.0))
            {
                errors.Add(new ValidationError("customer_satisfaction_rating", "Customer satisfaction rating must be between 1.0-5.0"));
            }

            // Resolution time validation
            if (supportPerson.AverageResolutionTime.HasValue && supportPerson.AverageResolutionTime < 0)
            {
                errors.Add(new ValidationError("average_resolution_time", "Average resolution time must be >= 0"));
            }

            if (errors.Any())
            {
                throw new SupportPersonValidationException(errors);
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ValidationError
    {
        public string Field { get; set; }
        public string Message { get; set; }

        public ValidationError(string field, string message)
        {
            Field = field;
            Message = message;
        }
    }

    public class SupportPersonValidationException : Exception
    {
        public List<ValidationError> Errors { get; }

        public SupportPersonValidationException(List<ValidationError> errors) 
            : base("Validation failed")
        {
            Errors = errors;
        }
    }
}