using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(document => document.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(document => document.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(document => document.Language).HasColumnName("language").HasMaxLength(Document.LanguageMaxLength).IsRequired();
        builder.Property(document => document.Title).HasColumnName("title").HasMaxLength(Document.TitleMaxLength).IsRequired();
        builder.Property(document => document.Content).HasColumnName("content").IsRequired();
        builder.Property(document => document.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();
        builder.Property(document => document.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(document => new { document.UserId, document.CreatedAt });
    }
}
