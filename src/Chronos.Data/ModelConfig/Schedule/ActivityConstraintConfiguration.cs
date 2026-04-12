using Chronos.Data.Utils;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Data.ModelConfig.Schedule;

public class ActivityConstraintConfiguration : IEntityTypeConfiguration<ActivityConstraint>
{
    public void Configure(EntityTypeBuilder<ActivityConstraint> builder)
    {
        builder.ToTable(ConfigUtils.ToTableName(nameof(ActivityConstraint)));

        builder.HasKey(ac => ac.Id);

        builder.Property(ac => ac.ActivityId)
            .IsRequired();

        builder.Property(ac => ac.WeekNum)
            .IsRequired(false);

        builder.Property(ac => ac.Key)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(ac => ac.Value)
            .IsRequired()
            .HasMaxLength(1024);
    }
}

