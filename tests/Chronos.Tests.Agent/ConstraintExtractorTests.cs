using System.Text.Json;
using Chronos.Agent.Conversation;
using Chronos.Agent.Extraction;
using Moq;

namespace Chronos.Tests.Agent;

public class ConstraintExtractorTests
{
    private static Mock<ILlmAdapter> CreateMockAdapter(string jsonResponse)
    {
        var mock = new Mock<ILlmAdapter>();
        mock.Setup(a => a.ChatAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<LlmOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(jsonResponse));
        return mock;
    }

    [Fact]
    public async Task ExtractAsync_ParsesValidJson_IntoDraft()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[] { new { key = "avoid_weekday", value = "Friday" } },
            softPreferences = new[] { new { key = "preferred_weekday", value = "Monday" } }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var messages = new List<ChatMessage>
        {
            new("user", "I can't work Fridays and prefer Mondays")
        };

        var result = await extractor.ExtractAsync(messages);

        Assert.Single(result.Draft.HardConstraints);
        Assert.Equal("avoid_weekday", result.Draft.HardConstraints[0].Key);
        Assert.Equal("Friday", result.Draft.HardConstraints[0].Value);
        Assert.Single(result.Draft.SoftPreferences);
        Assert.Equal("preferred_weekday", result.Draft.SoftPreferences[0].Key);
        Assert.Equal("Monday", result.Draft.SoftPreferences[0].Value);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExtractAsync_MultipleItems_ParsedCorrectly()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "avoid_weekday", value = "Friday" },
                new { key = "unavailable_day", value = "Saturday" }
            },
            softPreferences = new[]
            {
                new { key = "preferred_weekdays", value = "Monday,Wednesday" },
                new { key = "preferred_time_morning", value = "true" }
            }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage>
        {
            new("user", "test")
        });

        Assert.Equal(2, result.Draft.HardConstraints.Count);
        Assert.Equal(2, result.Draft.SoftPreferences.Count);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExtractAsync_FiltersOutInvalidKeys_AndReportsIssues()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "avoid_weekday", value = "Friday" },
                new { key = "made_up_key", value = "whatever" }
            },
            softPreferences = new[]
            {
                new { key = "preferred_weekday", value = "Monday" },
                new { key = "bogus_pref", value = "nope" }
            }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Single(result.Draft.HardConstraints);
        Assert.Equal("avoid_weekday", result.Draft.HardConstraints[0].Key);
        Assert.Single(result.Draft.SoftPreferences);
        Assert.Equal("preferred_weekday", result.Draft.SoftPreferences[0].Key);

        // Issues should surface — no silent dropping
        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, i => i.Key == "made_up_key" && i.Kind == "hardConstraint");
        Assert.Contains(result.Issues, i => i.Key == "bogus_pref" && i.Kind == "softPreference");
    }

    [Fact]
    public async Task ExtractAsync_RejectsInvalidWeekdayValue_AndReportsIssue()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "avoid_weekday", value = "Funday" },
                new { key = "unavailable_day", value = "Saturday" }
            },
            softPreferences = Array.Empty<object>()
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        // Only the valid Saturday entry survives
        Assert.Single(result.Draft.HardConstraints);
        Assert.Equal("Saturday", result.Draft.HardConstraints[0].Value);

        // The "Funday" entry is reported, not silently dropped
        var issue = Assert.Single(result.Issues);
        Assert.Equal("avoid_weekday", issue.Key);
        Assert.Equal("Funday", issue.Value);
        Assert.Contains("weekday", issue.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_RejectsInvalidTimeRange_AndReportsIssue()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = Array.Empty<object>(),
            softPreferences = new[]
            {
                new { key = "preferred_timerange", value = "Monday 9 to 11" }
            }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(result.Draft.SoftPreferences);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("preferred_timerange", issue.Key);
        Assert.Contains("time range", issue.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_RejectsNonBooleanForBooleanKey()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = Array.Empty<object>(),
            softPreferences = new[]
            {
                new { key = "preferred_time_morning", value = "yes please" }
            }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(result.Draft.SoftPreferences);
        var issue = Assert.Single(result.Issues);
        Assert.Contains("boolean", issue.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_EmptyArrays_ReturnsEmptyDraft()
    {
        var json = """{"hardConstraints":[],"softPreferences":[]}""";
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(result.Draft.HardConstraints);
        Assert.Empty(result.Draft.SoftPreferences);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExtractAsync_SendsExtractionPromptAndSchemaInOptions()
    {
        var json = """{"hardConstraints":[],"softPreferences":[]}""";
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        adapter.Verify(a => a.ChatAsync(
            It.Is<IReadOnlyList<ChatMessage>>(msgs =>
                msgs[0].Role == "system" &&
                msgs[0].Content.Contains("hardConstraints")),
            It.Is<LlmOptions?>(o =>
                o != null
                && o.JsonMode
                && o.JsonSchema != null
                && o.JsonSchema.Value.GetProperty("properties")
                    .GetProperty("hardConstraints").GetProperty("type").GetString() == "array"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractAsync_MalformedJson_ThrowsExtractionException()
    {
        var adapter = CreateMockAdapter("this is not json at all");
        var extractor = new ConstraintExtractor(adapter.Object);

        await Assert.ThrowsAsync<ExtractionException>(
            () => extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") }));
    }
}
