using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekNumToConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeekNum",
                table: "activity_constraints",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeekNum",
                table: "user_constraints",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeekNum",
                table: "activity_constraints");

            migrationBuilder.DropColumn(
                name: "WeekNum",
                table: "user_constraints");
        }
    }
}
