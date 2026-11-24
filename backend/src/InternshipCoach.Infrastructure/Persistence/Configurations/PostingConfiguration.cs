using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class PostingConfiguration : IEntityTypeConfiguration<Posting>
{
    public void Configure(EntityTypeBuilder<Posting> builder)
    {
        builder.ToTable("postings");
        builder.HasKey(posting => posting.Id);
        builder.Property(posting => posting.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(posting => posting.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(posting => posting.Company).HasColumnName("company").HasMaxLength(Posting.CompanyMaxLength).IsRequired();
        builder.Property(posting => posting.Title).HasColumnName("title").HasMaxLength(Posting.TitleMaxLength).IsRequired();
        builder.Property(posting => posting.City).HasColumnName("city").HasMaxLength(Posting.CityMaxLength).IsRequired();
        builder.Property(posting => posting.Language)
            .HasColumnName("language")
            .HasMaxLength(2)
            .HasConversion(language => language.ToCode(), code => ParseLanguage(code))
            .IsRequired();
        builder.Property(posting => posting.Url).HasColumnName("url").HasMaxLength(Posting.UrlMaxLength);
        builder.Property(posting => posting.RawText).HasColumnName("raw_text").IsRequired();
        builder.Property(posting => posting.Deadline).HasColumnName("deadline");
        builder.Property(posting => posting.InterviewAt).HasColumnName("interview_at");
        builder.Property(posting => posting.IsDemo).HasColumnName("is_demo");
        builder.Property(posting => posting.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        builder.HasMany(posting => posting.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.PostingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(posting => posting.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(posting => new { posting.UserId, posting.CreatedAt });
    }

    private static PostingLanguage ParseLanguage(string code) =>
        PostingLanguages.TryParse(code, out var language) ? language : PostingLanguage.En;
}

public sealed class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
{
    public void Configure(EntityTypeBuilder<Requirement> builder)
    {
        builder.ToTable("requirements");
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(requirement => requirement.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(requirement => requirement.PostingId).HasColumnName("posting_id").IsRequired();
        builder.Property(requirement => requirement.Position).HasColumnName("position");
        builder.Property(requirement => requirement.Text).HasColumnName("text").HasMaxLength(Requirement.TextMaxLength).IsRequired();
        builder.Property(requirement => requirement.Importance)
            .HasColumnName("importance")
            .HasMaxLength(10)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(requirement => requirement.PostingId);
    }
}
