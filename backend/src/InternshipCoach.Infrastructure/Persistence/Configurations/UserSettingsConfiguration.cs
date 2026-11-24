using InternshipCoach.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(settings => settings.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(settings => settings.DefaultLanguage).HasColumnName("default_language").HasMaxLength(2).IsRequired();
        builder.Property(settings => settings.StrongMatchThreshold).HasColumnName("strong_match_threshold");
        builder.Property(settings => settings.EmailAlerts).HasColumnName("email_alerts");

        builder.HasIndex(settings => settings.UserId).IsUnique();
    }
}
