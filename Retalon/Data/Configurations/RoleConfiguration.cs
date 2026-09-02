using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Retalon.Models.Entities;

namespace Retalon.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // Primary Key
        builder.HasKey(r => r.RoleId);

        // Properties
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.Description)
            .HasMaxLength(255);

        // Unique Role Name
        builder.HasIndex(r => r.Name)
            .IsUnique();

        builder.HasData(
            new Role
            {
                RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Admin",
                Description = "System administrator"
            },
            new Role
            {
                RoleId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Customer",
                Description = "Customer user"
            },
            new Role
            {
                RoleId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "WarehouseManager",
                Description = "Warehouse manager"
            }
        );
    }
}