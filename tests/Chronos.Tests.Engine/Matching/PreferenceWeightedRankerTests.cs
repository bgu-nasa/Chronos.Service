using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Resources;
using Chronos.Domain.Schedule;
using Chronos.Engine.Matching;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chronos.Tests.Engine.Matching;

[TestFixture]
[Category("Unit")]
public class PreferenceWeightedRankerTests
{
    private IUserPreferenceRepository _preferenceRepository;
    private PreferenceWeightedRanker _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _periodId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _preferenceRepository = Substitute.For<IUserPreferenceRepository>();
        _sut = new PreferenceWeightedRanker(
            _preferenceRepository,
            Substitute.For<ILogger<PreferenceWeightedRanker>>());
    }

    #region CalculateWeightAsync

    [Test]
    public async Task GivenNoPreferences_WhenCalculateWeight_ThenReturnsNeutral()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>());

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Monday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(1.0));
    }

    [Test]
    public async Task GivenPreferredWeekdayMatch_WhenCalculateWeight_ThenWeightIncreases()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("preferred_weekday", "Monday")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Monday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(3.0));
    }

    [Test]
    public async Task GivenPreferredWeekdayNoMatch_WhenCalculateWeight_ThenWeightStaysNeutral()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("preferred_weekday", "Tuesday")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Monday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(1.0));
    }

    [Test]
    public async Task GivenMorningPreference_WhenMorningSlot_ThenWeightIncreases()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("preferred_time_morning", "true")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Monday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(3.0));
    }

    [Test]
    public async Task GivenMorningPreference_WhenAfternoonSlot_ThenWeightStaysNeutral()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("preferred_time_morning", "true")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Monday", 14, 15), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(1.0));
    }

    [Test]
    public async Task GivenMultipleMatchingPreferences_WhenCalculateWeight_ThenMultipliesWeights()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("preferred_weekday", "Monday"),
                MakePreference("preferred_time_morning", "true")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Monday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(9.0)); // 3.0 * 3.0
    }

    [Test]
    public async Task GivenAvoidWeekdayMatch_WhenCalculateWeight_ThenWeightDecreases()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("avoid_weekday", "Friday")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Friday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(0.3).Within(0.01));
    }

    [Test]
    public async Task GivenPreferredTimeRange_WhenSlotFitsRange_ThenWeightIncreases()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("preferred_timerange", "Monday 08:00 - 12:00")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Monday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(4.0));
    }

    [Test]
    public async Task GivenPreferredWeekdays_WhenSlotMatchesOne_ThenWeightIncreases()
    {
        _preferenceRepository.GetByUserPeriodAsync(_userId, _periodId)
            .Returns(new List<UserPreference>
            {
                MakePreference("preferred_weekdays", "Monday, Wednesday, Friday")
            });

        var weight = await _sut.CalculateWeightAsync(
            MakeCandidate("Wednesday", 9, 10), _userId, _orgId, _periodId);

        Assert.That(weight, Is.EqualTo(3.0));
    }

    #endregion

    #region SelectRandomWeighted

    [Test]
    public void GivenEmptyCandidates_WhenSelectRandom_ThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _sut.SelectRandomWeighted([], []));
    }

    [Test]
    public void GivenMismatchedLengths_WhenSelectRandom_ThenThrowsArgumentException()
    {
        var candidates = new List<SlotResourcePair> { MakeCandidate("Monday", 9, 10) };

        Assert.Throws<ArgumentException>(() =>
            _sut.SelectRandomWeighted(candidates, [1.0, 2.0]));
    }

    [Test]
    public void GivenSingleCandidate_WhenSelectRandom_ThenReturnsThatCandidate()
    {
        var candidate = MakeCandidate("Monday", 9, 10);

        var result = _sut.SelectRandomWeighted([candidate], [1.0]);

        Assert.That(result, Is.EqualTo(candidate));
    }

    [Test]
    public void GivenMultipleCandidates_WhenSelectRandom_ThenReturnsOneOfThem()
    {
        var c1 = MakeCandidate("Monday", 9, 10);
        var c2 = MakeCandidate("Tuesday", 10, 11);
        var candidates = new List<SlotResourcePair> { c1, c2 };

        var result = _sut.SelectRandomWeighted(candidates, [1.0, 1.0]);

        Assert.That(candidates, Does.Contain(result));
    }

    [Test]
    public void GivenAllZeroWeights_WhenSelectRandom_ThenStillReturnsCandidate()
    {
        var c1 = MakeCandidate("Monday", 9, 10);
        var c2 = MakeCandidate("Tuesday", 10, 11);

        var result = _sut.SelectRandomWeighted([c1, c2], [0.0, 0.0]);

        Assert.That(new[] { c1, c2 }, Does.Contain(result));
    }

    #endregion

    #region Helpers

    private SlotResourcePair MakeCandidate(string weekday, int fromHour, int toHour)
    {
        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            SchedulingPeriodId = _periodId,
            Weekday = weekday,
            FromTime = TimeSpan.FromHours(fromHour),
            ToTime = TimeSpan.FromHours(toHour)
        };
        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            ResourceTypeId = Guid.NewGuid(),
            Location = "Building A",
            Identifier = "Room 1"
        };
        return new SlotResourcePair(slot, resource);
    }

    private UserPreference MakePreference(string key, string value)
    {
        return new UserPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgId,
            UserId = _userId,
            SchedulingPeriodId = _periodId,
            Key = key,
            Value = value
        };
    }

    #endregion
}
