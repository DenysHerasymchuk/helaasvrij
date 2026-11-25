using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InternshipCoach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_questions",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interview_answers",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    what_was_good = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    what_to_add = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    example_sentence = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_interview_answers_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "coach",
                        principalTable: "postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "interview_questions",
                schema: "coach",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    project = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requirement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_interview_questions_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "coach",
                        principalTable: "postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "coach",
                table: "company_questions",
                columns: new[] { "id", "company", "company_key", "text" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-4000-8000-000000000101"), "Brightlane", "brightlane", "Why do you want to build software for healthcare organisations?" },
                    { new Guid("00000000-0000-4000-8000-000000000102"), "Brightlane", "brightlane", "Tell us about a time a test caught a bug before a user did." },
                    { new Guid("00000000-0000-4000-8000-000000000103"), "Brightlane", "brightlane", "How would you explain a failing deployment to a non-technical colleague?" },
                    { new Guid("00000000-0000-4000-8000-000000000201"), "Northwind Data", "northwind data", "How do you check that the output of a data pipeline is correct?" },
                    { new Guid("00000000-0000-4000-8000-000000000202"), "Northwind Data", "northwind data", "Describe a messy dataset you worked with and how you cleaned it." },
                    { new Guid("00000000-0000-4000-8000-000000000203"), "Northwind Data", "northwind data", "When would you pick SQL over Python for a transformation?" },
                    { new Guid("00000000-0000-4000-8000-000000000301"), "Atelier Lumen", "atelier lumen", "Pourquoi voulez-vous développer des applications pour des musées ?" },
                    { new Guid("00000000-0000-4000-8000-000000000302"), "Atelier Lumen", "atelier lumen", "Comment testez-vous une application mobile sur plusieurs tailles d'écran ?" },
                    { new Guid("00000000-0000-4000-8000-000000000303"), "Atelier Lumen", "atelier lumen", "Parlez-moi d'un projet que vous avez mené en équipe." },
                    { new Guid("00000000-0000-4000-8000-000000000401"), "Havenstad Logistics", "havenstad logistics", "Wat trekt je aan in software voor de logistieke sector?" },
                    { new Guid("00000000-0000-4000-8000-000000000402"), "Havenstad Logistics", "havenstad logistics", "Hoe zou je een REST API ontwerpen om containers te volgen?" },
                    { new Guid("00000000-0000-4000-8000-000000000403"), "Havenstad Logistics", "havenstad logistics", "Hoe los je een merge-conflict op met je team?" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_questions_company_key",
                schema: "coach",
                table: "company_questions",
                column: "company_key");

            migrationBuilder.CreateIndex(
                name: "IX_interview_answers_posting_id_question_id",
                schema: "coach",
                table: "interview_answers",
                columns: new[] { "posting_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "IX_interview_questions_posting_id_position",
                schema: "coach",
                table: "interview_questions",
                columns: new[] { "posting_id", "position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_questions",
                schema: "coach");

            migrationBuilder.DropTable(
                name: "interview_answers",
                schema: "coach");

            migrationBuilder.DropTable(
                name: "interview_questions",
                schema: "coach");
        }
    }
}
