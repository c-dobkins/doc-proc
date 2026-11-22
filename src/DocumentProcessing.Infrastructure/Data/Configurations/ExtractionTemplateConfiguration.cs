using DocumentProcessing.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessing.Infrastructure.Data.Configurations;

public class ExtractionTemplateConfiguration : IEntityTypeConfiguration<ExtractionTemplate>
{
    public void Configure(EntityTypeBuilder<ExtractionTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.PromptTemplate)
            .IsRequired();

        builder.OwnsMany(t => t.Fields, f =>
        {
            f.WithOwner().HasForeignKey("ExtractionTemplateId");
            f.Property(p => p.Name).IsRequired().HasMaxLength(200);
            f.Property(p => p.Description).HasMaxLength(500);
            f.Property(p => p.DataType).HasConversion<string>().HasMaxLength(50);
        });

        builder.HasIndex(t => t.Name).IsUnique();
    }
}
