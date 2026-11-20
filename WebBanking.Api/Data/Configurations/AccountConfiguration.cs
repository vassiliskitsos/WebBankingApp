using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebBanking.Api.Data.Entities;

namespace WebBanking.Api.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);

        // IBAN is at most 34 characters (ISO 13616).
        builder.Property(a => a.Iban)
            .IsRequired()
            .HasMaxLength(34);

        builder.HasIndex(a => a.Iban)
            .IsUnique();

        builder.Property(a => a.Balance)
            .HasPrecision(18, 2);

        builder.Property(a => a.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        // Npgsql maps a uint row version to PostgreSQL's xmin system column,
        // which the database bumps on every update.
        builder.Property(a => a.RowVersion)
            .IsRowVersion();
    }
}
