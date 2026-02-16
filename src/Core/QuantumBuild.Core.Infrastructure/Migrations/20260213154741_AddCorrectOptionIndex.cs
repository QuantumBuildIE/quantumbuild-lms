using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectOptionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectOptionIndex",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions",
                type: "integer",
                nullable: true);

            // Backfill CorrectOptionIndex for existing MultipleChoice questions
            // by finding the 0-based index of CorrectAnswer in the Options JSON array
            migrationBuilder.Sql(@"
                UPDATE toolbox_talks.""ToolboxTalkQuestions"" q
                SET ""CorrectOptionIndex"" = subq.idx
                FROM (
                    SELECT q2.""Id"", (arr.ordinality - 1)::int AS idx
                    FROM toolbox_talks.""ToolboxTalkQuestions"" q2,
                         jsonb_array_elements_text(q2.""Options""::jsonb) WITH ORDINALITY AS arr(elem, ordinality)
                    WHERE LOWER(TRIM(arr.elem)) = LOWER(TRIM(q2.""CorrectAnswer""))
                      AND q2.""QuestionType"" = 'MultipleChoice'
                      AND q2.""Options"" IS NOT NULL
                      AND q2.""CorrectAnswer"" IS NOT NULL
                ) subq
                WHERE q.""Id"" = subq.""Id""
                  AND q.""CorrectOptionIndex"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectOptionIndex",
                schema: "toolbox_talks",
                table: "ToolboxTalkQuestions");
        }
    }
}
