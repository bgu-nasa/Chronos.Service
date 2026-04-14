using Chronos.Agent.Conversation;

namespace Chronos.Agent.Extraction;

public sealed class LlmConstraintExtractor(
    ILlmAdapter llmAdapter,
    RuleBasedConstraintExtractor fallbackExtractor) : IConstraintExtractor
{
    public async Task<ExtractedConstraintSet> ExtractAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            var llmResult = await llmAdapter.ExtractAsync(messages, cancellationToken);
            if (llmResult.HardConstraints.Count > 0 || llmResult.SoftPreferences.Count > 0)
            {
                return llmResult;
            }
        }
        catch
        {
        }

        return await fallbackExtractor.ExtractAsync(messages, cancellationToken);
    }
}
