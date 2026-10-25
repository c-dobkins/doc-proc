using DocumentProcessing.Api.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Tests;

public class ApiKeyValidationTests
{
    [Theory]
    [InlineData("key-one-0123456789abcdefgh", true)]
    [InlineData("key-two-0123456789abcdefgh", true)]
    [InlineData("key-one-0123456789abcdefg", false)]   // prefix of a valid key
    [InlineData("", false)]
    public void IsValidKey_ComparesExactly(string provided, bool expected)
    {
        string[] keys = ["key-one-0123456789abcdefgh", "key-two-0123456789abcdefgh"];

        Assert.Equal(expected, ApiKeyAuthenticationHandler.IsValidKey(provided, keys));
    }

    [Fact]
    public void IsValidKey_NoKeysConfigured_RejectsEverything()
    {
        Assert.False(ApiKeyAuthenticationHandler.IsValidKey("anything-0123456789abcdef", []));
    }
}

public class ApiKeyOptionsValidationTests
{
    [Fact]
    public void AddApiKeyAuthentication_KeyTooShort_FailsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:ApiKeys:0"] = "too-short" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiKeyAuthentication(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ApiKeyOptions>>().Value);
    }
}
