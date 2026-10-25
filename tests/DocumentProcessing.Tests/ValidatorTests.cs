using DocumentProcessing.Api.DTOs;
using DocumentProcessing.Api.Validators;
using FluentValidation.TestHelper;

namespace DocumentProcessing.Tests;

public class ValidatorTests
{
    #region CreateTemplateRequestValidator

    private readonly CreateTemplateRequestValidator _createValidator = new();

    [Fact]
    public void CreateTemplate_ValidRequest_PassesValidation()
    {
        var request = new CreateTemplateRequest(
            "Invoice",
            "Invoice extraction",
            [new TemplateFieldDto("Amount", "Currency", true, "Total amount")],
            "Extract: {ocrText}\nFields: {fieldDefinitions}\nFormat: {outputFormat}");

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateTemplate_EmptyName_FailsValidation()
    {
        var request = new CreateTemplateRequest(
            "",
            null,
            [new TemplateFieldDto("Amount", "Currency", true, null)],
            "prompt");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateTemplate_EmptyPromptTemplate_FailsValidation()
    {
        var request = new CreateTemplateRequest(
            "Test",
            null,
            [new TemplateFieldDto("Amount", "Currency", true, null)],
            "");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PromptTemplate);
    }

    [Fact]
    public void CreateTemplate_EmptyFields_FailsValidation()
    {
        var request = new CreateTemplateRequest("Test", null, [], "prompt");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Fields);
    }

    [Fact]
    public void CreateTemplate_MissingRequiredPlaceholder_FailsValidation()
    {
        var request = new CreateTemplateRequest(
            "Test",
            null,
            [new TemplateFieldDto("Amount", "Currency", true, null)],
            "Only has {ocrText} — missing the other two");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PromptTemplate);
    }

    [Fact]
    public void CreateTemplate_DuplicateFieldNames_FailsValidation()
    {
        var request = new CreateTemplateRequest(
            "Test",
            null,
            [
                new TemplateFieldDto("Amount", "Currency", true, null),
                new TemplateFieldDto("amount", "Decimal", false, null)
            ],
            "{ocrText}\n{fieldDefinitions}\n{outputFormat}");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Fields);
    }

    [Fact]
    public void CreateTemplate_InvalidFieldDataType_FailsValidation()
    {
        var request = new CreateTemplateRequest(
            "Test",
            null,
            [new TemplateFieldDto("Field1", "InvalidType", false, null)],
            "prompt");

        var result = _createValidator.TestValidate(request);

        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Invalid data type"));
    }

    [Fact]
    public void CreateTemplate_FieldWithEmptyName_FailsValidation()
    {
        var request = new CreateTemplateRequest(
            "Test",
            null,
            [new TemplateFieldDto("", "String", false, null)],
            "prompt");

        var result = _createValidator.TestValidate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateTemplate_NameTooLong_FailsValidation()
    {
        var request = new CreateTemplateRequest(
            new string('A', 201),
            null,
            [new TemplateFieldDto("Field1", "String", false, null)],
            "prompt");

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    #endregion

    #region UpdateTemplateRequestValidator

    private readonly UpdateTemplateRequestValidator _updateValidator = new();

    [Fact]
    public void UpdateTemplate_ValidRequest_PassesValidation()
    {
        var request = new UpdateTemplateRequest(
            "Updated",
            "Desc",
            [new TemplateFieldDto("Amount", "Decimal", true, null)],
            "Extract: {ocrText}\nFields: {fieldDefinitions}\nFormat: {outputFormat}");

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateTemplate_EmptyFields_FailsValidation()
    {
        var request = new UpdateTemplateRequest("Test", null, [], "prompt");

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Fields);
    }

    [Fact]
    public void UpdateTemplate_MissingPlaceholder_FailsValidation()
    {
        var request = new UpdateTemplateRequest(
            "Test",
            null,
            [new TemplateFieldDto("Amount", "Currency", true, null)],
            "Extract {ocrText} — missing fieldDefinitions and outputFormat");

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PromptTemplate);
    }

    #endregion

    #region UpdateFieldsRequestValidator

    private readonly UpdateFieldsRequestValidator _fieldsValidator = new();

    [Fact]
    public void UpdateFields_ValidRequest_PassesValidation()
    {
        var request = new UpdateFieldsRequest(
            [new FieldUpdateDto("InvoiceNumber", "INV-001")]);

        var result = _fieldsValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateFields_EmptyFieldsList_FailsValidation()
    {
        var request = new UpdateFieldsRequest([]);

        var result = _fieldsValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Fields);
    }

    [Fact]
    public void UpdateFields_FieldWithEmptyName_FailsValidation()
    {
        var request = new UpdateFieldsRequest(
            [new FieldUpdateDto("", "value")]);

        var result = _fieldsValidator.TestValidate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateFields_NullValue_PassesValidation()
    {
        var request = new UpdateFieldsRequest(
            [new FieldUpdateDto("FieldName", null)]);

        var result = _fieldsValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region TemplateFieldDtoValidator — all valid data types

    private readonly TemplateFieldDtoValidator _fieldValidator = new();

    [Theory]
    [InlineData("String")]
    [InlineData("Integer")]
    [InlineData("Decimal")]
    [InlineData("Date")]
    [InlineData("Boolean")]
    [InlineData("Currency")]
    [InlineData("Address")]
    [InlineData("PhoneNumber")]
    [InlineData("Email")]
    public void TemplateField_ValidDataTypes_PassValidation(string dataType)
    {
        var field = new TemplateFieldDto("TestField", dataType, false, null);

        var result = _fieldValidator.TestValidate(field);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void TemplateField_CaseInsensitiveDataType_PassesValidation()
    {
        var field = new TemplateFieldDto("TestField", "string", false, null);

        var result = _fieldValidator.TestValidate(field);

        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
