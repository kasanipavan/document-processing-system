using System.ComponentModel.DataAnnotations;

namespace DocumentProcessor.Web.Validation
{
    public class RequiredTrueAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is bool boolValue && boolValue)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(ErrorMessage ?? "This field must be checked.");
        }
    }
}