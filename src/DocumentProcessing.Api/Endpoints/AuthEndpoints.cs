using DocumentProcessing.Api.Auth;
using DocumentProcessing.Api.DTOs;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        // Lets the UI show read-only mode and validate a key the user entered
        group.MapGet("/status", (HttpContext context, IOptions<ApiKeyOptions> options) =>
            Results.Ok(new AuthStatusDto(
                WritesRequireKey: options.Value.RequireApiKeyForWrites,
                CanWrite: !options.Value.RequireApiKeyForWrites || context.User.Identity?.IsAuthenticated == true)))
            .Produces<AuthStatusDto>();

        return group;
    }
}
