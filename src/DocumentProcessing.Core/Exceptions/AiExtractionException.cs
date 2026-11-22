namespace DocumentProcessing.Core.Exceptions;

/// <summary>
/// Thrown when an AI extraction provider fails during the extraction call.
/// Wraps the underlying provider-specific exception to give callers a uniform
/// catch target regardless of which provider is active.
/// </summary>
public class AiExtractionException(string message, Exception innerException)
    : Exception(message, innerException);
