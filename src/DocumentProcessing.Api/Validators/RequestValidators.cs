using DocumentProcessing.Api.DTOs;
using DocumentProcessing.Core.Enums;
using FluentValidation;

namespace DocumentProcessing.Api.Validators;

public class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    private static readonly string[] RequiredPlaceholders = ["{ocrText}", "{fieldDefinitions}", "{outputFormat}"];

    public CreateTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(200).WithMessage("Template name must not exceed 200 characters.");

        RuleFor(x => x.PromptTemplate)
            .NotEmpty().WithMessage("Prompt template is required.")
            .Must(ContainsAllPlaceholders)
            .WithMessage($"Prompt template must contain all required placeholders: {string.Join(", ", RequiredPlaceholders)}");

        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field is required.")
            .Must(fields => fields.DistinctBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Count() == fields.Count)
            .WithMessage("Field names must be unique (case-insensitive).");

        RuleForEach(x => x.Fields).SetValidator(new TemplateFieldDtoValidator());
    }

    private static bool ContainsAllPlaceholders(string prompt) =>
        RequiredPlaceholders.All(p => prompt.Contains(p, StringComparison.OrdinalIgnoreCase));
}

public class UpdateTemplateRequestValidator : AbstractValidator<UpdateTemplateRequest>
{
    private static readonly string[] RequiredPlaceholders = ["{ocrText}", "{fieldDefinitions}", "{outputFormat}"];

    public UpdateTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(200).WithMessage("Template name must not exceed 200 characters.");

        RuleFor(x => x.PromptTemplate)
            .NotEmpty().WithMessage("Prompt template is required.")
            .Must(ContainsAllPlaceholders)
            .WithMessage($"Prompt template must contain all required placeholders: {string.Join(", ", RequiredPlaceholders)}");

        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field is required.")
            .Must(fields => fields.DistinctBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Count() == fields.Count)
            .WithMessage("Field names must be unique (case-insensitive).");

        RuleForEach(x => x.Fields).SetValidator(new TemplateFieldDtoValidator());
    }

    private static bool ContainsAllPlaceholders(string prompt) =>
        RequiredPlaceholders.All(p => prompt.Contains(p, StringComparison.OrdinalIgnoreCase));
}

public class TemplateFieldDtoValidator : AbstractValidator<TemplateFieldDto>
{
    private static readonly HashSet<string> ValidDataTypes =
        Enum.GetNames<FieldDataType>().ToHashSet(StringComparer.OrdinalIgnoreCase);

    public TemplateFieldDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Field name is required.")
            .MaximumLength(200).WithMessage("Field name must not exceed 200 characters.");

        RuleFor(x => x.DataType)
            .NotEmpty().WithMessage("Field data type is required.")
            .Must(dt => ValidDataTypes.Contains(dt))
            .WithMessage(x => $"Invalid data type '{x.DataType}'. Valid types: {string.Join(", ", ValidDataTypes)}");
    }
}

public class UpdateFieldsRequestValidator : AbstractValidator<UpdateFieldsRequest>
{
    public UpdateFieldsRequestValidator()
    {
        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field update is required.");

        RuleForEach(x => x.Fields).ChildRules(field =>
        {
            field.RuleFor(f => f.Name)
                .NotEmpty().WithMessage("Field name is required.");
        });
    }
}
