using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolboxTalkCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add Code column as nullable (so existing rows don't fail)
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Step 2: Backfill existing rows with auto-generated codes from titles
            migrationBuilder.Sql(@"
DO $$
DECLARE
    rec RECORD;
    v_prefix TEXT;
    v_words TEXT[];
    v_word TEXT;
    v_code TEXT;
    v_suffix INT;
    v_common_words TEXT[] := ARRAY['a','an','the','and','or','for','in','to','of','on','at','by','with','from','is','it'];
BEGIN
    FOR rec IN
        SELECT ""Id"", ""TenantId"", ""Title""
        FROM toolbox_talks.""ToolboxTalks""
        WHERE ""Code"" IS NULL OR ""Code"" = ''
        ORDER BY ""CreatedAt""
    LOOP
        -- Build prefix from first letter of each non-common word
        v_prefix := '';
        v_words := string_to_array(rec.""Title"", ' ');
        FOREACH v_word IN ARRAY v_words
        LOOP
            IF v_word != '' AND NOT (LOWER(v_word) = ANY(v_common_words)) THEN
                v_prefix := v_prefix || UPPER(LEFT(v_word, 1));
            END IF;
        END LOOP;

        -- If fewer than 2 characters, take first 3 chars of title
        IF LENGTH(v_prefix) < 2 THEN
            v_prefix := UPPER(LEFT(REPLACE(rec.""Title"", ' ', ''), 3));
        END IF;

        -- Truncate prefix to 16 chars max (leaves room for -NNN)
        IF LENGTH(v_prefix) > 16 THEN
            v_prefix := LEFT(v_prefix, 16);
        END IF;

        -- Find next available suffix number for this prefix within the tenant
        SELECT COALESCE(MAX(
            CASE
                WHEN ""Code"" ~ ('^' || v_prefix || '-[0-9]+$')
                THEN CAST(SUBSTRING(""Code"" FROM LENGTH(v_prefix) + 2) AS INT)
                ELSE 0
            END
        ), 0) + 1 INTO v_suffix
        FROM toolbox_talks.""ToolboxTalks""
        WHERE ""TenantId"" = rec.""TenantId""
          AND ""Code"" LIKE v_prefix || '-%';

        v_code := v_prefix || '-' || LPAD(v_suffix::TEXT, 3, '0');

        UPDATE toolbox_talks.""ToolboxTalks""
        SET ""Code"" = v_code
        WHERE ""Id"" = rec.""Id"";
    END LOOP;
END $$;
");

            // Step 3: Make the column required now that all rows have a value
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Step 4: Create unique composite index
            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalks_TenantId_Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ToolboxTalks_TenantId_Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "toolbox_talks",
                table: "ToolboxTalks");
        }
    }
}
