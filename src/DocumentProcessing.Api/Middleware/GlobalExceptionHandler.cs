using System.Net;
using FluentValidation;

namespace DocumentProcessing.Api.Middleware;

/// <summary>
/// Global exception handling middleware. Catches unhandled exceptions and
/// FluentValidation errors, returning consistent ProblemDetails responses.
/// </summary>
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Validation failed for {Path}", context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/problem+json";

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                title = "Validation Error",
                status = 400,
                detail = "One or more validation errors occurred.",
                errors
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — no response needed
            logger.LogDebug("Request cancelled by client for {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                title = "Internal Server Error",
                status = 500,
                detail = env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred."
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
