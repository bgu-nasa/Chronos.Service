using Chronos.Data.Utils;
using Chronos.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Chronos.Data.ModelConfig.Resources;
public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable(ConfigUtils.ToTableName(nameof(Activity)));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.OrganizationId)
            .IsRequired();

        builder.Property(a => a.SubjectId)
            .IsRequired();
        builder.Property(a => a.AssignedUserId)
            .IsRequired();
        builder.Property(a => a.ActivityType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.ExpectedStudents);

        builder.Property(a => a.Duration)
            .IsRequired();
    }
}