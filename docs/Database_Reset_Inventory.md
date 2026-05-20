Database Reset Inventory Report
1. SuperUser Identification
Primary identifier: Users.IsSuperUser = true (boolean column on the User entity)

Secondary confirmation: UserRoles join to Roles where Roles.Name = 'SuperUser'

The seeded row is superuser@certifiediq.ai. To SELECT the rows to preserve:


SELECT * FROM "Users" WHERE "IsSuperUser" = true
Critical FK issue — Users.TenantId: The SuperUser row has TenantId = '11111111-1111-1111-1111-111111111111' (the hardcoded default tenant GUID used in the seeder). This is a FK to the Tenants table. If we delete all rows from Tenants, this FK breaks.

Options (confirm which you want before I generate SQL):

Option A (recommended): Null out Users.TenantId for SuperUser before deleting tenants, then re-run the seeder which will recreate the default tenant row and re-link it — OR insert that one tenant row manually with the known GUID.
Option B: Leave the default tenant row (11111111-1111-1111-1111-111111111111) in Tenants but delete everything else tenant-scoped from that tenant too.
2. Table Inventory
a. Tenant-scoped tables — TenantEntity-derived, all rows deleted
(These have TenantId via inheritance — full truncate or DELETE WHERE 1=1)

Default schema:

Table	Notes
Sites	
Employees	Has QrPin-related columns — all gone
Companies	
Contacts	Children of Companies — delete first
SupervisorAssignments	
TenantModules	
BulkImportSessions	
TenantLookupValues	
Schema toolbox_talks:

Table	Notes
ToolboxTalks	Parent of many child tables
ToolboxTalkSchedules	
ToolboxTalkCourses	
ToolboxTalkCourseAssignments	TenantEntity — confirmed
ScheduledTalks	
ToolboxTalkTranslations	
ToolboxTalkVideoTranslations	
ToolboxTalkCertificates	
ToolboxTalkSlides	
SubtitleProcessingJobs	
TranslationValidationRuns	Has FK to PipelineVersions (system-level — keep)
ContentCreationSessions	
TranslationDeviations	TenantEntity — confirmed
ValidationRegulatoryScores	
RegulatoryRequirementMappings	
AiUsageLogs	
AiUsageSummaries	
AuditCorpora	
CorpusRuns	
QrLocations	
QrCodes	
QrSessions	
TenantSectors	
LessonParser module (default schema):

