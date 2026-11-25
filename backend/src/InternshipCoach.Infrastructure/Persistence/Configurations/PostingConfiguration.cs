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
        builder.Property(posting => posting.Number).HasColumnName("number").IsRequired();
        builder.Property(posting => posting.IsDemo).HasColumnName("is_demo");
        builder.Property(posting => posting.GenericAnswer).HasColumnName("generic_answer").HasMaxLength(Posting.GenericAnswerMaxLength);
        builder.Property(posting => posting.GenericAnswerAt).HasColumnName("generic_answer_at");
        builder.Property(posting => posting.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        builder.HasMany(posting => posting.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.PostingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(posting => posting.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(posting => posting.Snapshots)
            .WithOne()
            .HasForeignKey(snapshot => snapshot.PostingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(posting => posting.Snapshots).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(posting => posting.IsAnalysed);

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
        builder.Property(requirement => requirement.Coverage)
            .HasColumnName("coverage")
            .HasMaxLength(10)
            .HasConversion<string>()
            .HasDefaultValue(Coverage.None)
            .IsRequired();
        builder.Property(requirement => requirement.EvidenceQuote).HasColumnName("evidence_quote").HasMaxLength(Requirement.QuoteMaxLength);
        builder.Property(requirement => requirement.EvidenceSourceDocumentId).HasColumnName("evidence_source_document_id");
        builder.Property(requirement => requirement.Action).HasColumnName("action").HasMaxLength(Requirement.ActionMaxLength);
        builder.Property(requirement => requirement.ActionProject).HasColumnName("action_project").HasMaxLength(Requirement.ProjectMaxLength);
        builder.Property(requirement => requirement.DueDate).HasColumnName("due_date");
        builder.Property(requirement => requirement.Done).HasColumnName("done");
        builder.Property(requirement => requirement.AnalysedAt).HasColumnName("analysed_at");

        builder.HasIndex(requirement => requirement.PostingId);
    }
}

public sealed class ScoreSnapshotConfiguration : IEntityTypeConfiguration<ScoreSnapshot>
{
    public void Configure(EntityTypeBuilder<ScoreSnapshot> builder)
    {
        builder.ToTable("score_snapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(snapshot => snapshot.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(snapshot => snapshot.PostingId).HasColumnName("posting_id").IsRequired();
        builder.Property(snapshot => snapshot.Score).HasColumnName("score");
        builder.Property(snapshot => snapshot.Reason).HasColumnName("reason").HasMaxLength(ScoreSnapshot.ReasonMaxLength).IsRequired();
        builder.Property(snapshot => snapshot.TakenAt).HasColumnName("taken_at");

        builder.HasIndex(snapshot => new { snapshot.PostingId, snapshot.TakenAt });
    }
}
