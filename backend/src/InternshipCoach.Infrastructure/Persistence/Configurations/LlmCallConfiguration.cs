using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class LlmCallConfiguration : IEntityTypeConfiguration<LlmCall>
{
    public void Configure(EntityTypeBuilder<LlmCall> builder)
    {
        builder.ToTable("llm_calls");
        builder.HasKey(call => call.Id);
        builder.Property(call => call.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(call => call.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(call => call.Feature).HasColumnName("feature").HasMaxLength(LlmCall.FeatureMaxLength).IsRequired();
        builder.Property(call => call.Model).HasColumnName("model").HasMaxLength(LlmCall.ModelMaxLength).IsRequired();
        builder.Property(call => call.InputTokens).HasColumnName("input_tokens");
        builder.Property(call => call.OutputTokens).HasColumnName("output_tokens");
        builder.Property(call => call.DurationMs).HasColumnName("duration_ms");
        builder.Property(call => call.Success).HasColumnName("success");
        builder.Property(call => call.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        builder.HasIndex(call => new { call.UserId, call.CreatedAt });
    }
}
