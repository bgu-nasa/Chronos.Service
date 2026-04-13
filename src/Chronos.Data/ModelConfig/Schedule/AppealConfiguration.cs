using Chronos.Data.Utils;
using Chronos.Domain.Schedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronos.Data.ModelConfig.Schedule;

public class AppealConfiguration : IEntityTypeConfiguration<Appeal>
{
    public void Configure(EntityTypeBuilder<Appeal> builder)
    {
        builder.ToTable(ConfigUtils.ToTableName(nameof(Appeal)));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AssignmentId)
            .IsRequired();

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Description)
            .IsRequired()
            .HasMaxLength(1023);
    }
}
