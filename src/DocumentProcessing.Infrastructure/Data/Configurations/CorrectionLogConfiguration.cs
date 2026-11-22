using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Data.Configurations;

public class CorrectionLogConfiguration : IEntityTypeConfiguration<CorrectionLog>
{
    public void Configure(EntityTypeBuilder<CorrectionLog> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FieldName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.OriginalValue)
            .HasMaxLength(2000);

        builder.Property(c => c.CorrectedValue)
            .HasMaxLength(2000);

        builder.HasIndex(c => c.DocumentId);
    }
}
