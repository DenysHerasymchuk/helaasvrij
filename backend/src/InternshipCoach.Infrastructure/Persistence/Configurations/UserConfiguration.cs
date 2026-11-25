using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(Email.MaxLength)
            .HasConversion(email => email.Value, value => Email.Create(value))
            .IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.DisplayName).HasColumnName("display_name").HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash");
        builder.Property(user => user.GitHubId).HasColumnName("github_id");
        builder.Property(user => user.GitHubLogin).HasColumnName("github_login").HasMaxLength(100);
        builder.Property(user => user.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        builder.HasIndex(user => user.GitHubId).IsUnique().HasFilter("github_id IS NOT NULL");
    }
}
