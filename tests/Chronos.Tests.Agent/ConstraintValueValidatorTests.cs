using Chronos.Agent.Extraction;

namespace Chronos.Tests.Agent;

public class ConstraintValueValidatorTests
{
    // --- forbidden_timerange / preferred_timerange (the two time-range keys) ---

    [Theory]
    [InlineData("forbidden_timerange", "Monday 09:30 - 11:00")]
    [InlineData("forbidden_timerange", "Monday 09:30 - 11:00, Wednesday 13:00 - 15:00")]
    [InlineData("preferred_timerange", "Tuesday 13:00 - 15:30")]
    [InlineData("preferred_timerange", "Monday 09:00 - 11:00\nFriday 14:00 - 16:00")]   // newline-separated
    public void TimeRangeList_AcceptsValid(string key, string value)
    {
        Assert.Null(ConstraintValueValidator.Validate(key, value));
    }

    [Theory]
    [InlineData("forbidden_timerange", "Monday 9 to 11")]
    [InlineData("forbidden_timerange", "Funday 09:00 - 11:00")]
    [InlineData("preferred_timerange", "Monday 25:00 - 26:00")]
    [InlineData("preferred_timerange", "Monday 11:00 - 09:00")]    // start >= end
    public void TimeRangeList_RejectsMalformed(string key, string value)
    {
        Assert.NotNull(ConstraintValueValidator.Validate(key, value));
    }

    // --- preferred_weekdays (CSV weekdays) ---

    [Fact]
    public void PreferredWeekdays_AcceptsCommaSeparated()
    {
        Assert.Null(ConstraintValueValidator.Validate("preferred_weekdays", "Monday,Wednesday,Friday"));
        Assert.Null(ConstraintValueValidator.Validate("preferred_weekdays", "Monday, Tuesday"));
    }

    [Fact]
    public void PreferredWeekdays_RejectsMixedInvalid()
    {
        var error = ConstraintValueValidator.Validate("preferred_weekdays", "Monday,Funday");
        Assert.NotNull(error);
        Assert.Contains("Funday", error);
    }

    // --- Single-weekday keys (preferred_weekday, avoid_weekday) ---

    [Theory]
    [InlineData("preferred_weekday")]
    [InlineData("avoid_weekday")]
    public void SingleWeekday_AcceptsValidNames(string key)
    {
        Assert.Null(ConstraintValueValidator.Validate(key, "Monday"));
        Assert.Null(ConstraintValueValidator.Validate(key, "friday"));   // case-insensitive
        Assert.Null(ConstraintValueValidator.Validate(key, "  Sunday "));// trims whitespace
    }

    [Theory]
    [InlineData("preferred_weekday", "Funday")]
    [InlineData("avoid_weekday", "tomorrow")]
    [InlineData("preferred_weekday", "Mon")]   // abbreviations rejected
    public void SingleWeekday_RejectsInvalidNames(string key, string value)
    {
        var error = ConstraintValueValidator.Validate(key, value);
        Assert.NotNull(error);
        Assert.Contains("weekday", error, StringComparison.OrdinalIgnoreCase);
    }

    // --- Boolean keys (preferred_time_morning/afternoon/evening) ---

    [Theory]
    [InlineData("preferred_time_morning")]
    [InlineData("preferred_time_afternoon")]
    [InlineData("preferred_time_evening")]
    public void Boolean_AcceptsTrueAndFalse(string key)
    {
        Assert.Null(ConstraintValueValidator.Validate(key, "true"));
        Assert.Null(ConstraintValueValidator.Validate(key, "FALSE"));
    }

    [Fact]
    public void Boolean_RejectsNonBoolean()
    {
        var error = ConstraintValueValidator.Validate("preferred_time_morning", "yes please");
        Assert.NotNull(error);
        Assert.Contains("boolean", error, StringComparison.OrdinalIgnoreCase);
    }

    // --- Edge cases ---

    [Fact]
    public void EmptyValue_AlwaysRejected()
    {
        Assert.NotNull(ConstraintValueValidator.Validate("preferred_weekday", ""));
        Assert.NotNull(ConstraintValueValidator.Validate("forbidden_timerange", "   "));
    }

    [Theory]
    // Regression: each of these was either invented out of thin air or only applies to
    // ActivityConstraint flow — the agent must never produce them as user constraints.
    [InlineData("unavailable_day")]
    [InlineData("time_range")]
    [InlineData("required_capacity")]
    [InlineData("compatible_resource_types")]
    [InlineData("location_preference")]
    [InlineData("made_up_key")]
    public void UnknownKeys_AreRejected(string key)
    {
        var error = ConstraintValueValidator.Validate(key, "anything");
        Assert.NotNull(error);
        Assert.Contains("Unknown key", error);
        Assert.False(KnownConstraintKeys.IsValid(key));
    }
}

public class ConstraintExtractionSchemaTests
{
    [Fact]
    public void Build_HasTopLevelHardAndSoftArrays()
    {
        var schema = ConstraintExtractionSchema.Build();

        Assert.Equal("object", schema.GetProperty("type").GetString());
        var props = schema.GetProperty("properties");
        Assert.Equal("array", props.GetProperty("hardConstraints").GetProperty("type").GetString());
        Assert.Equal("array", props.GetProperty("softPreferences").GetProperty("type").GetString());
    }

    [Fact]
    public void Build_RestrictsHardKeysToCatalog()
    {
        var schema = ConstraintExtractionSchema.Build();
        var keyEnum = schema
            .GetProperty("properties")
            .GetProperty("hardConstraints")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("key")
            .GetProperty("enum");

        var keys = keyEnum.EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Equal(KnownConstraintKeys.HardConstraintKeys.ToHashSet(), keys);
        Assert.Single(keys);
        Assert.Contains("forbidden_timerange", keys);
    }

    [Fact]
    public void Build_RestrictsSoftKeysToCatalog()
    {
        var schema = ConstraintExtractionSchema.Build();
        var keyEnum = schema
            .GetProperty("properties")
            .GetProperty("softPreferences")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("key")
            .GetProperty("enum");

        var keys = keyEnum.EnumerateArray().Select(e => e.GetString()).ToHashSet();
        Assert.Equal(KnownConstraintKeys.SoftPreferenceKeys.ToHashSet(), keys);
    }
}
