namespace QuantumBuild.Core.Application.Constants;

public static class AuditActions
{
    public static class Auth
    {
        public const string LoginSuccess = "Auth.Login.Success";
        public const string LoginFailed = "Auth.Login.Failed";
        public const string LoginAccountInactive = "Auth.Login.AccountInactive";
        public const string Logout = "Auth.Logout";
        public const string TokenRefresh = "Auth.TokenRefresh";
        public const string PasswordSet = "Auth.PasswordSet";
    }

    public static class User
    {
        public const string Create = "User.Create";
        public const string Update = "User.Update";
        public const string Delete = "User.Delete";
        public const string PasswordReset = "User.PasswordReset";
        public const string LinkEmployee = "User.LinkEmployee";
        public const string UnlinkEmployee = "User.UnlinkEmployee";
    }

    public static class Tenant
    {
        public const string Create = "Tenant.Create";
        public const string Update = "Tenant.Update";
        public const string StatusUpdate = "Tenant.StatusUpdate";
        public const string Reset = "Tenant.Reset";
    }

    public static class Module
    {
        public const string Assign = "Module.Assign";
        public const string Remove = "Module.Remove";
    }

    public static class Employee
    {
        public const string Create = "Employee.Create";
        public const string Update = "Employee.Update";
        public const string Delete = "Employee.Delete";
        public const string AssignOperator = "Employee.AssignOperator";
        public const string UnassignOperator = "Employee.UnassignOperator";
    }
}
