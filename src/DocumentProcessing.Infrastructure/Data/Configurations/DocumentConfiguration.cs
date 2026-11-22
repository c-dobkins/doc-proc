using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.StoragePath)
            .HasMaxLength(1000);

        builder.Property(d => d.Source)
            .HasMaxLength(200);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.FileType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(d => d.Template)
            .WithMany()
            .HasForeignKey(d => d.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.ProcessingResult)
            .WithOne(r => r.Document)
            .HasForeignKey<ProcessingResult>(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.UploadedAt);
    }
}
