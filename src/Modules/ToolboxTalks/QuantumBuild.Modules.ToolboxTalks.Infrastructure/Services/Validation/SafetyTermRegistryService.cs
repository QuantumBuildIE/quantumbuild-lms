using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.SafetyTermRegistry;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public class SafetyTermRegistryService : ISafetyTermRegistryService
{
    private static readonly Dictionary<string, List<RegistryEntry>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Polish"] = new List<RegistryEntry>
        {
            new("safeguarding",
                ["bezpieczeństwo podopiecznych", "obawy bezpieczeństwa", "ochrona bezpieczeństwa"],
                "ochrona podopiecznych (safeguarding)",
                "bezpieczeństwo = physical safety, not safeguarding. HIQA requires ochrona podopiecznych"),

            new("must / is required to",
                ["należy", "powinien", "powinna", "powinno"],
                "musi / jest zobowiązany",
                "należy/powinien = should (recommendation). Regulatory obligations require musi (must)"),

            new("must not / is prohibited",
                ["nie powinien", "nie powinna", "nie należy"],
                "nie wolno / jest zabronione",
                "nie powinien = should not (advisory). Regulatory prohibitions require nie wolno"),

            new("immediately / without delay",
                ["natychmiast", "od razu"],
                "niezwłocznie",
                "niezwłocznie is the formal legal standard. natychmiast/od razu are informal"),

            new("countersignature",
                ["podpis", "co-podpisany"],
                "kontrasygnata",
                "podpis = any signature. Supervisory verification requires kontrasygnata")
        },

        ["Romanian"] = new List<RegistryEntry>
        {
            new("safeguarding",
                ["siguranță", "securitate"],
                "protecția persoanelor vulnerabile (safeguarding)",
                "siguranță/securitate = physical safety. Romanian requires the HIQA protection term"),

            new("must / is required to",
                ["ar trebui", "ar trebui să"],
                "trebuie să / este obligat să",
                "ar trebui = should (conditional). Obligations require trebuie să (must)")
        },

        ["Portuguese"] = new List<RegistryEntry>
        {
            new("safeguarding",
                ["segurança", "proteção de segurança"],
                "proteção de pessoas vulneráveis (safeguarding)",
                "segurança = physical safety. Requires proteção de pessoas vulneráveis"),

            new("must / is required to",
                ["deveria", "devia"],
                "deve / é obrigado a",
                "deveria = should (conditional). Obligations require deve (must)")
        },

        ["Spanish"] = new List<RegistryEntry>
        {
            new("safeguarding",
                ["seguridad", "protección de seguridad"],
                "protección de personas vulnerables (safeguarding)",
                "seguridad = physical safety. Requires protección de personas vulnerables"),

            new("must / is required to",
                ["debería", "habría que"],
                "debe / está obligado a",
                "debería = should (conditional). Obligations require debe (must)")
        },

        ["French"] = new List<RegistryEntry>
        {
            new("safeguarding",
                ["sécurité", "sauvegarde"],
                "protection des personnes vulnérables (safeguarding)",
                "sécurité = physical safety. sauvegarde = data backup (false friend). Requires protection des personnes vulnérables"),

            new("must / is required to",
                ["devrait", "faudrait"],
                "doit / est tenu de",
                "devrait = should (conditional). Obligations require doit (must)")
        }
    };

    public RegistryScanResult Scan(string translatedText, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(translatedText) ||
            string.IsNullOrWhiteSpace(targetLanguage) ||
            !Registry.TryGetValue(targetLanguage, out var entries))
        {
            return new RegistryScanResult([], false);
        }

        var violations = new List<RegistryViolation>();

        foreach (var entry in entries)
        {
            foreach (var badPattern in entry.BadPatterns)
            {
                if (translatedText.Contains(badPattern, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(new RegistryViolation(
                        entry.SourceTerm,
                        badPattern,
                        entry.RequiredTerm,
                        entry.Reason));
                }
            }
        }

        return new RegistryScanResult(violations, violations.Count > 0);
    }

    private record RegistryEntry(
        string SourceTerm,
        string[] BadPatterns,
        string RequiredTerm,
        string Reason);
}
