using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using System.Linq;

namespace Identity.Base.Extensions;

public static class ValidationExtensions
{
    public static IDictionary<string, string[]> ToDictionary(this ValidationResult result)
        => result.Errors
            .GroupBy(error => error.PropertyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => string.IsNullOrWhiteSpace(group.Key) ? "_" : group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);

    public static IDictionary<string, string[]> ToDictionary(this IdentityResult result)
        => result.Errors
            .GroupBy(error => error.Code ?? "Identity", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);
}
