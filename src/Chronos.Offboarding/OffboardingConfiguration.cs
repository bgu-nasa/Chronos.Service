namespace Chronos.Offboarding;

public class OffboardingConfiguration
{
    public const string Offboarding = nameof(Offboarding);
    public string CronSchedule { get; set; }
    public int RetentionDays { get; set; }
}
