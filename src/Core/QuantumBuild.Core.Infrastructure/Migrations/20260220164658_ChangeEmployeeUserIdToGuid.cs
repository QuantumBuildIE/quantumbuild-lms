using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEmployeeUserIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cast existing text values to uuid; NULLs remain NULL
            migrationBuilder.Sql(
                """ALTER TABLE "Employees" ALTER COLUMN "UserId" TYPE uuid USING "UserId"::uuid;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "Employees" ALTER COLUMN "UserId" TYPE character varying(450) USING "UserId"::text;""");
        }
    }
}
