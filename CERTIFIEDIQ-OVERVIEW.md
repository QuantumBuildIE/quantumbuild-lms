# CertifiedIQ

## What is it?

CertifiedIQ (built on the QuantumBuild LMS platform) is a multi-tenant Learning Management System purpose-built for workplace safety training and compliance. Its core product is **Toolbox Talks** — short, video-based safety training modules paired with quizzes, digital sign-off, and certificate generation — plus the surrounding admin, scheduling, and reporting infrastructure needed to run a compliance training program at organizational scale.

It is a modular monolith (ASP.NET Core / Next.js) designed to serve many separate customer organizations (tenants) from one codebase, each with its own employees, sites, content, and configuration.

## What problem does it solve?

Workplaces across regulated and safety-sensitive industries are required to deliver, track, and prove recurring safety training — but the manual reality is usually a mess of spreadsheets, paper sign-off sheets, and PDFs that are hard to audit and easy to lose. CertifiedIQ solves this by:

- Replacing paper toolbox talks with trackable digital training that records who completed what, when, where (via geolocation), and with what score
- Automating the compliance lifecycle — scheduling, reminders, overdue tracking, and refresher scheduling — so nothing silently lapses
- Producing audit-ready evidence: signed completions, generated certificates, and (for multi-language workforces) formally validated translations
- Giving supervisors and admins live visibility into who is compliant, who is overdue, and where the gaps are, instead of reconstructing that picture after the fact

## Who would use it?

- **Employees/operators** — complete assigned safety talks and courses, take quizzes, sign off, and download their certificates
- **Supervisors** — manage a team of operators, view team-level compliance (skills matrix, reports), and schedule training for their group
- **Admins** — create and manage training content, schedules, courses, sites, employees, and reports for their organization (tenant)
- **Compliance/Learnings admins** — manage regulatory mappings, sector-specific compliance checklists, and translation validation for regulated content
- **SuperUsers (platform operators)** — administer the platform across all tenants, including regulatory content ingestion and cross-tenant usage monitoring

Target sectors are broad and configurable per tenant: construction, manufacturing, mining, transport, food & hospitality, healthcare, homecare, and any other workplace with safety obligations.

## What can you do with it?

- **Create training content** manually, or generate it with AI from an uploaded video or PDF (auto-extracted sections, AI-written quiz questions, AI-generated slideshows)
- **Bundle talks into courses** with ordered, sequential completion requirements
- **Schedule and assign** training — one-time or recurring — to individuals or groups, with automatic overdue tracking and reminders
- **Deliver training to employees** with video watch-tracking (anti-skip), sequential section reading, quizzes with pass thresholds and retries, and signature + geolocation capture on completion
- **Automatically issue certificates** and schedule refreshers on a defined interval
- **Translate content into multiple languages** and run it through a multi-round AI back-translation consensus engine that scores accuracy and flags safety-critical mismatches, producing a formal audit report
- **Run compliance and workforce reports** — completion records, overdue lists, compliance-by-department, and a skills matrix (employees × required training), all exportable to Excel/PDF
- **Manage regulatory alignment** — ingest regulatory documents, map approved requirements to specific training content, and generate inspection-readiness reports per sector
- **Administer the organization** — sites, employees, companies, users, roles/permissions, and supervisor–operator team structures, with bulk employee import

## What makes it different?

- **Safety-first, not generic LMS** — the whole workflow (glossary-aware translation, safety-critical classification, regulatory mapping) is built around the specific stakes of safety and compliance training, not repurposed corporate e-learning software
- **AI content generation with a human-reviewable trail** — video/PDF-to-course generation is fast, but nothing ships without going through review, and translations go through a genuine multi-provider consensus + audit process rather than a single machine-translation pass
- **Real multi-tenancy with role-scoped visibility** — one platform serves many separate organizations, and reporting/data access is automatically scoped by role (SuperUser/Admin see everything in-tenant, Supervisors see their team, Operators see their own record)
- **Compliance evidence is a first-class output**, not an afterthought — signed completions, certificates, and translation validation audit reports are designed to hold up to inspection, not just to mark a training item "done"

## The bottom line

CertifiedIQ turns workplace safety training from a paperwork obligation into a trackable, auditable, and largely automated system — so organizations can prove compliance instead of hoping their records hold up, while giving employees a straightforward way to complete and demonstrate their training.

## A brief note on the technology

CertifiedIQ is built as a modular monolith: an ASP.NET Core 9 / Entity Framework Core backend with PostgreSQL, and a Next.js 16 / React 19 frontend. Background processing (scheduling, reminders, AI content generation, translation validation) runs through Hangfire, with SignalR pushing real-time progress to the UI. AI capabilities are powered by Anthropic's Claude models (content generation, quiz generation, translation, regulatory scoring) alongside ElevenLabs for transcription and DeepL/Gemini for back-translation consensus. Certificates and audit reports are generated as PDFs with QuestPDF, and all media (video, PDFs, subtitles, certificates) is stored on Cloudflare R2.
