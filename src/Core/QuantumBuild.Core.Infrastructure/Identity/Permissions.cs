namespace QuantumBuild.Core.Infrastructure.Identity;

/// <summary>
/// Static class containing all permission constants for the application.
/// 9 permissions across 3 modules: Learnings, Core, Tenant.
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Learnings module permissions (formerly ToolboxTalks)
    /// </summary>
    public static class Learnings
    {
        public const string View = "Learnings.View";
        public const string Manage = "Learnings.Manage";
        public const string Schedule = "Learnings.Schedule";
        public const string Admin = "Learnings.Admin";
    }

    /// <summary>
    /// Core module permissions (Sites, Employees, Companies, Users)
    /// </summary>
    public static class Core
    {
        public const string ManageEmployees = "Core.ManageEmployees";
        public const string ManageSites = "Core.ManageSites";
        public const string ManageCompanies = "Core.ManageCompanies";
        public const string ManageUsers = "Core.ManageUsers";
    }

    /// <summary>
    /// Tenant management permissions
    /// </summary>
    public static class Tenant
    {
        public const string Manage = "Tenant.Manage";
    }

    /// <summary>
    /// Get all permissions as a list (useful for seeding and policy registration)
    /// </summary>
    public static IEnumerable<string> GetAll()
    {
        return new[]
        {
            // Learnings permissions
            Learnings.View,
            Learnings.Manage,
            Learnings.Schedule,
            Learnings.Admin,

            // Core permissions
            Core.ManageEmployees,
            Core.ManageSites,
            Core.ManageCompanies,
            Core.ManageUsers,

            // Tenant permissions
            Tenant.Manage
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
