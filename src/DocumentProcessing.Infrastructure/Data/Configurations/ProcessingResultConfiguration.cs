using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Data.Configurations;

public class ProcessingResultConfiguration : IEntityTypeConfiguration<ProcessingResult>
{
    public void Configure(EntityTypeBuilder<ProcessingResult> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.AiProvider)
            .HasMaxLength(100);

        builder.OwnsOne(r => r.Metrics, m =>
        {
            m.Property(p => p.EstimatedCost).HasPrecision(18, 6);
        });

        builder.HasMany(r => r.ExtractedFields)
            .WithOne()
            .HasForeignKey(f => f.ProcessingResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
