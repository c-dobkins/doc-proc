namespace DocumentProcessing.Infrastructure.Ai;

/// <summary>
/// Root configuration for the AI extraction layer.
/// </summary>
public class AiProviderOptions
{
    public const string SectionName = "AiProviders";

    /// <summary>
    /// Name of the default provider to use when none is specified per-template.
    /// </summary>
    public required string DefaultProvider { get; set; }

    /// <summary>
    /// Ordered list of provider names to try when the default provider is unavailable.
    /// </summary>
    public List<string> FallbackOrder { get; set; } = [];

    public AzureOpenAiOptions AzureOpenAi { get; set; } = new();

    public OpenAiOptions OpenAi { get; set; } = new();

    public GeminiOptions Gemini { get; set; } = new();

    public OllamaOptions Ollama { get; set; } = new();
}

public class AzureOpenAiOptions
{
    public bool Enabled { get; set; }

    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    public string DeploymentName { get; set; } = "gpt-4o";
}

public class OpenAiOptions
{
    public bool Enabled { get; set; }

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o";
}

public class GeminiOptions
{
    public bool Enabled { get; set; }

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-2.0-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
}

public class OllamaOptions
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "llama3";
}
