using DocumentProcessing.Core.Enums;

namespace DocumentProcessing.Core.Models;

/// <summary>
/// Defines a field to be extracted from a document type.
/// </summary>
public class TemplateField
{
    public required string Name { get; set; }

    public FieldDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    public string? Description { get; set; }
}

/// <summary>
/// Defines a reusable extraction template for a document type.
/// </summary>
public class ExtractionTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public List<TemplateField> Fields { get; set; } = [];

    public required string PromptTemplate { get; set; }

    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
