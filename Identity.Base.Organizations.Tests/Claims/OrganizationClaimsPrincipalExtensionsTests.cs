using System.Security.Claims;
using Identity.Base.Organizations.Claims;
using Shouldly;

namespace Identity.Base.Organizations.Tests.Claims;

public class OrganizationClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetOrganizationId_ReturnsParsedId()
    {
        var organizationId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationClaimTypes.OrganizationId, organizationId.ToString("D"))
        ]));

        principal.GetOrganizationId().ShouldBe(organizationId);
    }

    [Fact]
    public void GetOrganizationId_ReturnsNullWhenMissingOrInvalid()
    {
        var missing = new ClaimsPrincipal(new ClaimsIdentity());
        missing.GetOrganizationId().ShouldBeNull();

        var invalid = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationClaimTypes.OrganizationId, "not-a-guid")
        ]));

        invalid.GetOrganizationId().ShouldBeNull();
    }

    [Fact]
    public void GetOrganizationMemberships_ReturnsDistinctValidIds()
    {
        var organizationId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationClaimTypes.OrganizationMemberships, $"{organizationId:D} {organizationId:D} invalid")
        ]));

        var memberships = principal.GetOrganizationMemberships();

        memberships.Count.ShouldBe(1);
        memberships.ShouldContain(organizationId);
    }

    [Fact]
    public void GetOrganizationMemberships_ReturnsEmptyWhenMissingOrBlank()
    {
        var missing = new ClaimsPrincipal(new ClaimsIdentity());
        missing.GetOrganizationMemberships().ShouldBeEmpty();

        var blank = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationClaimTypes.OrganizationMemberships, " ")
        ]));

        blank.GetOrganizationMemberships().ShouldBeEmpty();
    }

    [Fact]
    public void HasOrganizationMembership_ReturnsTrueWhenPresent()
    {
        var organizationId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationClaimTypes.OrganizationMemberships, $"{organizationId:D} {Guid.NewGuid():D}")
        ]));

        principal.HasOrganizationMembership(organizationId).ShouldBeTrue();
        principal.HasOrganizationMembership(Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public void HasOrganizationMembership_ReturnsFalseForEmptyGuid()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationClaimTypes.OrganizationMemberships, $"{Guid.NewGuid():D}")
        ]));

        principal.HasOrganizationMembership(Guid.Empty).ShouldBeFalse();
    }
}
