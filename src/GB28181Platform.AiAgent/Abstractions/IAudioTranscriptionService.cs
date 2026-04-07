namespace GB28181Platform.AiAgent.Abstractions;

public interface IAudioTranscriptionService
{
    Task<string> TranscribeAsync(string mediaType, string base64Data, CancellationToken cancellationToken);
}
