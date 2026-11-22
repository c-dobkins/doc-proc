using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Api.Auth;

/// <summary>
/// Access control for a public demo: reads stay anonymous, while anything that changes data or
/// triggers OCR/AI processing requires an API key in the <c>X-Api-Key</c> header.
/// </summary>
public class ApiKeyOptions
{
    public const string SectionName = "Auth";
    public const int MinimumKeyLength = 24;

    /// <summary>
    /// When true (the default), write endpoints require a valid key. With no keys configured they
    /// are closed entirely (fail closed). Development turns this off.
    /// </summary>
    public bool RequireApiKeyForWrites { get; set; } = true;

    /// <summary>Accepted keys. Supply via environment variables or user secrets, never in committed config.</summary>
    public List<string> ApiKeys { get; set; } = [];
}

public static class AuthPolicies
{
    public const string Write = "write";
    public const string ApiKeyScheme = "ApiKey";
    public const string ApiKeyHeader = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    IOptionsMonitor<ApiKeyOptions> apiKeyOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthPolicies.ApiKeyHeader, out var provided) || string.IsNullOrEmpty(provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!IsValidKey(provided.ToString(), apiKeyOptions.CurrentValue.ApiKeys))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "api-key")], AuthPolicies.ApiKeyScheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            title = "An access key is required for this action.",
            status = StatusCodes.Status401Unauthorized
        }, options: null, contentType: "application/problem+json");
    }

    /// <summary>Constant-time comparison (over SHA-256 digests, so key length isn't leaked either).</summary>
    internal static bool IsValidKey(string provided, IEnumerable<string> validKeys)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var match = false;

        foreach (var key in validKeys)
            match |= CryptographicOperations.FixedTimeEquals(providedHash, SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        return match;
    }
}

public static class ApiKeyAuthenticationExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .Validate(o => o.ApiKeys.All(k => k.Length >= ApiKeyOptions.MinimumKeyLength),
                $"Every Auth:ApiKeys entry must be at least {ApiKeyOptions.MinimumKeyLength} characters.")
            .ValidateOnStart();

        services.AddAuthentication(AuthPolicies.ApiKeyScheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthPolicies.ApiKeyScheme, null);

        var requireKey = configuration.GetValue($"{ApiKeyOptions.SectionName}:{nameof(ApiKeyOptions.RequireApiKeyForWrites)}", true);

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.Write, policy =>
            {
                if (requireKey)
                    policy.RequireAuthenticatedUser();
                else
                    policy.RequireAssertion(_ => true);
            });

        return services;
    }
}
