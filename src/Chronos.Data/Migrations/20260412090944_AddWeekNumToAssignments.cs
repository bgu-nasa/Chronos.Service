using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekNumToAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"assignments\" ADD COLUMN IF NOT EXISTS \"WeekNum\" integer NULL;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_assignments_SlotId_ResourceId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_assignments_SlotId_ResourceId_WeekYear_WeekNum\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_assignments_SlotId_ResourceId_WeekNum\";");
            migrationBuilder.Sql("ALTER TABLE \"assignments\" DROP COLUMN IF EXISTS \"WeekYear\";");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_assignments_SlotId_ResourceId_WeekNum\" ON \"assignments\" (\"SlotId\", \"ResourceId\", \"WeekNum\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_assignments_SlotId_ResourceId_WeekNum\";");
            migrationBuilder.Sql("ALTER TABLE \"assignments\" ADD COLUMN IF NOT EXISTS \"WeekYear\" integer NULL;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_assignments_SlotId_ResourceId_WeekYear_WeekNum\" ON \"assignments\" (\"SlotId\", \"ResourceId\", \"WeekYear\", \"WeekNum\");");
        }
    }
}
