using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternshipCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGapAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "action",
                schema: "coach",
                table: "requirements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "action_project",
                schema: "coach",
                table: "requirements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "analysed_at",
                schema: "coach",
                table: "requirements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "coverage",
                schema: "coach",
                table: "requirements",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<bool>(
                name: "done",
                schema: "coach",
                table: "requirements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                schema: "coach",
                table: "requirements",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_quote",
                schema: "coach",
                table: "requirements",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "evidence_source_document_id",
                schema: "coach",
                table: "requirements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "score_snapshots",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_score_snapshots_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "coach",
                        principalTable: "postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_score_snapshots_posting_id_taken_at",
                schema: "coach",
                table: "score_snapshots",
                columns: new[] { "posting_id", "taken_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_snapshots",
                schema: "coach");

            migrationBuilder.DropColumn(
                name: "action",
                schema: "coach",
                table: "requirements");

            migrationBuilder.DropColumn(
                name: "action_project",
                schema: "coach",
                table: "requirements");

            migrationBuilder.DropColumn(
                name: "analysed_at",
                schema: "coach",
                table: "requirements");

            migrationBuilder.DropColumn(
                name: "coverage",
                schema: "coach",
                table: "requirements");

            migrationBuilder.DropColumn(
                name: "done",
                schema: "coach",
                table: "requirements");

            migrationBuilder.DropColumn(
                name: "due_date",
                schema: "coach",
                table: "requirements");

            migrationBuilder.DropColumn(
                name: "evidence_quote",
                schema: "coach",
                table: "requirements");

            migrationBuilder.DropColumn(
                name: "evidence_source_document_id",
                schema: "coach",
                table: "requirements");
        }
    }
}
