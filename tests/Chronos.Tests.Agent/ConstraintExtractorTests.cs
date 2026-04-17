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

        var draft = await extractor.ExtractAsync(messages);

        Assert.Single(draft.HardConstraints);
        Assert.Equal("avoid_weekday", draft.HardConstraints[0].Key);
        Assert.Equal("Friday", draft.HardConstraints[0].Value);
        Assert.Single(draft.SoftPreferences);
        Assert.Equal("preferred_weekday", draft.SoftPreferences[0].Key);
        Assert.Equal("Monday", draft.SoftPreferences[0].Value);
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

        var draft = await extractor.ExtractAsync(new List<ChatMessage>
        {
            new("user", "test")
        });

        Assert.Equal(2, draft.HardConstraints.Count);
        Assert.Equal(2, draft.SoftPreferences.Count);
    }

    [Fact]
    public async Task ExtractAsync_FiltersOutInvalidKeys()
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

        var draft = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Single(draft.HardConstraints);
        Assert.Equal("avoid_weekday", draft.HardConstraints[0].Key);
        Assert.Single(draft.SoftPreferences);
        Assert.Equal("preferred_weekday", draft.SoftPreferences[0].Key);
    }

    [Fact]
    public async Task ExtractAsync_EmptyArrays_ReturnsEmptyDraft()
    {
        var json = """{"hardConstraints":[],"softPreferences":[]}""";
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var draft = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(draft.HardConstraints);
        Assert.Empty(draft.SoftPreferences);
    }

    [Fact]
    public async Task ExtractAsync_SendsExtractionPromptAsSystemMessage()
    {
        var json = """{"hardConstraints":[],"softPreferences":[]}""";
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        adapter.Verify(a => a.ChatAsync(
            It.Is<IReadOnlyList<ChatMessage>>(msgs =>
                msgs[0].Role == "system" &&
                msgs[0].Content.Contains("hardConstraints")),
            It.Is<LlmOptions?>(o => o != null && o.JsonMode),
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
