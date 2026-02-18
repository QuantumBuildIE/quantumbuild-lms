using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuantumBuild.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRolesPermissionsAndAddSuperUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add IsSuperUser column to Users table
            migrationBuilder.AddColumn<bool>(
                name: "IsSuperUser",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 2. Rename permissions: ToolboxTalks.* → Learnings.*
            migrationBuilder.Sql(@"
                UPDATE ""Permissions"" SET ""Name"" = 'Learnings.View', ""Module"" = 'Learnings', ""Description"" = 'View learnings'
                WHERE ""Name"" = 'ToolboxTalks.View';

                UPDATE ""Permissions"" SET ""Name"" = 'Learnings.Manage', ""Module"" = 'Learnings', ""Description"" = 'Create, edit, and delete learnings'
                WHERE ""Name"" = 'ToolboxTalks.Create';

                UPDATE ""Permissions"" SET ""Name"" = 'Learnings.Schedule', ""Module"" = 'Learnings', ""Description"" = 'Schedule and assign learnings'
                WHERE ""Name"" = 'ToolboxTalks.Schedule';

                UPDATE ""Permissions"" SET ""Name"" = 'Learnings.Admin', ""Module"" = 'Learnings', ""Description"" = 'Full learnings administration'
                WHERE ""Name"" = 'ToolboxTalks.Admin';
            ");

            // 3. Delete consolidated permissions (Edit, Delete → merged into Manage)
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (
                    SELECT ""Id"" FROM ""Permissions"" WHERE ""Name"" IN ('ToolboxTalks.Edit', 'ToolboxTalks.Delete')
                );
                DELETE FROM ""Permissions"" WHERE ""Name"" IN ('ToolboxTalks.Edit', 'ToolboxTalks.Delete');
            ");

            // 4. Delete dead permissions: ToolboxTalks.ViewReports, Core.ManageRoles, Core.Admin
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (
                    SELECT ""Id"" FROM ""Permissions"" WHERE ""Name"" IN ('ToolboxTalks.ViewReports', 'Core.ManageRoles', 'Core.Admin')
                );
                DELETE FROM ""Permissions"" WHERE ""Name"" IN ('ToolboxTalks.ViewReports', 'Core.ManageRoles', 'Core.Admin');
            ");

            // 5. Delete old roles (Finance, OfficeStaff, SiteManager, WarehouseStaff, Operative) and their assignments
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions"" WHERE ""RoleId"" IN (
                    SELECT ""Id"" FROM ""Roles"" WHERE ""Name"" IN ('Finance', 'OfficeStaff', 'SiteManager', 'WarehouseStaff', 'Operative')
                );
                DELETE FROM ""UserRoles"" WHERE ""RoleId"" IN (
                    SELECT ""Id"" FROM ""Roles"" WHERE ""Name"" IN ('Finance', 'OfficeStaff', 'SiteManager', 'WarehouseStaff', 'Operative')
                );
                DELETE FROM ""Roles"" WHERE ""Name"" IN ('Finance', 'OfficeStaff', 'SiteManager', 'WarehouseStaff', 'Operative');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuperUser",
                table: "Users");

            // Note: Permission renames and role deletions are not reversed in Down().
            // Re-seeding via DataSeeder is required if rolling back.
        }
    }
}
