using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AudioToText;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GB28181Platform.AiAgent.Capabilities.Application;

public class OpenAiCompatibleAudioTranscriptionService : IAudioTranscriptionService
{
    private readonly SemanticKernelOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OpenAiCompatibleAudioTranscriptionService> _logger;

    public OpenAiCompatibleAudioTranscriptionService(
        SemanticKernelOptions options,
        ILoggerFactory loggerFactory,
        ILogger<OpenAiCompatibleAudioTranscriptionService> logger)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(string mediaType, string base64Data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Audio.BaseUrl) || string.IsNullOrWhiteSpace(_options.Audio.Model))
        {
            _logger.LogWarning("Audio transcription skipped because SemanticKernel:AudioModel is not configured.");
            return "[audio transcription unavailable]";
        }

        var audioBytes = Convert.FromBase64String(base64Data);
        using var httpClient = SemanticKernelClientFactory.CreateHttpClient(_options.Audio);
        IAudioToTextService service = new OpenAIAudioToTextService(
            _options.Audio.Model,
            _options.Audio.ApiKey,
            _options.Audio.BaseUrl,
            httpClient,
            _loggerFactory);

        var fileName = GuessAudioFileName(mediaType);
        var executionSettings = new OpenAIAudioToTextExecutionSettings(fileName);
        var audioContent = new AudioContent(audioBytes, mediaType);
        var textContent = await service.GetTextContentAsync(audioContent, executionSettings, kernel: null, cancellationToken);

        return string.IsNullOrWhiteSpace(textContent?.Text)
            ? "[audio transcription empty]"
            : textContent.Text;
    }

    private static string GuessAudioFileName(string mediaType)
    {
        return mediaType switch
        {
            "audio/wav" => "input.wav",
            "audio/mpeg" => "input.mp3",
            "audio/mp3" => "input.mp3",
            "audio/ogg" => "input.ogg",
            _ => "input.audio"
        };
    }
}
