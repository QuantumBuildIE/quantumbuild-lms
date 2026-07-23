using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds the regulatory profile chain: RegulatoryBody → RegulatoryDocument → RegulatoryProfile → RegulatoryCriteria.
/// All system-managed (no TenantId except RegulatoryCriteria system defaults with TenantId = null).
/// </summary>
public static class RegulatoryProfileSeedData
{
    public static async Task SeedAsync(DbContext context, ILogger logger)
    {
        var now = DateTime.UtcNow;

        // --- 1. Regulatory Bodies ---
        // TranslationInstructions text ported verbatim from the old TranslationPrompts.GetSectorInstructions
        // switch statement (removed in the ApplicableFrameworksService refactor) — one block per body, reused
        // for every sector the body's documents cover (e.g. HIQA covers both homecare and healthcare).
        var bodyTranslationInstructions = new Dictionary<string, string>
        {
            ["HIQA"] = """
                SECTOR-SPECIFIC REQUIREMENTS (Healthcare / Homecare — HIQA):
                - REGULATORY CONTEXT: This is a HIQA-regulated homecare/healthcare compliance document. Regulatory precision is mandatory.
                - SAFEGUARDING: "Safeguarding" must retain its full legal meaning — do not translate as generic "safety" or "protection".
                - MANDATORY REPORTING: Notification deadlines (e.g. "within 3 working days") must be numerically exact.
                - JOB TITLES: Designated Liaison Person (DLP), Person in Charge (PIC), Registered Provider — translate consistently using approved equivalents or retain in English if no standard equivalent exists.
                - CONSENT: "Informed consent", "capacity", "advocacy" — use clinical/legal standard translations.
                """,

            ["HSA"] = """
                SECTOR-SPECIFIC REQUIREMENTS (Construction / Manufacturing — HSA):
                - REGULATORY CONTEXT: This is an HSA-regulated workplace safety document. Safety language precision is mandatory.
                - PPE: All personal protective equipment terms must be translated precisely — no omissions or paraphrasing.
                - PROHIBITIONS: "Do not", "never", "must not" must retain full imperative force — never soften to "should not" or "it is recommended".
                - ROLES: PSDP, PSCS, competent person — translate with approved equivalents only.
                - RISK: Hazard categories and risk levels must match source exactly.
                """,

            ["FSAI"] = """
                SECTOR-SPECIFIC REQUIREMENTS (Food & Hospitality — FSAI):
                - REGULATORY CONTEXT: This is an FSAI-regulated food safety document. Allergen and HACCP terminology precision is mandatory.
                - ALLERGENS: All 14 declarable allergens must be named precisely — never paraphrased or approximated.
                - HACCP: Critical Control Point terminology must be consistent and exact throughout.
                - TEMPERATURES: All numeric temperature thresholds must be preserved exactly.
                - CCP LIMITS: Critical limits must not be softened or approximated.
                """,

            ["RSA"] = """
                SECTOR-SPECIFIC REQUIREMENTS (Transport — RSA):
                - REGULATORY CONTEXT: This is an RSA-regulated road transport document. Numeric precision is mandatory.
                - DRIVER HOURS: All hour and rest period requirements must be numerically exact.
                - TACHOGRAPH: Technical terms must use approved translations only — do not improvise.
                - LOAD LIMITS: All weight and dimension limits must be preserved exactly.
                - PROHIBITIONS: Driving prohibitions must retain full legal force.
                """,
        };

        var existingBodies = await context.Set<RegulatoryBody>()
            .IgnoreQueryFilters()
            .Where(b => !b.IsDeleted)
            .ToListAsync();
        var existingBodyCodes = existingBodies.Select(b => b.Code).ToHashSet();

        var bodySeeds = new (string Code, string Name, string Country)[]
        {
            ("HIQA", "Health Information and Quality Authority", "Ireland"),
            ("HSA", "Health and Safety Authority", "Ireland"),
            ("FSAI", "Food Safety Authority of Ireland", "Ireland"),
            ("RSA", "Road Safety Authority", "Ireland"),
        };

        var newBodies = new List<RegulatoryBody>();
        foreach (var (code, name, country) in bodySeeds)
        {
            if (existingBodyCodes.Contains(code))
                continue;

            newBodies.Add(new RegulatoryBody
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Country = country,
                Kind = RegulatoryBodyKind.Regulation,
                TranslationInstructions = bodyTranslationInstructions.GetValueOrDefault(code),
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        if (newBodies.Count > 0)
        {
            await context.Set<RegulatoryBody>().AddRangeAsync(newBodies);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} regulatory bodies", newBodies.Count);
        }

        // Backfill TranslationInstructions on bodies seeded before this field existed.
        var bodiesNeedingBackfill = existingBodies
            .Where(b => string.IsNullOrEmpty(b.TranslationInstructions)
                && bodyTranslationInstructions.ContainsKey(b.Code))
            .ToList();
        if (bodiesNeedingBackfill.Count > 0)
        {
            foreach (var body in bodiesNeedingBackfill)
            {
                body.TranslationInstructions = bodyTranslationInstructions[body.Code];
            }
            await context.SaveChangesAsync();
            logger.LogInformation(
                "Backfilled TranslationInstructions on {Count} pre-existing regulatory bodies", bodiesNeedingBackfill.Count);
        }

        // Reload all bodies for FK lookups
        var bodies = await context.Set<RegulatoryBody>()
            .IgnoreQueryFilters()
            .Where(b => !b.IsDeleted)
            .ToDictionaryAsync(b => b.Code, b => b.Id);

        // --- 2. Regulatory Documents ---
        var existingDocTitles = await context.Set<RegulatoryDocument>()
            .IgnoreQueryFilters()
            .Select(d => new { d.RegulatoryBodyId, d.Title })
            .ToListAsync();

        var docSeeds = new (string BodyCode, string Title, string Version, string Source)[]
        {
            ("HIQA", "Draft National Standards for Home Support Services", "Draft Nov 2024", "HIQA Draft National Standards \u00b7 Nov 2024"),
            ("HSA", "Safety, Health and Welfare at Work Regulations", "Current", "HSA Safety & Health Regulations"),
            ("FSAI", "Food Safety Authority of Ireland Regulations", "Current", "FSAI Food Safety Regulations"),
            ("RSA", "Road Transport Regulations", "Current", "RSA Road Transport Regulations"),
        };

        var newDocs = new List<RegulatoryDocument>();
        foreach (var (bodyCode, title, version, source) in docSeeds)
        {
            if (!bodies.TryGetValue(bodyCode, out var bodyId))
                continue;

            if (existingDocTitles.Any(d => d.RegulatoryBodyId == bodyId && d.Title == title))
                continue;

            newDocs.Add(new RegulatoryDocument
            {
                Id = Guid.NewGuid(),
                RegulatoryBodyId = bodyId,
                Title = title,
                Version = version,
                Source = source,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        if (newDocs.Count > 0)
        {
            await context.Set<RegulatoryDocument>().AddRangeAsync(newDocs);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} regulatory documents", newDocs.Count);
        }

        // Reload all documents for FK lookups (keyed by body code)
        var allDocs = await context.Set<RegulatoryDocument>()
            .IgnoreQueryFilters()
            .Where(d => !d.IsDeleted)
            .ToListAsync();

        var docsByBodyCode = new Dictionary<string, Guid>();
        foreach (var doc in allDocs)
        {
            var bodyCode = bodies.FirstOrDefault(b => b.Value == doc.RegulatoryBodyId).Key;
            if (bodyCode != null && !docsByBodyCode.ContainsKey(bodyCode))
                docsByBodyCode[bodyCode] = doc.Id;
        }

        // --- 3. Regulatory Profiles ---
        var sectors = await context.Set<Sector>()
            .IgnoreQueryFilters()
            .Where(s => !s.IsDeleted)
            .ToDictionaryAsync(s => s.Key, s => s);

        var existingProfiles = await context.Set<RegulatoryProfile>()
            .IgnoreQueryFilters()
            .Select(p => new { p.RegulatoryDocumentId, p.SectorId })
            .ToListAsync();

        var profileSeeds = new (string BodyCode, string SectorKey, string ScoreLabel, string ExportLabel, string Description, string CategoryWeightsJson)[]
        {
            ("HIQA", "homecare",
                "HIQA Regulatory Score", "HIQA Inspection Export",
                "Safeguarding, medication, mandatory reporting, EVV compliance",
                "[{\"Key\":\"TERMINOLOGY_CONSISTENCY\",\"Label\":\"Terminology Consistency\",\"Weight\":1.5},{\"Key\":\"PROFESSIONAL_REGISTER\",\"Label\":\"Professional Register\",\"Weight\":1.0},{\"Key\":\"SAFETY_CRITICAL_LANGUAGE\",\"Label\":\"Safety-Critical Language\",\"Weight\":1.5},{\"Key\":\"REGULATORY_COMPLETENESS\",\"Label\":\"Regulatory Completeness\",\"Weight\":1.0},{\"Key\":\"GRAMMATICAL_ACCURACY\",\"Label\":\"Grammatical Accuracy\",\"Weight\":1.0},{\"Key\":\"NATURALNESS_FLUENCY\",\"Label\":\"Naturalness & Fluency\",\"Weight\":1.0}]"),

            ("HSA", "construction",
                "HSA Regulatory Score", "HSA Inspection Export",
                "PPE requirements, risk assessments, method statements, PSDP/PSCS roles",
                "[{\"Key\":\"TERMINOLOGY_CONSISTENCY\",\"Label\":\"Terminology Consistency\",\"Weight\":1.5},{\"Key\":\"PROFESSIONAL_REGISTER\",\"Label\":\"Professional Register\",\"Weight\":1.0},{\"Key\":\"SAFETY_CRITICAL_LANGUAGE\",\"Label\":\"Safety-Critical Language\",\"Weight\":2.0},{\"Key\":\"REGULATORY_COMPLETENESS\",\"Label\":\"Regulatory Completeness\",\"Weight\":1.0},{\"Key\":\"GRAMMATICAL_ACCURACY\",\"Label\":\"Grammatical Accuracy\",\"Weight\":1.0},{\"Key\":\"NATURALNESS_FLUENCY\",\"Label\":\"Naturalness & Fluency\",\"Weight\":0.5}]"),

            ("HSA", "manufacturing",
                "HSA Regulatory Score", "HSA Inspection Export",
                "Machine guarding, LOTO, chemical handling, REACH compliance",
                "[{\"Key\":\"TERMINOLOGY_CONSISTENCY\",\"Label\":\"Terminology Consistency\",\"Weight\":1.5},{\"Key\":\"PROFESSIONAL_REGISTER\",\"Label\":\"Professional Register\",\"Weight\":1.0},{\"Key\":\"SAFETY_CRITICAL_LANGUAGE\",\"Label\":\"Safety-Critical Language\",\"Weight\":2.0},{\"Key\":\"REGULATORY_COMPLETENESS\",\"Label\":\"Regulatory Completeness\",\"Weight\":1.0},{\"Key\":\"GRAMMATICAL_ACCURACY\",\"Label\":\"Grammatical Accuracy\",\"Weight\":1.0},{\"Key\":\"NATURALNESS_FLUENCY\",\"Label\":\"Naturalness & Fluency\",\"Weight\":0.5}]"),

            ("FSAI", "food_hospitality",
                "FSAI Regulatory Score", "FSAI Inspection Export",
                "HACCP, allergen controls, temperature requirements, CCP terminology",
                "[{\"Key\":\"TERMINOLOGY_CONSISTENCY\",\"Label\":\"Terminology Consistency\",\"Weight\":2.0},{\"Key\":\"PROFESSIONAL_REGISTER\",\"Label\":\"Professional Register\",\"Weight\":1.0},{\"Key\":\"SAFETY_CRITICAL_LANGUAGE\",\"Label\":\"Safety-Critical Language\",\"Weight\":1.5},{\"Key\":\"REGULATORY_COMPLETENESS\",\"Label\":\"Regulatory Completeness\",\"Weight\":1.5},{\"Key\":\"GRAMMATICAL_ACCURACY\",\"Label\":\"Grammatical Accuracy\",\"Weight\":1.0},{\"Key\":\"NATURALNESS_FLUENCY\",\"Label\":\"Naturalness & Fluency\",\"Weight\":0.5}]"),

            ("RSA", "transport",
                "RSA Regulatory Score", "RSA Inspection Export",
                "Driver hours, tachograph, load limits, vehicle inspection requirements",
                "[{\"Key\":\"TERMINOLOGY_CONSISTENCY\",\"Label\":\"Terminology Consistency\",\"Weight\":1.5},{\"Key\":\"PROFESSIONAL_REGISTER\",\"Label\":\"Professional Register\",\"Weight\":1.0},{\"Key\":\"SAFETY_CRITICAL_LANGUAGE\",\"Label\":\"Safety-Critical Language\",\"Weight\":1.5},{\"Key\":\"REGULATORY_COMPLETENESS\",\"Label\":\"Regulatory Completeness\",\"Weight\":1.5},{\"Key\":\"GRAMMATICAL_ACCURACY\",\"Label\":\"Grammatical Accuracy\",\"Weight\":1.0},{\"Key\":\"NATURALNESS_FLUENCY\",\"Label\":\"Naturalness & Fluency\",\"Weight\":0.5}]"),

            ("HIQA", "healthcare",
                "HIQA Regulatory Score", "HIQA Inspection Export",
                "Clinical governance, patient safety, infection control, medication management",
                "[{\"Key\":\"TERMINOLOGY_CONSISTENCY\",\"Label\":\"Terminology Consistency\",\"Weight\":1.5},{\"Key\":\"PROFESSIONAL_REGISTER\",\"Label\":\"Professional Register\",\"Weight\":1.0},{\"Key\":\"SAFETY_CRITICAL_LANGUAGE\",\"Label\":\"Safety-Critical Language\",\"Weight\":1.5},{\"Key\":\"REGULATORY_COMPLETENESS\",\"Label\":\"Regulatory Completeness\",\"Weight\":1.0},{\"Key\":\"GRAMMATICAL_ACCURACY\",\"Label\":\"Grammatical Accuracy\",\"Weight\":1.0},{\"Key\":\"NATURALNESS_FLUENCY\",\"Label\":\"Naturalness & Fluency\",\"Weight\":1.0}]"),
        };

        var newProfiles = new List<RegulatoryProfile>();
        foreach (var (bodyCode, sectorKey, scoreLabel, exportLabel, description, categoryWeightsJson) in profileSeeds)
        {
            if (!docsByBodyCode.TryGetValue(bodyCode, out var docId))
                continue;
            if (!sectors.TryGetValue(sectorKey, out var sector))
                continue;
            if (existingProfiles.Any(p => p.RegulatoryDocumentId == docId && p.SectorId == sector.Id))
                continue;

            newProfiles.Add(new RegulatoryProfile
            {
                Id = Guid.NewGuid(),
                RegulatoryDocumentId = docId,
                SectorId = sector.Id,
                SectorKey = sectorKey,
                ScoreLabel = scoreLabel,
                ExportLabel = exportLabel,
                Description = description,
                CategoryWeightsJson = categoryWeightsJson,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        if (newProfiles.Count > 0)
        {
            await context.Set<RegulatoryProfile>().AddRangeAsync(newProfiles);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} regulatory profiles", newProfiles.Count);
        }

        // --- 4. Regulatory Criteria (system defaults, TenantId = null) ---
        var allProfiles = await context.Set<RegulatoryProfile>()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        // Build lookup: (bodyCode, sectorKey) → profileId
        var profileLookup = new Dictionary<(string BodyCode, string SectorKey), Guid>();
        foreach (var profile in allProfiles)
        {
            var docBodyCode = docSeeds
                .Where(d => docsByBodyCode.TryGetValue(d.BodyCode, out var did) && did == profile.RegulatoryDocumentId)
                .Select(d => d.BodyCode)
                .FirstOrDefault();
            if (docBodyCode != null)
                profileLookup[(docBodyCode, profile.SectorKey)] = profile.Id;
        }

        var existingCriteria = await context.Set<RegulatoryCriteria>()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == null)
            .Select(c => new { c.RegulatoryProfileId, c.CategoryKey, c.DisplayOrder })
            .ToListAsync();

        // (BodyCode, SectorKey, CategoryKey, CriteriaText, Source, DisplayOrder)
        var criteriaSeeds = new (string BodyCode, string SectorKey, string CategoryKey, string CriteriaText, string Source, int DisplayOrder)[]
        {
            // Homecare (HIQA)
            ("HIQA", "homecare", "SAFETY_CRITICAL_LANGUAGE",
                "SAFEGUARDING: 'safeguarding' must be translated with its full legal meaning \u2014 not weakened to 'safety' or 'protection' generically",
                "\u00a77 Incident Reporting", 1),
            ("HIQA", "homecare", "SAFETY_CRITICAL_LANGUAGE",
                "MEDICATION: Pre-signing prohibition must be preserved with full force",
                "\u00a78 MAR Management", 2),
            ("HIQA", "homecare", "REGULATORY_COMPLETENESS",
                "EVV: GPS radius, time thresholds, and coordinator response windows must be exact",
                "\u00a76 Visit Verification", 3),
            ("HIQA", "homecare", "REGULATORY_COMPLETENESS",
                "HIQA REPORTING: 3-business-day notification requirement must be explicit",
                "\u00a77 Incident Reporting", 4),

            // Construction (HSA)
            ("HSA", "construction", "TERMINOLOGY_CONSISTENCY",
                "PPE: All personal protective equipment terms must be translated precisely \u2014 no omissions",
                "PPE Requirements", 1),
            ("HSA", "construction", "SAFETY_CRITICAL_LANGUAGE",
                "PROHIBITIONS: 'Do not', 'never', 'must not' must retain full imperative force",
                "Safety Language", 2),
            ("HSA", "construction", "TERMINOLOGY_CONSISTENCY",
                "ROLES: PSDP, PSCS, competent person \u2014 translate with approved equivalents only",
                "Roles & Responsibilities", 3),
            ("HSA", "construction", "SAFETY_CRITICAL_LANGUAGE",
                "RISK: Hazard categories and risk levels must match source exactly",
                "Risk Assessment", 4),

            // Manufacturing (HSA)
            ("HSA", "manufacturing", "TERMINOLOGY_CONSISTENCY",
                "CHEMICALS: All chemical names, CAS numbers, and GHS hazard categories must be exact",
                "Chemical Handling", 1),
            ("HSA", "manufacturing", "REGULATORY_COMPLETENESS",
                "LOTO: Lockout/tagout procedure steps must be complete and in correct order",
                "LOTO Procedures", 2),
            ("HSA", "manufacturing", "SAFETY_CRITICAL_LANGUAGE",
                "MACHINE GUARDING: Safety interlock and guarding requirements must not be softened",
                "Machine Safety", 3),
            ("HSA", "manufacturing", "SAFETY_CRITICAL_LANGUAGE",
                "PPE: All personal protective equipment requirements must be fully preserved",
                "PPE Requirements", 4),

            // Food & Hospitality (FSAI)
            ("FSAI", "food_hospitality", "TERMINOLOGY_CONSISTENCY",
                "ALLERGENS: All 14 declarable allergens must be named precisely \u2014 never paraphrased",
                "Allergen Control", 1),
            ("FSAI", "food_hospitality", "TERMINOLOGY_CONSISTENCY",
                "HACCP: Critical Control Point terminology must be consistent and exact",
                "HACCP", 2),
            ("FSAI", "food_hospitality", "REGULATORY_COMPLETENESS",
                "TEMPERATURES: All numeric temperature thresholds must be preserved exactly",
                "Temperature Control", 3),
            ("FSAI", "food_hospitality", "REGULATORY_COMPLETENESS",
                "CCP LIMITS: Critical limits must not be softened or approximated",
                "CCP Management", 4),

            // Transport (RSA)
            ("RSA", "transport", "REGULATORY_COMPLETENESS",
                "HOURS: Driver hours and rest period requirements must be numerically exact",
                "Driver Hours", 1),
            ("RSA", "transport", "TERMINOLOGY_CONSISTENCY",
                "TACHOGRAPH: Technical terms must use approved translations only",
                "Tachograph", 2),
            ("RSA", "transport", "REGULATORY_COMPLETENESS",
                "LOAD LIMITS: All weight and dimension limits must be preserved exactly",
                "Load Management", 3),
            ("RSA", "transport", "SAFETY_CRITICAL_LANGUAGE",
                "PROHIBITIONS: Driving prohibitions must retain full legal force",
                "Safety Language", 4),
        };

        var newCriteria = new List<RegulatoryCriteria>();
        foreach (var (bodyCode, sectorKey, categoryKey, criteriaText, source, displayOrder) in criteriaSeeds)
        {
            if (!profileLookup.TryGetValue((bodyCode, sectorKey), out var profileId))
                continue;

            if (existingCriteria.Any(c => c.RegulatoryProfileId == profileId && c.CategoryKey == categoryKey && c.DisplayOrder == displayOrder))
                continue;

            newCriteria.Add(new RegulatoryCriteria
            {
                Id = Guid.NewGuid(),
                RegulatoryProfileId = profileId,
                TenantId = null,
                CategoryKey = categoryKey,
                CriteriaText = criteriaText,
                Source = source,
                DisplayOrder = displayOrder,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        if (newCriteria.Count > 0)
        {
            await context.Set<RegulatoryCriteria>().AddRangeAsync(newCriteria);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} regulatory criteria", newCriteria.Count);
        }

        if (newBodies.Count == 0 && newDocs.Count == 0 && newProfiles.Count == 0 && newCriteria.Count == 0
            && bodiesNeedingBackfill.Count == 0)
        {
            logger.LogInformation("All regulatory profile data already exists, skipping");
        }
    }
}
