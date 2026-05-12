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
            hardConstraints = new[]
            {
                new { key = "forbidden_timerange", value = "Friday 09:00 - 17:00" }
            },
            softPreferences = new[]
            {
                new { key = "preferred_weekday", value = "Monday" }
            }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var messages = new List<ChatMessage>
        {
            new("user", "I can't work Fridays and prefer Mondays")
        };

        var result = await extractor.ExtractAsync(messages);

        Assert.Single(result.Draft.HardConstraints);
        Assert.Equal("forbidden_timerange", result.Draft.HardConstraints[0].Key);
        Assert.Equal("Friday 09:00 - 17:00", result.Draft.HardConstraints[0].Value);
        Assert.Single(result.Draft.SoftPreferences);
        Assert.Equal("preferred_weekday", result.Draft.SoftPreferences[0].Key);
        Assert.Equal("Monday", result.Draft.SoftPreferences[0].Value);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExtractAsync_MultipleSoftItems_ParsedCorrectly()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "forbidden_timerange", value = "Friday 09:00 - 17:00, Saturday 10:00 - 14:00" }
            },
            softPreferences = new[]
            {
                new { key = "preferred_weekdays", value = "Monday,Wednesday" },
                new { key = "preferred_time_morning", value = "true" },
                new { key = "avoid_weekday", value = "Tuesday" }
            }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Single(result.Draft.HardConstraints);
        Assert.Equal(3, result.Draft.SoftPreferences.Count);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExtractAsync_FiltersOutInvalidKeys_AndReportsIssues()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "forbidden_timerange", value = "Friday 09:00 - 17:00" },
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
        Assert.Equal("forbidden_timerange", result.Draft.HardConstraints[0].Key);
        Assert.Single(result.Draft.SoftPreferences);
        Assert.Equal("preferred_weekday", result.Draft.SoftPreferences[0].Key);

        // Issues should surface — no silent dropping
        Assert.Equal(2, result.Issues.Count);
        Assert.Contains(result.Issues, i => i.Key == "made_up_key" && i.Kind == "hardConstraint");
        Assert.Contains(result.Issues, i => i.Key == "bogus_pref" && i.Kind == "softPreference");
    }

    [Theory]
    // Each of these keys was either previously hallucinated or only applies to
    // ActivityConstraint flow; they must be rejected as hard UserConstraint keys.
    [InlineData("unavailable_day", "Friday")]
    [InlineData("time_range", "{\"start\":\"08:00\",\"end\":\"17:00\"}")]
    [InlineData("required_capacity", "{\"min\":20}")]
    [InlineData("compatible_resource_types", "Lecture Hall")]
    [InlineData("location_preference", "Building A")]
    public async Task ExtractAsync_RejectsNonUserConstraintHardKeys(string key, string value)
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key, value },
                new { key = "forbidden_timerange", value = "Friday 09:00 - 17:00" }
            },
            softPreferences = Array.Empty<object>()
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        // Only forbidden_timerange survives; the other hard key is reported as an issue.
        Assert.Single(result.Draft.HardConstraints);
        Assert.Equal("forbidden_timerange", result.Draft.HardConstraints[0].Key);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(key, issue.Key);
        Assert.Contains("Unknown", issue.Reason);
    }

    [Fact]
    public async Task ExtractAsync_RejectsInvalidWeekdayValue_AndReportsIssue()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = Array.Empty<object>(),
            softPreferences = new[]
            {
                new { key = "preferred_weekday", value = "Funday" },
                new { key = "avoid_weekday", value = "Saturday" }
            }
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Single(result.Draft.SoftPreferences);
        Assert.Equal("Saturday", result.Draft.SoftPreferences[0].Value);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("preferred_weekday", issue.Key);
        Assert.Equal("Funday", issue.Value);
        Assert.Contains("weekday", issue.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_RejectsInvalidTimeRange_AndReportsIssue()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "forbidden_timerange", value = "Monday 9 to 11" }
            },
            softPreferences = Array.Empty<object>()
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(result.Draft.HardConstraints);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("forbidden_timerange", issue.Key);
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
    public async Task ExtractAsync_OneTimeHardConstraint_ParsesWeekNumIntoDraft()
    {
        // Engine accepts UserConstraint.WeekNum (nullable ISO week) for one-time
        // exceptions like "next Tuesday only" — extraction must preserve it end-to-end.
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "forbidden_timerange", value = "Tuesday 00:00 - 23:59", weekNum = (int?)21 }
            },
            softPreferences = Array.Empty<object>()
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(result.Issues);
        var hard = Assert.Single(result.Draft.HardConstraints);
        Assert.Equal("forbidden_timerange", hard.Key);
        Assert.Equal("Tuesday 00:00 - 23:59", hard.Value);
        Assert.Equal(21, hard.WeekNum);
    }

    [Fact]
    public async Task ExtractAsync_RecurringHardConstraint_HasNullWeekNum()
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "forbidden_timerange", value = "Friday 09:00 - 17:00", weekNum = (int?)null }
            },
            softPreferences = Array.Empty<object>()
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(result.Issues);
        var hard = Assert.Single(result.Draft.HardConstraints);
        Assert.Null(hard.WeekNum);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(54)]
    [InlineData(-3)]
    public async Task ExtractAsync_OutOfRangeWeekNum_ReportedAsIssue(int weekNum)
    {
        var json = JsonSerializer.Serialize(new
        {
            hardConstraints = new[]
            {
                new { key = "forbidden_timerange", value = "Tuesday 00:00 - 23:59", weekNum }
            },
            softPreferences = Array.Empty<object>()
        });
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        var result = await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.Empty(result.Draft.HardConstraints);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("hardConstraint", issue.Kind);
        Assert.Contains("weekNum", issue.Reason);
    }

    [Fact]
    public async Task ExtractAsync_SchemaIncludesWeekNumOnHardConstraintItems()
    {
        var json = """{"hardConstraints":[],"softPreferences":[]}""";
        var adapter = CreateMockAdapter(json);
        var extractor = new ConstraintExtractor(adapter.Object);

        LlmOptions? capturedOptions = null;
        adapter
            .Setup(a => a.ChatAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<LlmOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ChatMessage>, LlmOptions?, CancellationToken>((_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(new LlmResponse(json));

        await extractor.ExtractAsync(new List<ChatMessage> { new("user", "test") });

        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions!.JsonSchema);
        var hardItemProps = capturedOptions.JsonSchema!.Value
            .GetProperty("properties")
            .GetProperty("hardConstraints")
            .GetProperty("items")
            .GetProperty("properties");
        Assert.True(hardItemProps.TryGetProperty("weekNum", out _),
            "Hard constraint items in extraction schema must declare weekNum.");
    }
}
