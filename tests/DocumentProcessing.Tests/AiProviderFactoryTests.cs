using DocumentProcessing.Core.Interfaces.Pipeline;
using DocumentProcessing.Infrastructure.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DocumentProcessing.Tests;

public class AiProviderFactoryTests
{
    private static IAiExtractionProvider CreateMockProvider(string name, bool available = true)
    {
        var provider = Substitute.For<IAiExtractionProvider>();
        provider.Name.Returns(name);
        provider.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(available);
        return provider;
    }

    private static AiProviderFactory CreateFactory(
        IEnumerable<IAiExtractionProvider> providers,
        string defaultProvider = "Ollama",
        List<string>? fallbackOrder = null)
    {
        var options = Options.Create(new AiProviderOptions
        {
            DefaultProvider = defaultProvider,
            FallbackOrder = fallbackOrder ?? []
        });
        var logger = Substitute.For<ILogger<AiProviderFactory>>();
        return new AiProviderFactory(providers, options, logger);
    }

    [Fact]
    public async Task GetDefaultProviderAsync_NameIsCaseInsensitive()
    {
        var ollama = CreateMockProvider("Ollama");
        var factory = CreateFactory([ollama], defaultProvider: "ollama");

        var result = await factory.GetDefaultProviderAsync(TestContext.Current.CancellationToken);

        Assert.Same(ollama, result);
    }

    [Fact]
    public async Task GetDefaultProviderAsync_ReturnsDefaultWhenAvailable()
    {
        var ollama = CreateMockProvider("Ollama");
        var openai = CreateMockProvider("OpenAI");
        var factory = CreateFactory([ollama, openai], defaultProvider: "OpenAI");

        var result = await factory.GetDefaultProviderAsync(CancellationToken.None);

        Assert.Same(openai, result);
    }

    [Fact]
    public async Task GetDefaultProviderAsync_DefaultUnavailable_UsesFallback()
    {
        var ollama = CreateMockProvider("Ollama", available: false);
        var openai = CreateMockProvider("OpenAI", available: true);
        var factory = CreateFactory(
            [ollama, openai],
            defaultProvider: "Ollama",
            fallbackOrder: ["OpenAI"]);

        var result = await factory.GetDefaultProviderAsync(CancellationToken.None);

        Assert.Same(openai, result);
    }

    [Fact]
    public async Task GetDefaultProviderAsync_FallbacksInOrder_PicksFirstAvailable()
    {
        var primary = CreateMockProvider("Primary", available: false);
        var second = CreateMockProvider("Second", available: false);
        var third = CreateMockProvider("Third", available: true);
        var factory = CreateFactory(
            [primary, second, third],
            defaultProvider: "Primary",
            fallbackOrder: ["Second", "Third"]);

        var result = await factory.GetDefaultProviderAsync(CancellationToken.None);

        Assert.Same(third, result);
    }

    [Fact]
    public async Task GetDefaultProviderAsync_AllUnavailable_ThrowsInvalidOperationException()
    {
        var ollama = CreateMockProvider("Ollama", available: false);
        var openai = CreateMockProvider("OpenAI", available: false);
        var factory = CreateFactory(
            [ollama, openai],
            defaultProvider: "Ollama",
            fallbackOrder: ["OpenAI"]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.GetDefaultProviderAsync(CancellationToken.None));

        Assert.Contains("No AI provider is available", ex.Message);
    }

    [Fact]
    public async Task GetDefaultProviderAsync_UnknownDefault_SkipsAndUsesFallback()
    {
        var openai = CreateMockProvider("OpenAI", available: true);
        var factory = CreateFactory(
            [openai],
            defaultProvider: "NonExistent",
            fallbackOrder: ["OpenAI"]);

        var result = await factory.GetDefaultProviderAsync(CancellationToken.None);

        Assert.Same(openai, result);
    }
}
