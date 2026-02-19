namespace QuantumBuild.Core.Application.Features.TenantSettings;

public static class TenantSettingKeys
{
    public const string GeneralModule = "General";

    public const string EmailTeamName = "EmailTeamName";
    public const string TalkCertificatePrefix = "TalkCertificatePrefix";
    public const string CourseCertificatePrefix = "CourseCertificatePrefix";

    public static class Defaults
    {
        public const string EmailTeamName = "Training Team";
        public const string TalkCertificatePrefix = "LRN";
        public const string CourseCertificatePrefix = "TBC";
    }
}
