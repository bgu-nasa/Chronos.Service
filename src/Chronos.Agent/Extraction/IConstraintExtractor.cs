using Chronos.Agent.Conversation;

namespace Chronos.Agent.Extraction;

public interface IConstraintExtractor
{
    Task<ExtractedConstraintSet> ExtractAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
