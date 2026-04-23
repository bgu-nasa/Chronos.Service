namespace Chronos.Offboarding;

public class OffboardingConfiguration
{
    public const string Offboarding = nameof(Offboarding);
    public required string CronSchedule { get; set; }
    public required int RetentionDays { get; set; }
}
