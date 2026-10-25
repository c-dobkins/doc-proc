using DocumentProcessing.Api.Auth;
using DocumentProcessing.Api.DTOs;
using DocumentProcessing.Api.Middleware;
using DocumentProcessing.Core.Enums;
using DocumentProcessing.Core.Interfaces;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Api.Endpoints;

public static class TemplateEndpoints
{
    public static RouteGroupBuilder MapTemplateEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/templates").WithTags("Templates");

        group.MapGet("/", GetTemplates)
            .Produces<List<TemplateDto>>();

        group.MapGet("/{id:guid}", GetTemplateById)
            .Produces<TemplateDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateTemplate)
            .RequireAuthorization(AuthPolicies.Write)
            .AddEndpointFilter<ValidationFilter<CreateTemplateRequest>>()
            .Produces<TemplateDto>(StatusCodes.Status201Created);

        group.MapPut("/{id:guid}", UpdateTemplate)
            .RequireAuthorization(AuthPolicies.Write)
            .AddEndpointFilter<ValidationFilter<UpdateTemplateRequest>>()
            .Produces<TemplateDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteTemplate)
            .RequireAuthorization(AuthPolicies.Write)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetTemplates(
        ITemplateRepository templateRepository,
        CancellationToken ct)
    {
        var templates = await templateRepository.GetAllAsync(ct);
        return Results.Ok(templates.Select(MapToDto).ToList());
    }

    private static async Task<IResult> GetTemplateById(
        Guid id,
        ITemplateRepository templateRepository,
        CancellationToken ct)
    {
        var template = await templateRepository.GetByIdAsync(id, ct);
        return template is null ? Results.NotFound() : Results.Ok(MapToDto(template));
    }

    private static async Task<IResult> CreateTemplate(
        CreateTemplateRequest request,
        ITemplateRepository templateRepository,
        CancellationToken ct)
    {
        var template = new ExtractionTemplate
        {
            Name = request.Name,
            Description = request.Description,
            PromptTemplate = request.PromptTemplate,
            Fields = request.Fields.Select(f => new TemplateField
            {
                Name = f.Name,
                DataType = Enum.Parse<FieldDataType>(f.DataType, ignoreCase: true),
                IsRequired = f.IsRequired,
                Description = f.Description
            }).ToList()
        };

        await templateRepository.AddAsync(template, ct);
        return Results.Created($"/api/templates/{template.Id}", MapToDto(template));
    }

    private static async Task<IResult> UpdateTemplate(
        Guid id,
        UpdateTemplateRequest request,
        ITemplateRepository templateRepository,
        CancellationToken ct)
    {
        var template = await templateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Results.NotFound();

        template.Name = request.Name;
        template.Description = request.Description;
        template.PromptTemplate = request.PromptTemplate;
        template.Version++;
        template.Fields = request.Fields.Select(f => new TemplateField
        {
            Name = f.Name,
            DataType = Enum.Parse<FieldDataType>(f.DataType, ignoreCase: true),
            IsRequired = f.IsRequired,
            Description = f.Description
        }).ToList();

        await templateRepository.UpdateAsync(template, ct);
        return Results.Ok(MapToDto(template));
    }

    private static async Task<IResult> DeleteTemplate(
        Guid id,
        ITemplateRepository templateRepository,
        CancellationToken ct)
    {
        var template = await templateRepository.GetByIdAsync(id, ct);
        if (template is null)
            return Results.NotFound();

        await templateRepository.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static TemplateDto MapToDto(ExtractionTemplate template) =>
        new(
            template.Id,
            template.Name,
            template.Description,
            template.Fields.Select(f =>
                new TemplateFieldDto(f.Name, f.DataType.ToString(), f.IsRequired, f.Description)).ToList(),
            template.PromptTemplate,
            template.Version,
            template.CreatedAt,
            template.UpdatedAt);
}
