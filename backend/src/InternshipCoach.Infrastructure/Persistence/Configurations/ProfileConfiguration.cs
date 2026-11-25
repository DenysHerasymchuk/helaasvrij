using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class ProfileItemConfiguration : IEntityTypeConfiguration<ProfileItem>
{
    public void Configure(EntityTypeBuilder<ProfileItem> builder)
    {
        builder.ToTable("profile_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(item => item.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(ProfileItem.NameMaxLength).IsRequired();
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(ProfileItem.NameMaxLength).IsRequired();
        builder.Property(item => item.Quote).HasColumnName("quote").HasMaxLength(ProfileEvidence.QuoteMaxLength).IsRequired();
        builder.Property(item => item.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
        builder.Property(item => item.Confirmed).HasColumnName("confirmed").IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<SourceDocument>()
            .WithMany()
            .HasForeignKey(item => item.SourceDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(item => item.Evidence)
            .WithOne()
            .HasForeignKey(evidence => evidence.ProfileItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.Evidence).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => new { item.UserId, item.Kind, item.NormalizedName }).IsUnique();
    }
}

public sealed class ProfileEvidenceConfiguration : IEntityTypeConfiguration<ProfileEvidence>
{
    public void Configure(EntityTypeBuilder<ProfileEvidence> builder)
    {
        builder.ToTable("profile_evidence");
        builder.HasKey(evidence => evidence.Id);
        builder.Property(evidence => evidence.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(evidence => evidence.ProfileItemId).HasColumnName("profile_item_id").IsRequired();
        builder.Property(evidence => evidence.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
        builder.Property(evidence => evidence.Quote).HasColumnName("quote").HasMaxLength(ProfileEvidence.QuoteMaxLength).IsRequired();

        builder.HasOne<SourceDocument>()
            .WithMany()
            .HasForeignKey(evidence => evidence.SourceDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(evidence => evidence.ProfileItemId);
    }
}
