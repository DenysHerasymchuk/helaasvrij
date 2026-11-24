using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Interview;
using InternshipCoach.Domain.Postings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class InterviewQuestionConfiguration : IEntityTypeConfiguration<InterviewQuestion>
{
    public void Configure(EntityTypeBuilder<InterviewQuestion> builder)
    {
        builder.ToTable("interview_questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(question => question.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(question => question.PostingId).HasColumnName("posting_id").IsRequired();
        builder.Property(question => question.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(question => question.Text).HasColumnName("text").HasMaxLength(InterviewQuestion.TextMaxLength).IsRequired();
        builder.Property(question => question.Project).HasColumnName("project").HasMaxLength(InterviewQuestion.ProjectMaxLength).IsRequired();
        builder.Property(question => question.RequirementId).HasColumnName("requirement_id");
        builder.Property(question => question.Position).HasColumnName("position");
        builder.Property(question => question.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        builder.HasOne<Posting>().WithMany().HasForeignKey(question => question.PostingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(question => new { question.PostingId, question.Position });
    }
}

public sealed class InterviewAnswerConfiguration : IEntityTypeConfiguration<InterviewAnswer>
{
    public void Configure(EntityTypeBuilder<InterviewAnswer> builder)
    {
        builder.ToTable("interview_answers");
        builder.HasKey(answer => answer.Id);
        builder.Property(answer => answer.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(answer => answer.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(answer => answer.PostingId).HasColumnName("posting_id").IsRequired();
        builder.Property(answer => answer.QuestionId).HasColumnName("question_id").IsRequired();
        builder.Property(answer => answer.Answer).HasColumnName("answer").HasMaxLength(InterviewAnswer.AnswerMaxLength).IsRequired();
        builder.Property(answer => answer.WhatWasGood).HasColumnName("what_was_good").HasMaxLength(InterviewAnswer.FeedbackMaxLength).IsRequired();
        builder.Property(answer => answer.WhatToAdd).HasColumnName("what_to_add").HasMaxLength(InterviewAnswer.FeedbackMaxLength).IsRequired();
        builder.Property(answer => answer.ExampleSentence).HasColumnName("example_sentence").HasMaxLength(InterviewAnswer.FeedbackMaxLength).IsRequired();
        builder.Property(answer => answer.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        builder.HasOne<Posting>().WithMany().HasForeignKey(answer => answer.PostingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(answer => new { answer.PostingId, answer.QuestionId });
    }
}

public sealed class CompanyQuestionConfiguration : IEntityTypeConfiguration<CompanyQuestion>
{
    public void Configure(EntityTypeBuilder<CompanyQuestion> builder)
    {
        builder.ToTable("company_questions");
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(question => question.CompanyKey).HasColumnName("company_key").HasMaxLength(CompanyQuestion.CompanyMaxLength).IsRequired();
        builder.Property(question => question.Company).HasColumnName("company").HasMaxLength(CompanyQuestion.CompanyMaxLength).IsRequired();
        builder.Property(question => question.Text).HasColumnName("text").HasMaxLength(CompanyQuestion.TextMaxLength).IsRequired();
        builder.HasIndex(question => question.CompanyKey);

        // Anonymous examples for the four demo companies. Fixed ids keep the migration stable.
        builder.HasData(CompanyQuestionSeed.All.Select(seed => new { seed.Id, seed.CompanyKey, seed.Company, seed.Text }));
    }
}

public static class CompanyQuestionSeed
{
    public sealed record Item(Guid Id, string CompanyKey, string Company, string Text);

    public static readonly IReadOnlyList<Item> All =
    [
        Make("00000000-0000-4000-8000-000000000101", "Brightlane", "Why do you want to build software for healthcare organisations?"),
        Make("00000000-0000-4000-8000-000000000102", "Brightlane", "Tell us about a time a test caught a bug before a user did."),
        Make("00000000-0000-4000-8000-000000000103", "Brightlane", "How would you explain a failing deployment to a non-technical colleague?"),
        Make("00000000-0000-4000-8000-000000000201", "Northwind Data", "How do you check that the output of a data pipeline is correct?"),
        Make("00000000-0000-4000-8000-000000000202", "Northwind Data", "Describe a messy dataset you worked with and how you cleaned it."),
        Make("00000000-0000-4000-8000-000000000203", "Northwind Data", "When would you pick SQL over Python for a transformation?"),
        Make("00000000-0000-4000-8000-000000000301", "Atelier Lumen", "Pourquoi voulez-vous développer des applications pour des musées ?"),
        Make("00000000-0000-4000-8000-000000000302", "Atelier Lumen", "Comment testez-vous une application mobile sur plusieurs tailles d'écran ?"),
        Make("00000000-0000-4000-8000-000000000303", "Atelier Lumen", "Parlez-moi d'un projet que vous avez mené en équipe."),
        Make("00000000-0000-4000-8000-000000000401", "Havenstad Logistics", "Wat trekt je aan in software voor de logistieke sector?"),
        Make("00000000-0000-4000-8000-000000000402", "Havenstad Logistics", "Hoe zou je een REST API ontwerpen om containers te volgen?"),
        Make("00000000-0000-4000-8000-000000000403", "Havenstad Logistics", "Hoe los je een merge-conflict op met je team?")
    ];

    private static Item Make(string id, string company, string text) => new(Guid.Parse(id), CompanyQuestion.KeyFor(company), company, text);
}
