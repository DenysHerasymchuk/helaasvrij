using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternshipCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStageAndLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extracted items count straight away now, so items waiting for confirmation are confirmed.
            migrationBuilder.Sql("UPDATE coach.profile_items SET confirmed = true WHERE confirmed = false;");

            migrationBuilder.DropTable(
                name: "profile_basics",
                schema: "coach");

            migrationBuilder.DropColumn(
                name: "level",
                schema: "coach",
                table: "profile_items");

            migrationBuilder.DropColumn(
                name: "stage_end",
                schema: "coach",
                table: "postings");

            migrationBuilder.DropColumn(
                name: "stage_start",
                schema: "coach",
                table: "postings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "level",
                schema: "coach",
                table: "profile_items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "stage_end",
                schema: "coach",
                table: "postings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "stage_start",
                schema: "coach",
                table: "postings",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "profile_basics",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stage_end = table.Column<DateOnly>(type: "date", nullable: true),
                    stage_start = table.Column<DateOnly>(type: "date", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_basics", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_basics_user_id",
                schema: "coach",
                table: "profile_basics",
                column: "user_id",
                unique: true);
        }
    }
}
