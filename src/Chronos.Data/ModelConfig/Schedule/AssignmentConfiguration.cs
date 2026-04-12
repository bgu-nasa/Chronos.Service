using Chronos.Data.Utils;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Data.ModelConfig.Schedule;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable(ConfigUtils.ToTableName(nameof(Assignment)));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.SlotId)
            .IsRequired();

        builder.Property(a => a.ResourceId)
            .IsRequired();

        builder.Property(a => a.ActivityId)
            .IsRequired();

        builder.Property(a => a.WeekNum)
            .IsRequired(false);

        builder.HasIndex(a => new { a.SlotId, a.ResourceId, a.WeekNum })
            .IsUnique();
    }
}

