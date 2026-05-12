using Chronos.Agent.Extraction;

namespace Chronos.Tests.Agent;

public class ConstraintValueValidatorTests
{
    // --- Weekday keys ---

    [Theory]
    [InlineData("unavailable_day")]
    [InlineData("avoid_weekday")]
    [InlineData("preferred_weekday")]
    public void Weekday_AcceptsValidNames(string key)
    {
        Assert.Null(ConstraintValueValidator.Validate(key, "Monday"));
        Assert.Null(ConstraintValueValidator.Validate(key, "friday"));   // case-insensitive
        Assert.Null(ConstraintValueValidator.Validate(key, "  Sunday "));// trims whitespace
    }

    [Theory]
    [InlineData("unavailable_day", "Funday")]
    [InlineData("avoid_weekday", "tomorrow")]
    [InlineData("preferred_weekday", "Mon")]   // abbreviations rejected
    public void Weekday_RejectsInvalidNames(string key, string value)
    {
        var error = ConstraintValueValidator.Validate(key, value);
        Assert.NotNull(error);
        Assert.Contains("weekday", error, StringComparison.OrdinalIgnoreCase);
    }

    // --- Multiple weekdays ---

    [Fact]
    public void WeekdayList_AcceptsCommaSeparated()
    {
        Assert.Null(ConstraintValueValidator.Validate("preferred_weekdays", "Monday,Wednesday,Friday"));
        Assert.Null(ConstraintValueValidator.Validate("preferred_weekdays", "Monday, Tuesday"));
    }

    [Fact]
    public void WeekdayList_RejectsMixedInvalid()
    {
        var error = ConstraintValueValidator.Validate("preferred_weekdays", "Monday,Funday");
        Assert.NotNull(error);
        Assert.Contains("Funday", error);
    }

    // --- Booleans ---

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

    // --- Time ranges ---

    [Fact]
    public void TimeRange_AcceptsValidEntries()
    {
        Assert.Null(ConstraintValueValidator.Validate("preferred_timerange", "Monday 09:00 - 11:00"));
        Assert.Null(ConstraintValueValidator.Validate(
            "preferred_timerange",
            "Monday 09:00 - 11:00, Tuesday 13:00 - 15:30"));
    }

    [Theory]
    [InlineData("Monday 9 to 11")]              // missing minutes / wrong separator
    [InlineData("Funday 09:00 - 11:00")]        // bad weekday
    [InlineData("Monday 25:00 - 26:00")]        // out-of-range hour
    [InlineData("Monday 09:60 - 10:00")]        // out-of-range minute
    public void TimeRange_RejectsMalformedEntries(string value)
    {
        Assert.NotNull(ConstraintValueValidator.Validate("preferred_timerange", value));
    }

    // --- Edge cases ---

    [Fact]
    public void EmptyValue_AlwaysRejected()
    {
        Assert.NotNull(ConstraintValueValidator.Validate("avoid_weekday", ""));
        Assert.NotNull(ConstraintValueValidator.Validate("avoid_weekday", "   "));
    }

    [Fact]
    public void UnknownKey_Rejected()
    {
        var error = ConstraintValueValidator.Validate("made_up_key", "anything");
        Assert.NotNull(error);
        Assert.Contains("Unknown key", error);
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
