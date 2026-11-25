using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternshipCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_skills",
                schema: "coach",
                table: "user_settings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "humanizer");

            migrationBuilder.AddColumn<string>(
                name: "interview_skills",
                schema: "coach",
                table: "user_settings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "humanizer,teacher");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_skills",
                schema: "coach",
                table: "user_settings");

            migrationBuilder.DropColumn(
                name: "interview_skills",
                schema: "coach",
                table: "user_settings");
        }
    }
}
