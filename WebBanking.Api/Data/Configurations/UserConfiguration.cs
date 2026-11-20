using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebBanking.Api.Data.Entities;

namespace WebBanking.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        // Same limit as RegisterRequest.Username.
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        // Stored as "Customer" / "Admin" (README section 4).
        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(u => u.Accounts)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
