using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternshipCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintsAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "number",
                schema: "coach",
                table: "postings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "sprint_id",
                schema: "coach",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "application_events",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    from_stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    to_stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_events_applications_application_id",
                        column: x => x.application_id,
                        principalSchema: "coach",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprints",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    goal = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprints", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_sprint_id",
                schema: "coach",
                table: "applications",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_events_application_id_at",
                schema: "coach",
                table: "application_events",
                columns: new[] { "application_id", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_application_events_sprint_id",
                schema: "coach",
                table: "application_events",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "IX_sprints_user_id_created_at",
                schema: "coach",
                table: "sprints",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_sprints_one_active_per_user",
                schema: "coach",
                table: "sprints",
                column: "user_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.AddForeignKey(
                name: "FK_applications_sprints_sprint_id",
                schema: "coach",
                table: "applications",
                column: "sprint_id",
                principalSchema: "coach",
                principalTable: "sprints",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Existing positions get a running number per user, oldest first.
            migrationBuilder.Sql(@"UPDATE coach.postings p SET number = r.rn
                FROM (SELECT id, row_number() OVER (PARTITION BY user_id ORDER BY created_at) AS rn FROM coach.postings) r
                WHERE p.id = r.id;");

            // Existing cards get the history they already imply: added, then one move to the current stage.
            migrationBuilder.Sql(@"INSERT INTO coach.application_events (id, application_id, type, from_stage, to_stage, sprint_id, at)
                SELECT gen_random_uuid(), id, 'Added', NULL, 'Saved', NULL, created_at FROM coach.applications;");
            migrationBuilder.Sql(@"INSERT INTO coach.application_events (id, application_id, type, from_stage, to_stage, sprint_id, at)
                SELECT gen_random_uuid(), id, 'StageChanged', 'Saved', stage, NULL, stage_changed_at
                FROM coach.applications WHERE stage <> 'Saved';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_applications_sprints_sprint_id",
                schema: "coach",
                table: "applications");

            migrationBuilder.DropTable(
                name: "application_events",
                schema: "coach");

            migrationBuilder.DropTable(
                name: "sprints",
                schema: "coach");

            migrationBuilder.DropIndex(
                name: "IX_applications_sprint_id",
                schema: "coach",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "number",
                schema: "coach",
                table: "postings");

            migrationBuilder.DropColumn(
                name: "sprint_id",
                schema: "coach",
                table: "applications");
        }
    }
}
