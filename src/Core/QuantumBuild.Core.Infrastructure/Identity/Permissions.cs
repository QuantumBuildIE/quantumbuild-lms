namespace QuantumBuild.Core.Infrastructure.Identity;

/// <summary>
/// Static class containing all permission constants for the application
/// Simplified permission naming convention: {Module}.{PermissionName}
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Toolbox Talks module permissions
    /// </summary>
    public static class ToolboxTalks
    {
        public const string View = "ToolboxTalks.View";
        public const string Create = "ToolboxTalks.Create";
        public const string Edit = "ToolboxTalks.Edit";
        public const string Delete = "ToolboxTalks.Delete";
        public const string Schedule = "ToolboxTalks.Schedule";
        public const string ViewReports = "ToolboxTalks.ViewReports";
        public const string Admin = "ToolboxTalks.Admin";
    }

    /// <summary>
    /// Core module permissions (Sites, Employees, Companies, Users, Roles)
    /// </summary>
    public static class Core
    {
        public const string ManageSites = "Core.ManageSites";
        public const string ManageEmployees = "Core.ManageEmployees";
        public const string ManageCompanies = "Core.ManageCompanies";
        public const string ManageUsers = "Core.ManageUsers";
        public const string ManageRoles = "Core.ManageRoles";
        public const string Admin = "Core.Admin";
    }

    /// <summary>
    /// Get all permissions as a list (useful for seeding and policy registration)
    /// </summary>
    public static IEnumerable<string> GetAll()
    {
        return new[]
        {
            // Toolbox Talks permissions
            ToolboxTalks.View,
            ToolboxTalks.Create,
            ToolboxTalks.Edit,
            ToolboxTalks.Delete,
            ToolboxTalks.Schedule,
            ToolboxTalks.ViewReports,
            ToolboxTalks.Admin,

            // Core permissions
            Core.ManageSites,
            Core.ManageEmployees,
            Core.ManageCompanies,
            Core.ManageUsers,
            Core.ManageRoles,
            Core.Admin
        };
    }

    /// <summary>
    /// Get permissions by module name
    /// </summary>
    public static IEnumerable<string> GetByModule(string moduleName)
    {
        return GetAll().Where(p => p.StartsWith(moduleName + "."));
    }

    /// <summary>
    /// Get the module name from a permission string
    /// </summary>
    public static string GetModuleName(string permission)
    {
        var parts = permission.Split('.');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }
}