Table	Notes
ParseJobs	TenantEntity
b. Child tables with no TenantId but scoped via FK parent — all rows deleted (parent's data being cleared anyway)
Table	Parent	Notes
ToolboxTalkSections	ToolboxTalks	BaseEntity only
ToolboxTalkQuestions	ToolboxTalks	BaseEntity only
ToolboxTalkSlideTranslations	ToolboxTalkSlides	
ToolboxTalkSlideshowTranslations	ToolboxTalks	
ToolboxTalkCourseItems	Courses	
ToolboxTalkCourseTranslations	Courses	
ToolboxTalkScheduleAssignments	Schedules	
ScheduledTalkSectionProgress	ScheduledTalks	
ScheduledTalkQuizAttempts	ScheduledTalks	
ScheduledTalkCompletions	ScheduledTalks	
SubtitleTranslations	SubtitleProcessingJobs	
TranslationValidationResults	TranslationValidationRuns	
AuditCorpusEntries	AuditCorpora	
CorpusRunResults	CorpusRuns / AuditCorpusEntries	
c. System-level tables with mixed data — partial delete
Table	Action
SafetyGlossaries	DELETE WHERE "TenantId" IS NOT NULL — tenant overrides only
SafetyGlossaryTerms	Cascades from parent SafetyGlossary delete (no TenantId on terms directly)
RegulatoryCriteria	DELETE WHERE "TenantId" IS NOT NULL — tenant overrides only
ToolboxTalkSettings	DELETE WHERE "TenantId" IS NOT NULL — BaseEntity but one-row-per-tenant
DpaAcceptances	DELETE WHERE "TenantId" IS NOT NULL — BaseEntity with explicit TenantId
d. Core tables — selective delete (exclude SuperUser rows)
Table	Action
Users	Delete all rows WHERE IsSuperUser = false
UserRoles	Delete all rows where UserId not in SuperUser set
UserClaims	Delete all rows where UserId not in SuperUser set
UserLogins	Delete all rows where UserId not in SuperUser set
UserTokens	Delete all rows where UserId not in SuperUser set
Tenants	Delete all rows (see TenantId FK issue above)
Roles	Preserve SuperUser role; all others can go but RolePermissions for non-SuperUser roles should be cleaned up first
RolePermissions	Delete rows for non-SuperUser roles
RoleClaims	Delete rows for non-SuperUser roles
e. Hangfire tables — full truncate
Hangfire uses prefix hangfire_ (PostgreSQL storage). Tables to truncate:

Table
hangfire_job
hangfire_jobparameter
hangfire_jobstate
hangfire_state
hangfire_counter
hangfire_hash
hangfire_list
hangfire_set
hangfire_server
hangfire_lock
f. Audit — full truncate
Table	Notes
SystemAuditLogs	Nullable TenantId plain field (not FK), no inheritance — just truncate
g. System-level tables — DO NOT TOUCH
These are fully preserved:

Table	Reason
Sectors	System seed data
RegulatoryBodies	System seed data
RegulatoryDocuments	System seed data
RegulatoryProfiles	System seed data
RegulatoryRequirements	System seed data (IngestionStatus-gated)
LookupCategories	Training categories, languages
LookupValues	Same
Permissions	System-wide permission registry
Roles (SuperUser row only)	
RolePermissions (SuperUser's)	
PipelineVersions	System-level audit record
PipelineChangeRecords	Append-only system record
ProviderResultCache	System-level cache (no TenantId); safe to wipe if you want a clean slate, but it's not tenant data
CustomerUsageReportStates	System singleton — one row ever
3. Deletion Order (FK-safe)
The deepest children must go first. Here's the sequence:

Wave 1 — Deepest leaves (no children themselves):
CorpusRunResults, AuditCorpusEntries, ScheduledTalkSectionProgress, ScheduledTalkQuizAttempts, ScheduledTalkCompletions, ToolboxTalkSlideTranslations, SubtitleTranslations, TranslationValidationResults, ValidationRegulatoryScores, ToolboxTalkCourseItems, ToolboxTalkCourseTranslations, ToolboxTalkSlideshowTranslations

Wave 2:
CorpusRuns, TranslationDeviations, ScheduledTalks, ToolboxTalkCourseAssignments, ToolboxTalkScheduleAssignments, ToolboxTalkSlides, SubtitleProcessingJobs, ToolboxTalkVideoTranslations, ToolboxTalkTranslations, ContentCreationSessions

Wave 3:
AuditCorpora, TranslationValidationRuns, RegulatoryRequirementMappings, QrSessions, QrCodes, QrLocations, AiUsageLogs, AiUsageSummaries, ToolboxTalkCertificates, ToolboxTalkSchedules (→ after schedule assignments gone), ToolboxTalkCourses

Wave 4:
ToolboxTalkSections, ToolboxTalkQuestions, ToolboxTalkSettings (WHERE TenantId IS NOT NULL)

Wave 5:
ToolboxTalks, ParseJobs, BulkImportSessions

Wave 6 — Core tenant-adjacent:
SupervisorAssignments, QrSessions (if not already), TenantSectors, TenantModules, TenantLookupValues, DpaAcceptances (WHERE TenantId IS NOT NULL)

Wave 7 — Core entities:
Contacts, Employees, Sites, Companies

Wave 8 — Identity (non-SuperUser):
UserClaims, UserLogins, UserTokens, UserRoles (non-SuperUser), RolePermissions (non-SuperUser), RoleClaims (non-SuperUser), Users (WHERE IsSuperUser = false)

Wave 9 — SafetyGlossary tenant overrides:
SafetyGlossaryTerms (via cascade from parent), SafetyGlossaries (WHERE TenantId IS NOT NULL), RegulatoryCriteria (WHERE TenantId IS NOT NULL)

Wave 10 — Tenants + SuperUser FK resolution:
Non-default Tenants, then handle SuperUser's TenantId per whichever option you choose, then optionally delete the default tenant too

Wave 11 — Hangfire + Audit:
All hangfire_* tables, SystemAuditLogs

4. Flagged Uncertainties — Confirm Before SQL
Item	Uncertainty	Your call
SuperUser TenantId	Users.TenantId FK to Tenants — if all tenants deleted, this FK breaks. Need Option A (null + re-seed/re-insert) or Option B (keep default tenant row).	Which option?
ProviderResultCache	System-level corpus cache, no TenantId. Harmless to keep (stale cache). Safe to wipe if you want truly clean state.	Keep or wipe?
PipelineVersions / PipelineChangeRecords	System-level append-only audit. TranslationValidationRuns has a nullable FK to PipelineVersions — runs being deleted, but the PipelineVersion rows themselves are preserved. No conflict.	Keep (already intended) — just confirming
CustomerUsageReportStates	One-row-ever system singleton. No TenantId.	Keep (system)
Roles other than SuperUser	Admin, Supervisor, Operator, Finance, OfficeStaff etc. — these are seeded system roles. If deleted, the seeder would need to re-run to restore them. Are you doing a full re-seed after the wipe, or just wiping tenant data and leaving system roles intact?	Delete all non-SuperUser roles, or keep seeded roles?
AiUsageLogs / AiUsageSummaries query filter	These are TenantEntity but do NOT appear in ApplicationDbContext's HasQueryFilter block. They might have filters configured in the ToolboxTalks module's own config section, or rely on explicit .Where(). Doesn't affect the reset SQL (we're deleting all rows from those tables) — just flagging the anomaly.	No decision needed
Hangfire schema	Assumed tables are in the default public schema with hangfire_ prefix. Confirm: SELECT table_name FROM information_schema.tables WHERE table_name LIKE 'hangfire%' against the dev DB.	Verify prefix/schema
Once you confirm the SuperUser TenantId option and whether to keep or delete non-SuperUser seeded roles, I can generate the complete ordered SQL script.