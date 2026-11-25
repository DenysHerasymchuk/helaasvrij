using InternshipCoach.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("sources");
        builder.HasKey(source => source.Id);
        builder.Property(source => source.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(source => source.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(source => source.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(source => source.Label).HasColumnName("label").HasMaxLength(Source.LabelMaxLength).IsRequired();
        builder.Property(source => source.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(source => source.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(source => source.Error).HasColumnName("error").HasMaxLength(Source.ErrorMaxLength);

        builder.HasMany(source => source.Documents)
            .WithOne()
            .HasForeignKey(document => document.SourceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(source => source.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(source => source.UserId);
    }
}

public sealed class SourceDocumentConfiguration : IEntityTypeConfiguration<SourceDocument>
{
    public void Configure(EntityTypeBuilder<SourceDocument> builder)
    {
        builder.ToTable("source_documents");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(document => document.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(document => document.Path).HasColumnName("path").HasMaxLength(SourceDocument.PathMaxLength).IsRequired();
        builder.Property(document => document.Text).HasColumnName("text").HasColumnType("text").IsRequired();
        builder.Property(document => document.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(document => document.SourceId);
    }
}
