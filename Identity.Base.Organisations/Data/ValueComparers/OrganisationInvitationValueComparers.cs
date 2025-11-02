using System;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Identity.Base.Organisations.Data.ValueComparers;

internal static class OrganisationInvitationValueComparers
{
    public static ValueComparer<Guid[]> RoleIds { get; } = new(
        (left, right) => SequenceEqual(left, right),
        value => ComputeHashCode(value),
        value => Clone(value));

    private static bool SequenceEqual(Guid[]? left, Guid[]? right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);

        if (normalizedLeft.Length != normalizedRight.Length)
        {
            return false;
        }

        for (var index = 0; index < normalizedLeft.Length; index++)
        {
            if (normalizedLeft[index] != normalizedRight[index])
            {
                return false;
            }
        }

        return true;
    }

    private static int ComputeHashCode(Guid[]? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var item in normalized)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    private static Guid[] Clone(Guid[]? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return Array.Empty<Guid>();
        }

        var copy = new Guid[normalized.Length];
        Array.Copy(normalized, copy, normalized.Length);
        return copy;
    }

    private static Guid[] Normalize(Guid[]? value) => value is { Length: > 0 } ? value : Array.Empty<Guid>();
}
