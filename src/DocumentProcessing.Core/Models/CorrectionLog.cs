namespace DocumentProcessing.Core.Models;

/// <summary>
/// Records a human correction to an extracted field for the feedback loop.
/// </summary>
public class CorrectionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public required string FieldName { get; set; }

    public string? OriginalValue { get; set; }

    public string? CorrectedValue { get; set; }

    public DateTimeOffset CorrectedAt { get; set; } = DateTimeOffset.UtcNow;
}
