using Chronos.Agent.Extraction;

namespace Chronos.Tests.Agent;

public class PromptTemplatesTests
{
    // --- Conversation prompt ---

    [Fact]
    public void ConversationSystemPrompt_ContainsSchedulingContext()
    {
        var prompt = PromptTemplates.ConversationSystemPrompt;

        Assert.Contains("scheduling", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("constraint", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preference", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversationSystemPrompt_MentionsApproval()
    {
        var prompt = PromptTemplates.ConversationSystemPrompt;

        Assert.Contains("approv", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // --- Extraction prompt ---

    [Fact]
    public void ExtractionSystemPrompt_ContainsJsonInstruction()
    {
        var prompt = PromptTemplates.ExtractionSystemPrompt;

        Assert.Contains("JSON", prompt);
        Assert.Contains("hardConstraints", prompt);
        Assert.Contains("softPreferences", prompt);
    }

    [Fact]
    public void ExtractionSystemPrompt_ListsAllKnownKeys()
    {
        var prompt = PromptTemplates.ExtractionSystemPrompt;

        foreach (var key in KnownConstraintKeys.AllKeys)
        {
            Assert.Contains(key, prompt);
        }
    }

    // --- Known keys ---

    [Fact]
    public void KnownConstraintKeys_ContainsExpectedHardKeys()
    {
        Assert.Contains("unavailable_day", KnownConstraintKeys.HardConstraintKeys);
        Assert.Contains("avoid_weekday", KnownConstraintKeys.HardConstraintKeys);
    }

    [Fact]
    public void KnownConstraintKeys_ContainsExpectedSoftKeys()
    {
        Assert.Contains("preferred_weekday", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_weekdays", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_time_morning", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_time_afternoon", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_time_evening", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_timerange", KnownConstraintKeys.SoftPreferenceKeys);
    }

    [Fact]
    public void AllKeys_IsCombinationOfHardAndSoft()
    {
        var combined = KnownConstraintKeys.HardConstraintKeys
            .Concat(KnownConstraintKeys.SoftPreferenceKeys)
            .ToHashSet();

        Assert.Equal(combined, KnownConstraintKeys.AllKeys);
    }

    [Fact]
    public void IsValidKey_ReturnsTrueForKnownKey()
    {
        Assert.True(KnownConstraintKeys.IsValid("preferred_weekday"));
        Assert.True(KnownConstraintKeys.IsValid("avoid_weekday"));
    }

    [Fact]
    public void IsValidKey_ReturnsFalseForUnknownKey()
    {
        Assert.False(KnownConstraintKeys.IsValid("made_up_key"));
        Assert.False(KnownConstraintKeys.IsValid(""));
    }
}
