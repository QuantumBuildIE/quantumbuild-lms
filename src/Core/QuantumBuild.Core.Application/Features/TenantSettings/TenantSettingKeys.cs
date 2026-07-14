namespace QuantumBuild.Core.Application.Features.TenantSettings;

public static class TenantSettingKeys
{
    public const string GeneralModule = "General";

    public const string EmailTeamName = "EmailTeamName";
    public const string TalkCertificatePrefix = "TalkCertificatePrefix";
    public const string CourseCertificatePrefix = "CourseCertificatePrefix";
    public const string SkipValidationStep = "SkipValidationStep";
    public const string QrLocationTrainingEnabled = "QrLocationTrainingEnabled";
    public const string ExternalParticipantTokenLifetimeDays = "ExternalParticipantTokenLifetimeDays";
    public const string UseNewWizard = "UseNewWizard";
    public const string UseNewCourseCreation = "UseNewCourseCreation";

    public static class Defaults
    {
        public const string EmailTeamName = "Training Team";
        public const string TalkCertificatePrefix = "LRN";
        public const string CourseCertificatePrefix = "TBC";
        public const string UseNewCourseCreation = "true";
    }
}
