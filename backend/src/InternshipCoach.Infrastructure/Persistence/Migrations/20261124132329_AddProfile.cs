using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternshipCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "profile_basics",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stage_start = table.Column<DateOnly>(type: "date", nullable: true),
                    stage_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_basics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "profile_items",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    quote = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_profile_items_source_documents_source_document_id",
                        column: x => x.source_document_id,
                        principalSchema: "coach",
                        principalTable: "source_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_evidence",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_profile_evidence_profile_items_profile_item_id",
                        column: x => x.profile_item_id,
                        principalSchema: "coach",
                        principalTable: "profile_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_profile_evidence_source_documents_source_document_id",
                        column: x => x.source_document_id,
                        principalSchema: "coach",
                        principalTable: "source_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_profile_basics_user_id",
                schema: "coach",
                table: "profile_basics",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profile_evidence_profile_item_id",
                schema: "coach",
                table: "profile_evidence",
                column: "profile_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_evidence_source_document_id",
                schema: "coach",
                table: "profile_evidence",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_items_source_document_id",
                schema: "coach",
                table: "profile_items",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_profile_items_user_id_kind_normalized_name",
                schema: "coach",
                table: "profile_items",
                columns: new[] { "user_id", "kind", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_basics",
                schema: "coach");

            migrationBuilder.DropTable(
                name: "profile_evidence",
                schema: "coach");

            migrationBuilder.DropTable(
                name: "profile_items",
                schema: "coach");
        }
    }
}
