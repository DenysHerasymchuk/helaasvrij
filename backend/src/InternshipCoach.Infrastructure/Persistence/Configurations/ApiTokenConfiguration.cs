using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("api_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(token => token.Name).HasColumnName("name").HasMaxLength(ApiToken.NameMaxLength).IsRequired();
        builder.Property(token => token.Hash).HasColumnName("hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.Hint).HasColumnName("hint").HasMaxLength(8).IsRequired();
        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();
        builder.Property(token => token.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(token => token.RevokedAt).HasColumnName("revoked_at");
        builder.Ignore(token => token.IsActive);

        builder.HasIndex(token => token.Hash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.CreatedAt });
    }
}
