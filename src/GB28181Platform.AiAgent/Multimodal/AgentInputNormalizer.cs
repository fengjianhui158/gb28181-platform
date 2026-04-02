using GB28181Platform.AiAgent.Contracts;

namespace GB28181Platform.AiAgent.Multimodal;

public static class AgentInputNormalizer
{
    public static NormalizedAgentInput Normalize(AgentChatRequest request)
    {
        return new NormalizedAgentInput
        {
            ConversationId = string.IsNullOrWhiteSpace(request.ConversationId)
                ? Guid.NewGuid().ToString("N")
                : request.ConversationId,
            DeviceId = request.DeviceId,
            Items = request.ContentItems.Select(item => new NormalizedAgentInputItem
            {
                Kind = item.Kind,
                Text = item.Text,
                FileName = item.FileName,
                MediaType = item.MediaType,
                Base64Data = item.Base64Data
            }).ToList()
        };
    }
}
