using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds regulatory requirements for the HIQA homecare profile.
/// All system-managed (BaseEntity, no TenantId), IngestionSource = Manual, IngestionStatus = Approved.
/// </summary>
public static class RegulatoryRequirementSeedData
{
    public static async Task SeedAsync(DbContext context, ILogger logger)
    {
        var now = DateTime.UtcNow;

        // Find the HIQA homecare profile by joining RegulatoryDocument (HIQA body) and Sector (homecare key)
        var hiqaBody = await context.Set<RegulatoryBody>()
            .IgnoreQueryFilters()
            .Where(b => !b.IsDeleted && b.Code == "HIQA")
            .FirstOrDefaultAsync();

        if (hiqaBody == null)
        {
            logger.LogWarning("HIQA regulatory body not found — skipping requirement seeding");
            return;
        }

        var hiqaDoc = await context.Set<RegulatoryDocument>()
            .IgnoreQueryFilters()
            .Where(d => !d.IsDeleted && d.RegulatoryBodyId == hiqaBody.Id)
            .FirstOrDefaultAsync();

        if (hiqaDoc == null)
        {
            logger.LogWarning("HIQA regulatory document not found — skipping requirement seeding");
            return;
        }

        var homecareSector = await context.Set<Sector>()
            .IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && s.Key == "homecare")
            .FirstOrDefaultAsync();

        if (homecareSector == null)
        {
            logger.LogWarning("Homecare sector not found — skipping requirement seeding");
            return;
        }

        var hiqaProfile = await context.Set<RegulatoryProfile>()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.RegulatoryDocumentId == hiqaDoc.Id && p.SectorId == homecareSector.Id)
            .FirstOrDefaultAsync();

        if (hiqaProfile == null)
        {
            logger.LogWarning("HIQA homecare regulatory profile not found — skipping requirement seeding");
            return;
        }

        // Check existing requirements for this profile to avoid duplicates
        var existingTitles = await context.Set<RegulatoryRequirement>()
            .IgnoreQueryFilters()
            .Where(r => r.RegulatoryProfileId == hiqaProfile.Id)
            .Select(r => r.Title)
            .ToListAsync();

        var seeds = new (string Title, string Description, string? Section, string? SectionLabel, string? Principle, string? PrincipleLabel, string Priority, int DisplayOrder)[]
        {
            ("Safeguarding Incident Recording",
                "All safeguarding concerns recorded immediately, within 24 hours of event",
                "§7", "Incident Reporting", "P2", "Safety & Wellbeing", "high", 1),

            ("Critical Incident Verbal Notification",
                "Serious/Critical incidents require immediate verbal notification — staff must be trained to prompt and record confirmation",
                "§7", "Incident Reporting", "P2", "Safety & Wellbeing", "high", 2),

            ("HIQA Notification Timeline",
                "HIQA notification required within 3 working days for serious incidents",
                "§7", "Incident Reporting", "P2", "Safety & Wellbeing", "high", 3),

            ("MAR Access Competency",
                "Only competency-verified staff may access Medication Administration Records",
                "§8", "MAR Management", "P2", "Safety & Wellbeing", "high", 4),

            ("MAR Pre-signing Prohibition",
                "MAR entries must never be pre-signed — sign-off only after scheduled administration time",
                "§8", "MAR Management", "P2", "Safety & Wellbeing", "high", 5),

            ("MAR Audit Trail",
                "Full audit trail integrity required — no silent edits to MAR records",
                "§8", "MAR Management", "P2", "Safety & Wellbeing", "med", 6),

            ("Care Plan Creation Timeline",
                "Care plan created within 5 working days of service commencement",
                "§5", "Care Plans", "P3", "Responsiveness", "high", 7),

            ("Care Plan Review Cycle",
                "Care plans reviewed every 6 months or after significant client change",
                "§5", "Care Plans", "P3", "Responsiveness", "med", 8),

            ("EVV GPS Check-in",
                "Staff must check in within 200m of client address — GPS validated",
                "§6", "Visit Verification", "P3", "Responsiveness", "high", 9),

            ("EVV Missed Visit Escalation",
                "No check-in after 30 minutes triggers automatic coordinator notification",
                "§6", "Visit Verification", "P3", "Responsiveness", "high", 10),

            ("Retrospective Visit Countersigning",
                "Retrospective visit entries must be countersigned by supervisor",
                "§6", "Visit Verification", "P3", "Responsiveness", "high", 11),

            ("Emergency Protocol Paper Backup",
                "System failure activates paper backup — all entries retrospectively logged with supervisor countersignature",
                "§10", "Emergency Protocol", "P4", "Accountability", "med", 12),

            ("Client Record Retention",
                "Client records retained 7 years after service end then scheduled for secure deletion",
                "§12", "Data Protection (GDPR)", "P4", "Accountability", "med", 13),

            ("Document Version Control",
                "All system documents carry version number, effective date, and approving role",
                "§1", "Version Control", "P4", "Accountability", "low", 14),

            ("Incident Escalation Hierarchy",
                "Escalation hierarchy defined — system auto-assigns responsible role by incident type",
                "§7", "Incident Escalation", "P4", "Accountability", "high", 15),
        };

        var newRequirements = new List<RegulatoryRequirement>();
        foreach (var (title, description, section, sectionLabel, principle, principleLabel, priority, displayOrder) in seeds)
        {
            if (existingTitles.Contains(title))
                continue;

            newRequirements.Add(new RegulatoryRequirement
            {
                Id = Guid.NewGuid(),
                RegulatoryProfileId = hiqaProfile.Id,
                Title = title,
                Description = description,
                Section = section,
                SectionLabel = sectionLabel,
                Principle = principle,
                PrincipleLabel = principleLabel,
                Priority = priority,
                DisplayOrder = displayOrder,
                IngestionSource = RequirementIngestionSource.Manual,
                IngestionStatus = RequirementIngestionStatus.Approved,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        if (newRequirements.Count > 0)
        {
            await context.Set<RegulatoryRequirement>().AddRangeAsync(newRequirements);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} regulatory requirements for HIQA homecare", newRequirements.Count);
        }
        else
        {
            logger.LogInformation("All HIQA homecare regulatory requirements already exist, skipping");
        }
    }
}
