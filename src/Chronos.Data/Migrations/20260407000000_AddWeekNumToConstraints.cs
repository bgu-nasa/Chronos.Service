using Chronos.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chronos.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260407000000_AddWeekNumToConstraints")]
public partial class AddWeekNumToConstraints : Migration
{
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