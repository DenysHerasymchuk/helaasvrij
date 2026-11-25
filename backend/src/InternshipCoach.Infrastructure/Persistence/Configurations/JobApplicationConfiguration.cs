using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sprints;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(application => application.Id);
        builder.Property(application => application.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(application => application.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(application => application.PostingId).HasColumnName("posting_id").IsRequired();
        builder.Property(application => application.Stage).HasColumnName("stage").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(application => application.AppliedAt).HasColumnName("applied_at");
        builder.Property(application => application.StageChangedAt).HasColumnName("stage_changed_at").IsRequired();
        builder.Property(application => application.Notes).HasColumnName("notes").HasMaxLength(JobApplication.NotesMaxLength).IsRequired();
        builder.Property(application => application.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        builder.Property(application => application.SprintId).HasColumnName("sprint_id");
        builder.HasOne<Sprint>().WithMany().HasForeignKey(application => application.SprintId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(application => application.Events)
            .WithOne()
            .HasForeignKey(applicationEvent => applicationEvent.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(application => application.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(application => application.SprintId);

        // Deleting a posting removes its application.
        builder.HasOne<Posting>().WithMany().HasForeignKey(application => application.PostingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(application => new { application.UserId, application.PostingId }).IsUnique();
    }
}
