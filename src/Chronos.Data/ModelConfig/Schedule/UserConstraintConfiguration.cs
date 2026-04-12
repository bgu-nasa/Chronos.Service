using Chronos.Data.Utils;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Data.ModelConfig.Schedule;

public class UserConstraintConfiguration : IEntityTypeConfiguration<UserConstraint>
{
    public void Configure(EntityTypeBuilder<UserConstraint> builder)
    {
        builder.ToTable(ConfigUtils.ToTableName(nameof(UserConstraint)));

        builder.HasKey(uc => uc.Id);

        builder.Property(uc => uc.UserId)
            .IsRequired();

        builder.Property(uc => uc.OrganizationId)
            .IsRequired();

        builder.Property(uc => uc.SchedulingPeriodId)
            .IsRequired();

        builder.Property(uc => uc.WeekNum)
            .IsRequired(false);

        builder.Property(uc => uc.Key)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(uc => uc.Value)
            .IsRequired()
            .HasMaxLength(1024);
    }
}

