using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternshipCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "generic_answer",
                schema: "coach",
                table: "postings",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "generic_answer_at",
                schema: "coach",
                table: "postings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "generic_answer",
                schema: "coach",
                table: "postings");

            migrationBuilder.DropColumn(
                name: "generic_answer_at",
                schema: "coach",
                table: "postings");
        }
    }
}
