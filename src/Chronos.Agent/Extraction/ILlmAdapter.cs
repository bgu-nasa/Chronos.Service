using Chronos.Agent.Conversation;

namespace Chronos.Agent.Extraction;

public interface ILlmAdapter
{
    Task<ExtractedConstraintSet> ExtractAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
