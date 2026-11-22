using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Data.Configurations;

public class DocumentFieldConfiguration : IEntityTypeConfiguration<DocumentField>
{
    public void Configure(EntityTypeBuilder<DocumentField> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Value)
            .HasMaxLength(2000);

        builder.Property(f => f.DataType)
            .HasConversion<string>()
            .HasMaxLength(50);
    }
}
