using Chronos.Agent.Extraction;

namespace Chronos.Tests.Agent;

public class PromptTemplatesTests
{
    // --- Conversation prompt ---

    [Fact]
    public void ConversationSystemPrompt_ContainsSchedulingContext()
    {
        var prompt = PromptTemplates.BuildConversationSystemPrompt(
            DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("scheduling", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("constraint", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preference", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversationSystemPrompt_MentionsApproval()
    {
        var prompt = PromptTemplates.BuildConversationSystemPrompt(
            DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("approv", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversationSystemPrompt_IncludesProvidedDateAndDay_InUtc()
    {
        // Fixed reference date so the assertion isn't time-dependent.
        var reference = new DateTimeOffset(2026, 5, 12, 14, 43, 16, TimeSpan.Zero);

        var prompt = PromptTemplates.BuildConversationSystemPrompt(reference, TimeZoneInfo.Utc);

        Assert.Contains("2026-05-12", prompt);
        Assert.Contains("Tuesday", prompt);
        Assert.Contains("UTC", prompt);
    }

    [Fact]
    public void ConversationSystemPrompt_ConvertsTimeIntoProvidedTimezone()
    {
        // 2026-05-12 23:30 UTC → 2026-05-13 02:30 in Asia/Jerusalem (UTC+3, DST in May).
        // We assert on the local date/day so the LLM gets the user-local calendar,
        // not UTC.
        var reference = new DateTimeOffset(2026, 5, 12, 23, 30, 0, TimeSpan.Zero);
        var jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

        var prompt = PromptTemplates.BuildConversationSystemPrompt(reference, jerusalem);

        Assert.Contains("2026-05-13", prompt);
        Assert.Contains("Wednesday", prompt);
        Assert.Contains("Asia/Jerusalem", prompt);
        Assert.DoesNotContain("2026-05-12", prompt);
    }

    [Fact]
    public void ConversationSystemPrompt_DistinguishesRecurringFromOneTime()
    {
        var prompt = PromptTemplates.BuildConversationSystemPrompt(
            DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.Contains("recurring", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one-time", prompt, StringComparison.OrdinalIgnoreCase);
        // Must mention the weekNum mechanism so the agent knows one-time IS supported.
        Assert.Contains("weekNum", prompt);
        Assert.Contains("ISO week", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversationSystemPrompt_DoesNotClaimOneTimeIsUnsupported()
    {
        // Regression: a previous version of this prompt told the LLM the engine
        // "cannot store a single-date exception", which is false — UserConstraint.WeekNum
        // is exactly that. The prompt must not lie about it.
        var prompt = PromptTemplates.BuildConversationSystemPrompt(
            DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.DoesNotContain("cannot store", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no concept of a one-time", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // --- Extraction prompt ---

    [Fact]
    public void ExtractionSystemPrompt_DocumentsWeekNumField()
    {
        var prompt = PromptTemplates.ExtractionSystemPrompt;

        Assert.Contains("weekNum", prompt);
        Assert.Contains("ISO week", prompt, StringComparison.OrdinalIgnoreCase);
    }

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
        // The agent today supports exactly one hard UserConstraint key — the only one
        // ActivityConstraintProcessor handles for user-scoped constraints.
        Assert.Contains("forbidden_timerange", KnownConstraintKeys.HardConstraintKeys);
        Assert.Single(KnownConstraintKeys.HardConstraintKeys);
    }

    [Fact]
    public void KnownConstraintKeys_ContainsExpectedSoftKeys()
    {
        Assert.Contains("preferred_weekday", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_weekdays", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("avoid_weekday", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_time_morning", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_time_afternoon", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_time_evening", KnownConstraintKeys.SoftPreferenceKeys);
        Assert.Contains("preferred_timerange", KnownConstraintKeys.SoftPreferenceKeys);
    }

    [Fact]
    public void KnownConstraintKeys_DoesNotContainHallucinatedKeys()
    {
        // Regression: these keys never existed in the engine and must never be re-added.
        Assert.False(KnownConstraintKeys.IsValid("unavailable_day"));
        Assert.False(KnownConstraintKeys.IsValid("made_up_key"));
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
        Assert.True(KnownConstraintKeys.IsValid("forbidden_timerange"));
        Assert.True(KnownConstraintKeys.IsValid("avoid_weekday"));
    }

    [Fact]
    public void IsValidKey_ReturnsFalseForUnknownKey()
    {
        Assert.False(KnownConstraintKeys.IsValid("made_up_key"));
        Assert.False(KnownConstraintKeys.IsValid(""));
    }
}
