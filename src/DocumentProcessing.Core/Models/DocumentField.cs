using DocumentProcessing.Core.Enums;

namespace DocumentProcessing.Core.Models;

/// <summary>
/// Represents a single extracted field with its value and confidence.
/// </summary>
public class DocumentField
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Value { get; set; }

    public double Confidence { get; set; }

    public FieldDataType DataType { get; set; }

    public Guid ProcessingResultId { get; set; }
}
