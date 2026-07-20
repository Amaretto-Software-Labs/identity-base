using Identity.Base.Roles.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Base.Roles.Configurations;

public sealed class ServicePrincipalRoleConfiguration : IEntityTypeConfiguration<ServicePrincipalRole>
{
    public void Configure(EntityTypeBuilder<ServicePrincipalRole> builder)
    {
        builder.HasKey(assignment => new { assignment.ServicePrincipalId, assignment.RoleId });
        builder.HasOne(assignment => assignment.Role)
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
