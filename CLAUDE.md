Archived notes 1-89 are in CLAUDE-archive.md

# QuantumBuild LMS

A multi-tenant Learning Management System for workplace safety training and compliance, built with a modular monolith architecture.

---

## Project Overview

### Business Context

- **Primary Use:** Toolbox Talks — video-based safety training with quizzes, certificates, and compliance tracking
- **Sectors:** Any industry with a workplace — construction, manufacturing, mining, transport, food & hospitality, healthcare, homecare, and others. Multi-tenant architecture means each tenant configures their own sector context.
- **Scale:** Multi-language support, AI-generated content, subtitle processing, course management
- **Key Workflows:** Talk creation (manual + AI-generated), scheduling & assignment, employee completion with signature, quiz assessment, certificate generation, refresher scheduling, translation validation (TransVal)

### Currently Implemented

- **Toolbox Talks Module** — Full training lifecycle: content creation, AI generation, scheduling, assignment, completion, certificates, courses, reports
- **Admin Module (Core)** — Sites, Employees, Companies, Users management
- **Supervisor-Operator Assignments** — Supervisors manage their team of operators; reports auto-scoped by role
- **Authentication & Authorization** — JWT with permission-based policies, role-based report scoping
- **Dashboard** — Module selector
- **Subtitle Processing** — Video transcription (ElevenLabs) + translation (Claude API) to SRT files
- **Content Translation** — AI-powered translation of sections, quizzes, slideshows, email templates
- **Certificate Generation** — PDF certificates for talk and course completions
- **Translation Validation (TransVal)** — Multi-round back-translation consensus engine with safety classification, glossary verification, reviewer workflow, and audit PDF reports
- **Background Jobs** — Hangfire for scheduling, reminders, overdue tracking, content generation, translation validation

---

## Technology Stack

### Backend

| Technology            | Version | Purpose                                                                  |
| --------------------- | ------- | ------------------------------------------------------------------------ |
| ASP.NET Core          | 9.0     | Web API Framework                                                        |
| Entity Framework Core | 9.0     | ORM                                                                      |
| PostgreSQL            | Latest  | Database                                                                 |
| ASP.NET Identity      | 9.0     | Authentication                                                           |
| FluentValidation      | Latest  | Request validation                                                       |
| Hangfire              | Latest  | Background job processing                                                |
| SignalR               | Latest  | Real-time progress updates                                               |
| QuestPDF              | Latest  | Certificate & validation report PDF generation                           |
| ClosedXML             | 0.105.0 | Excel export (Skills Matrix)                                             |
| Cloudflare R2         | —       | File storage (videos, PDFs, subtitles, certificates, validation reports) |

### Frontend

| Technology         | Version | Purpose                                |
| ------------------ | ------- | -------------------------------------- |
| Next.js            | 16.0.10 | React framework (App Router)           |
| React              | 19.2.1  | UI library                             |
| TailwindCSS        | 4.x     | Styling                                |
| shadcn/ui          | Latest  | UI component library                   |
| TanStack Query     | 5.90.12 | Data fetching & caching                |
| React Hook Form    | 7.68.0  | Form handling                          |
| Zod                | 4.2.1   | Schema validation                      |
| Recharts           | 3.6.0   | Charts & analytics                     |
| Axios              | 1.13.2  | HTTP client                            |
| date-fns           | 4.1.0   | Date utilities                         |
| @dnd-kit           | Latest  | Drag-and-drop (course item reordering) |
| @microsoft/signalr | 10.0.0  | Real-time hub client                   |

---

## Solution Structure

```
quantumbuild-lms/
├── src/
│   ├── Core/                                    # Shared across all modules
│   │   ├── QuantumBuild.Core.Domain/            # Shared entities, base classes
│   │   │   ├── Common/                          # BaseEntity, TenantEntity
│   │   │   └── Entities/                        # Tenant, User, Role, Permission, Site, Employee, Company, Contact, SupervisorAssignment
│   │   ├── QuantumBuild.Core.Application/       # Shared interfaces, models, DTOs
│   │   │   ├── Abstractions/Email/              # IEmailProvider, EmailMessage
│   │   │   ├── DTOs/Auth/                       # Auth DTOs
│   │   │   ├── Features/                        # Service classes per feature (Companies, Contacts, Employees, etc.)
│   │   │   ├── Interfaces/                      # ICurrentUserService, ICoreDbContext, IAuthService, IEmailService
│   │   │   └── Models/                          # PaginatedList, Result
│   │   └── QuantumBuild.Core.Infrastructure/    # Shared EF configurations, Identity, Seeding
│   │       ├── Data/                            # ApplicationDbContext, Entity Configurations
│   │       ├── Identity/                        # AuthService, Permissions, PermissionHandler
│   │       ├── Persistence/                     # DataSeeder
│   │       ├── Repositories/                    # TenantRepository
│   │       └── Services/                        # CurrentUserService, EmailService
│   │
│   ├── Modules/
│   │   └── ToolboxTalks/                        # Toolbox Talks Module
│   │       ├── QuantumBuild.Modules.ToolboxTalks.Domain/
│   │       │   ├── Entities/                    # 36 entities (includes TransVal, Regulatory)
│   │       │   └── Enums/                       # 24 enums (includes TransVal, Regulatory)
│   │       ├── QuantumBuild.Modules.ToolboxTalks.Application/
│   │       │   ├── Abstractions/                # Pdf, Storage, Subtitles, Translations, Validation interfaces
│   │       │   ├── Commands/                    # CQRS commands (17 command handlers)
│   │       │   ├── Queries/                     # CQRS queries (12 query handlers)
│   │       │   ├── Features/                    # Certificates, CourseAssignments, Courses
│   │       │   ├── DTOs/                        # Data transfer objects
│   │       │   ├── Services/                    # Business logic services
│   │       │   └── Common/Interfaces/           # IToolboxTalksDbContext
│   │       └── QuantumBuild.Modules.ToolboxTalks.Infrastructure/
│   │           ├── Configuration/               # R2StorageSettings, SubtitleProcessingSettings
│   │           ├── Hubs/                        # SignalR hubs (ContentGeneration, SubtitleProcessing, TranslationValidation)
│   │           ├── Jobs/                        # Hangfire background jobs (12 jobs)
│   │           ├── Persistence/                 # DbContext, Entity Configurations, Seed Data
│   │           └── Services/                    # AI, Pdf, Storage, Subtitles, Translations, Validation
│   │
│   └── QuantumBuild.API/                        # Single API entry point
│       ├── Controllers/                         # 15 API controllers
│       └── Program.cs                           # Service registration
│
└── web/                                         # Next.js Frontend
    └── src/
        ├── app/                                 # App Router pages
        │   ├── login/                           # Login page
        │   ├── auth/set-password/               # Set password page
        │   └── (authenticated)/                 # Protected routes
        │       ├── dashboard/                   # Module selector
        │       ├── toolbox-talks/               # Employee-facing toolbox talks
        │       ├── admin/                       # Admin pages
        │       │   ├── sites, employees, etc.   # Core admin
        │       │   └── toolbox-talks/           # Admin toolbox talks management
        │       └── profile/                     # User profile
        ├── components/
        │   ├── ui/                              # shadcn/ui components (31 components)
        │   ├── shared/                          # DataTable, DeleteConfirmationDialog, ExportButtons, PendingTrainingBanner
        │   ├── layout/                          # TopNav
        │   ├── admin/                           # Admin-specific components (14)
        │   └── profile/                         # ChangePasswordForm
        ├── features/
        │   └── toolbox-talks/                   # Toolbox talks feature components (32)
        ├── hooks/                               # Custom hooks (use-geolocation)
        ├── lib/
        │   ├── api/                             # Axios client, admin API, toolbox-talks API
        │   ├── auth/                            # Auth context, hooks, utilities
        │   ├── providers.tsx                    # App providers
        │   ├── query-client.ts                  # TanStack Query client
        │   └── utils.ts                         # Utilities
        └── types/                               # TypeScript type definitions (auth, admin, modules, toolbox-talks)
```

---

## Backend API Endpoints

### Authentication (`/api/auth`)

| Method | Endpoint         | Description                         | Auth |
| ------ | ---------------- | ----------------------------------- | ---- |
| POST   | `/login`         | Authenticate and get JWT tokens     | No   |
| POST   | `/register`      | Register new user                   | No   |
| POST   | `/refresh-token` | Refresh expired access token        | No   |
| POST   | `/revoke-token`  | Logout (revoke refresh token)       | Yes  |
| GET    | `/me`            | Get current user info + permissions | Yes  |
| POST   | `/set-password`  | Set password from invitation link   | No   |

### Bulk Employee Import (`/api/employees/bulk-import`)

| Verb | Route                      | Description                                                                          | Policy               |
| ---- | -------------------------- | ------------------------------------------------------------------------------------ | -------------------- |
| POST | `/`                        | Upload + validate; create session, return validation summary                         | Core.ManageEmployees |
| POST | `/{sessionId}/confirm`     | Enqueue the processing job; stuck-session recovery if `ProcessingStartedAt` > 30 min | Core.ManageEmployees |
| GET  | `/{sessionId}`             | Session status + per-row results (for polling)                                       | Core.ManageEmployees |
| GET  | `/{sessionId}/failed-rows` | CSV of failed rows (correct and re-upload)                                           | Core.ManageEmployees |
| GET  | `/template`                | Static CSV template                                                                  | Core.ManageEmployees |

SuperUser may target another tenant via the `X-Tenant-Id` header; non-SuperUsers always operate on their own tenant. Uses `Result<T>` envelope on all JSON responses.

### Customer Usage Report (`/api/monitoring`)

| Verb | Route                                      | Description                                | Policy    |
| ---- | ------------------------------------------ | ------------------------------------------ | --------- |
| GET  | `/customer-usage`                          | List all tenants with metrics + risk flags | SuperUser |
| PUT  | `/customer-usage/{tenantId}/mark-reviewed` | Mark tenant reviewed                       | SuperUser |

Returns DTOs directly — frontend reads `response.data` (not `response.data.data`).

### Regulatory Browse (`/api/regulatory/browse`)

| Verb | Route     | Description                                                                                     | Policy          |
| ---- | --------- | ----------------------------------------------------------------------------------------------- | --------------- |
| GET  | `/browse` | Regulatory bodies → documents → Approved-only requirements, filtered to caller's tenant sectors | Learnings.Admin |

Lives on a dedicated `RegulatoryBrowseController` (class-gated `Learnings.Admin`), separate from `RegulatoryIngestionController` (class-gated `Tenant.Manage`). See note 24.

### Tenant Sectors — POST permission broadened

`POST /api/tenants/{id}/sectors` now allows `Learnings.Admin` on own tenant OR SuperUser. DELETE and PUT remain `Tenant.Manage` only. The tenant-scope check is an early `Forbid()` at the top of the action.

### Core Module

#### Users (`/api/users`)

| Method | Endpoint              | Description               | Permission       |
| ------ | --------------------- | ------------------------- | ---------------- |
| GET    | `/`                   | List users (paginated)    | Core.ManageUsers |
| GET    | `/{id}`               | Get user by ID            | Core.ManageUsers |
| POST   | `/`                   | Create user               | Core.ManageUsers |
| PUT    | `/{id}`               | Update user               | Core.ManageUsers |
| DELETE | `/{id}`               | Delete user               | Core.ManageUsers |
| PUT    | `/{id}/toggle-active` | Toggle user active status | Core.ManageUsers |

#### Roles (`/api/roles`)

| Method | Endpoint       | Description          | Permission       |
| ------ | -------------- | -------------------- | ---------------- |
| GET    | `/`            | List all roles       | Core.ManageRoles |
| GET    | `/{id}`        | Get role by ID       | Core.ManageRoles |
| GET    | `/permissions` | List all permissions | Core.ManageRoles |

#### Sites (`/api/sites`)

| Method | Endpoint | Description            | Permission       |
| ------ | -------- | ---------------------- | ---------------- |
| GET    | `/`      | List sites (paginated) | Core.ManageSites |
| GET    | `/{id}`  | Get site by ID         | Core.ManageSites |
| POST   | `/`      | Create site            | Core.ManageSites |
| PUT    | `/{id}`  | Update site            | Core.ManageSites |
| DELETE | `/{id}`  | Delete site (soft)     | Core.ManageSites |

#### Employees (`/api/employees`)

| Method | Endpoint | Description                                                      | Permission           |
| ------ | -------- | ---------------------------------------------------------------- | -------------------- |
| GET    | `/`      | List employees (paginated)                                       | Core.ManageEmployees |
| GET    | `/{id}`  | Get employee by ID                                               | Core.ManageEmployees |
| POST   | `/`      | Create employee                                                  | Core.ManageEmployees |
| PUT    | `/{id}`  | Update employee                                                  | Core.ManageEmployees |
| DELETE | `/{id}`  | Delete employee (soft, checks for active supervisor assignments) | Core.ManageEmployees |

#### Supervisor Assignments (`/api/employees`)

| Method | Endpoint                                 | Description                                                | Permission     |
| ------ | ---------------------------------------- | ---------------------------------------------------------- | -------------- |
| GET    | `/{supervisorId}/operators`              | List operators assigned to a supervisor                    | Learnings.View |
| GET    | `/{supervisorId}/operators/available`    | List employees available for assignment                    | Learnings.View |
| POST   | `/{supervisorId}/operators`              | Assign operator to supervisor (restore-on-reassign)        | Learnings.View |
| DELETE | `/{supervisorId}/operators/{operatorId}` | Unassign operator from supervisor (soft delete)            | Learnings.View |
| GET    | `/my-operators`                          | List current supervisor's operators (uses JWT employee_id) | Learnings.View |

> **Note:** All supervisor assignment endpoints use `Learnings.View` as the auth policy, with business-level scoping ensuring supervisors can only manage their own assignments.

#### Companies (`/api/companies`)

| Method | Endpoint | Description                | Permission           |
| ------ | -------- | -------------------------- | -------------------- |
| GET    | `/`      | List companies (paginated) | Core.ManageCompanies |
| GET    | `/{id}`  | Get company by ID          | Core.ManageCompanies |
| POST   | `/`      | Create company             | Core.ManageCompanies |
| PUT    | `/{id}`  | Update company             | Core.ManageCompanies |
| DELETE | `/{id}`  | Delete company (soft)      | Core.ManageCompanies |

#### Contacts (`/api/contacts`)

| Method | Endpoint | Description               | Permission           |
| ------ | -------- | ------------------------- | -------------------- |
| GET    | `/`      | List contacts (paginated) | Core.ManageCompanies |
| GET    | `/{id}`  | Get contact by ID         | Core.ManageCompanies |
| POST   | `/`      | Create contact            | Core.ManageCompanies |
| PUT    | `/{id}`  | Update contact            | Core.ManageCompanies |
| DELETE | `/{id}`  | Delete contact (soft)     | Core.ManageCompanies |

### Toolbox Talks Module

#### Toolbox Talks — Admin (`/api/toolbox-talks`)

| Method | Endpoint                      | Description                                            | Permission          |
| ------ | ----------------------------- | ------------------------------------------------------ | ------------------- |
| GET    | `/`                           | List toolbox talks (paginated, searchable, filterable) | ToolboxTalks.View   |
| GET    | `/{id}`                       | Get talk by ID with sections and questions             | ToolboxTalks.View   |
| GET    | `/{id}/preview`               | Preview talk as employee sees it                       | ToolboxTalks.View   |
| GET    | `/{id}/preview/slides`        | Get slides for preview                                 | ToolboxTalks.View   |
| GET    | `/{id}/slideshow-html`        | Get AI-generated HTML slideshow                        | ToolboxTalks.View   |
| POST   | `/`                           | Create a new toolbox talk                              | ToolboxTalks.Create |
| PUT    | `/{id}`                       | Update a toolbox talk                                  | ToolboxTalks.Edit   |
| DELETE | `/{id}`                       | Delete a toolbox talk (soft)                           | ToolboxTalks.Delete |
| GET    | `/dashboard`                  | Dashboard KPIs and statistics                          | ToolboxTalks.View   |
| GET    | `/settings`                   | Get tenant settings                                    | ToolboxTalks.View   |
| PUT    | `/settings`                   | Update tenant settings                                 | ToolboxTalks.Admin  |
| POST   | `/{id}/extract-content`       | Extract content from video/PDF for AI generation       | ToolboxTalks.Admin  |
| POST   | `/{id}/check-duplicate`       | Check file deduplication                               | ToolboxTalks.Admin  |
| POST   | `/{id}/reuse-content`         | Reuse content from another talk                        | ToolboxTalks.Admin  |
| POST   | `/{id}/update-file-hash`      | Update file hash for dedup tracking                    | ToolboxTalks.Admin  |
| POST   | `/{id}/generate`              | Start AI content generation (Hangfire + SignalR)       | ToolboxTalks.Admin  |
| POST   | `/{id}/generate-slides`       | Generate AI HTML slideshow from PDF                    | ToolboxTalks.Edit   |
| POST   | `/{id}/smart-generate`        | Smart content generation (dedup + AI)                  | ToolboxTalks.Edit   |
| POST   | `/{id}/translations/generate` | Generate content translations                          | ToolboxTalks.Admin  |
| GET    | `/{id}/translations`          | Get existing translations                              | ToolboxTalks.View   |

#### Toolbox Talks — Reports (`/api/toolbox-talks/reports`)

| Method | Endpoint                | Description                                                  | Permission        |
| ------ | ----------------------- | ------------------------------------------------------------ | ----------------- |
| GET    | `/compliance`           | Compliance report by department/talk                         | ToolboxTalks.View |
| GET    | `/overdue`              | Overdue assignments list                                     | ToolboxTalks.View |
| GET    | `/completions`          | Detailed completion records (paginated)                      | ToolboxTalks.View |
| GET    | `/skills-matrix`        | Skills matrix: employees × learnings grid with cell statuses | ToolboxTalks.View |
| GET    | `/overdue/export`       | Export overdue report as Excel                               | ToolboxTalks.View |
| GET    | `/completions/export`   | Export completions as Excel                                  | ToolboxTalks.View |
| GET    | `/compliance/export`    | Export compliance as PDF                                     | ToolboxTalks.View |
| GET    | `/skills-matrix/export` | Export skills matrix as colour-coded Excel (ClosedXML)       | ToolboxTalks.View |

#### Toolbox Talks — Certificates (`/api/toolbox-talks/certificates`)

| Method | Endpoint                    | Description                           | Permission        |
| ------ | --------------------------- | ------------------------------------- | ----------------- |
| GET    | `/report`                   | Certificate report with summary stats | ToolboxTalks.View |
| GET    | `/by-employee/{employeeId}` | Certificates for a specific employee  | ToolboxTalks.View |
| GET    | `/{id}/download`            | Download certificate PDF (admin)      | ToolboxTalks.View |

#### My Toolbox Talks — Employee Portal (`/api/my/toolbox-talks`)

| Method | Endpoint                          | Description                                    | Permission    |
| ------ | --------------------------------- | ---------------------------------------------- | ------------- |
| GET    | `/`                               | List assigned talks (paginated, filterable)    | Authenticated |
| GET    | `/{id}`                           | Get assigned talk with full content            | Authenticated |
| POST   | `/{id}/start`                     | Start a toolbox talk (captures geolocation)    | Authenticated |
| POST   | `/{id}/sections/{sectionId}/read` | Mark a section as read                         | Authenticated |
| POST   | `/{id}/quiz/submit`               | Submit quiz answers                            | Authenticated |
| POST   | `/{id}/video-progress`            | Update video watch progress                    | Authenticated |
| POST   | `/{id}/reset-video-progress`      | Reset video progress                           | Authenticated |
| POST   | `/{id}/complete`                  | Complete talk with signature + geolocation     | Authenticated |
| GET    | `/pending`                        | Pending talks                                  | Authenticated |
| GET    | `/in-progress`                    | In-progress talks                              | Authenticated |
| GET    | `/overdue`                        | Overdue talks                                  | Authenticated |
| GET    | `/completed`                      | Completed talks                                | Authenticated |
| GET    | `/summary`                        | Summary counts (pending, in-progress, overdue) | Authenticated |
| GET    | `/{id}/subtitles/status`          | Subtitle processing status                     | Authenticated |
| GET    | `/{id}/subtitles/{languageCode}`  | Get subtitle file (SRT/WebVTT)                 | Authenticated |
| GET    | `/{id}/slides`                    | Get slides with optional translation           | Authenticated |
| GET    | `/{id}/slideshow`                 | Get HTML slideshow with optional translation   | Authenticated |
| GET    | `/courses`                        | Course assignments for current employee        | Authenticated |
| GET    | `/courses/{id}`                   | Specific course assignment                     | Authenticated |
| GET    | `/certificates`                   | Employee's certificates                        | Authenticated |
| GET    | `/certificates/{id}/download`     | Download certificate PDF                       | Authenticated |

#### Scheduled Talks (`/api/toolbox-talks/assigned`)

| Method | Endpoint                    | Description                                  | Permission            |
| ------ | --------------------------- | -------------------------------------------- | --------------------- |
| GET    | `/`                         | List all assignments (paginated, filterable) | ToolboxTalks.View     |
| GET    | `/{id}`                     | Get assignment by ID                         | ToolboxTalks.View     |
| GET    | `/by-employee/{employeeId}` | Assignments for employee                     | ToolboxTalks.View     |
| GET    | `/overdue`                  | Overdue assignments                          | ToolboxTalks.View     |
| GET    | `/pending`                  | Pending assignments                          | ToolboxTalks.View     |
| GET    | `/in-progress`              | In-progress assignments                      | ToolboxTalks.View     |
| GET    | `/completed`                | Completed assignments                        | ToolboxTalks.View     |
| POST   | `/{id}/reminder`            | Send reminder                                | Learnings.Schedule |
| DELETE | `/{id}`                     | Cancel assignment                            | Learnings.Schedule |

#### Schedules (`/api/toolbox-talks/schedules`)

| Method | Endpoint        | Description                            | Permission            |
| ------ | --------------- | -------------------------------------- | --------------------- |
| GET    | `/`             | List schedules (paginated, filterable) | ToolboxTalks.View     |
| GET    | `/{id}`         | Get schedule with assignments          | ToolboxTalks.View     |
| POST   | `/`             | Create schedule                        | Learnings.Schedule |
| PUT    | `/{id}`         | Update schedule                        | Learnings.Schedule |
| DELETE | `/{id}`         | Cancel schedule                        | Learnings.Schedule |
| POST   | `/{id}/process` | Process schedule to create assignments | Learnings.Schedule |

#### Courses (`/api/toolbox-talks/courses`)

| Method | Endpoint               | Description                            | Permission          |
| ------ | ---------------------- | -------------------------------------- | ------------------- |
| GET    | `/`                    | List courses (searchable, filterable)  | ToolboxTalks.View   |
| GET    | `/{id}`                | Get course with items and translations | ToolboxTalks.View   |
| POST   | `/`                    | Create course                          | ToolboxTalks.Create |
| PUT    | `/{id}`                | Update course                          | ToolboxTalks.Edit   |
| DELETE | `/{id}`                | Delete course (soft)                   | ToolboxTalks.Delete |
| POST   | `/{id}/items`          | Add talk to course                     | ToolboxTalks.Edit   |
| DELETE | `/{id}/items/{talkId}` | Remove talk from course                | ToolboxTalks.Edit   |
| PUT    | `/{id}/items`          | Reorder/bulk update items              | ToolboxTalks.Edit   |

#### Course Assignments (`/api/toolbox-talks/course-assignments`)

| Method | Endpoint                | Description                                | Permission          |
| ------ | ----------------------- | ------------------------------------------ | ------------------- |
| POST   | `/preview`              | Preview assignment (shows completed talks) | ToolboxTalks.View   |
| POST   | `/`                     | Assign course to employees                 | ToolboxTalks.Create |
| GET    | `/by-course/{courseId}` | Assignments for a course                   | ToolboxTalks.View   |
| GET    | `/{id}`                 | Get course assignment details              | ToolboxTalks.View   |
| DELETE | `/{id}`                 | Delete course assignment                   | ToolboxTalks.Delete |

#### Subtitle Processing (`/api/toolbox-talks/{toolboxTalkId}/subtitles`)

| Method | Endpoint          | Description                                             | Permission        |
| ------ | ----------------- | ------------------------------------------------------- | ----------------- |
| POST   | `/process`        | Start subtitle processing (transcription + translation) | ToolboxTalks.Edit |
| GET    | `/status`         | Get processing status                                   | ToolboxTalks.View |
| POST   | `/cancel`         | Cancel active processing                                | ToolboxTalks.Edit |
| POST   | `/retry`          | Retry failed translations                               | ToolboxTalks.Edit |
| GET    | `/{languageCode}` | Download subtitle file (SRT/WebVTT)                     | ToolboxTalks.View |

#### File Management (`/api/toolbox-talks/{toolboxTalkId}`)

| Method | Endpoint     | Description              | Permission        |
| ------ | ------------ | ------------------------ | ----------------- |
| POST   | `/video`     | Upload video (max 500MB) | ToolboxTalks.Edit |
| POST   | `/pdf`       | Upload PDF (max 50MB)    | ToolboxTalks.Edit |
| PUT    | `/video-url` | Set external video URL   | ToolboxTalks.Edit |
| DELETE | `/video`     | Delete video             | ToolboxTalks.Edit |
| DELETE | `/pdf`       | Delete PDF               | ToolboxTalks.Edit |
| DELETE | `/files`     | Delete all files         | ToolboxTalks.Edit |

#### Translation Validation (`/api/toolbox-talks/{talkId}/validation`)

| Method | Endpoint                              | Description                      | Permission         |
| ------ | ------------------------------------- | -------------------------------- | ------------------ |
| POST   | `/validate`                           | Start new validation run         | ToolboxTalks.Admin |
| GET    | `/runs`                               | List validation runs (paginated) | ToolboxTalks.View  |
| GET    | `/runs/{runId}`                       | Get run with all results         | ToolboxTalks.View  |
| PUT    | `/runs/{runId}/sections/{idx}/accept` | Reviewer accepts section         | ToolboxTalks.Admin |
| PUT    | `/runs/{runId}/sections/{idx}/reject` | Reviewer rejects section         | ToolboxTalks.Admin |
| PUT    | `/runs/{runId}/sections/{idx}/edit`   | Reviewer edits & re-validates    | ToolboxTalks.Admin |
| POST   | `/runs/{runId}/sections/{idx}/retry`  | Retry section validation         | ToolboxTalks.Admin |
| GET    | `/runs/{runId}/report`                | Download audit report PDF        | ToolboxTalks.View  |
| POST   | `/runs/{runId}/report/generate`       | Generate audit report PDF        | ToolboxTalks.Admin |
| DELETE | `/runs/{runId}`                       | Soft-delete validation run       | ToolboxTalks.Admin |

#### Sectors (`/api/toolbox-talks/sectors`)

| Method | Endpoint | Description                         | Permission    |
| ------ | -------- | ----------------------------------- | ------------- |
| GET    | `/`      | List all active system-wide sectors | Authenticated |

#### Tenant Sectors (`/api/tenants/{tenantId}/sectors`)

| Method | Endpoint                  | Description                                                               | Permission    |
| ------ | ------------------------- | ------------------------------------------------------------------------- | ------------- |
| GET    | `/`                       | List tenant's sectors (tenant ID guard: own tenant only unless SuperUser) | Authenticated |
| POST   | `/`                       | Assign sector to tenant                                                   | Tenant.Manage |
| DELETE | `/{sectorId}`             | Remove sector from tenant (400 if last sector)                            | Tenant.Manage |
| PUT    | `/{sectorId}/set-default` | Set sector as tenant default                                              | Tenant.Manage |

#### Regulatory Scoring (`/api/toolbox-talks/validation-runs/{runId}`)

| Method | Endpoint                    | Description                            | Permission      |
| ------ | --------------------------- | -------------------------------------- | --------------- |
| POST   | `/regulatory-score`         | Trigger regulatory scoring run         | Learnings.Admin |
| GET    | `/regulatory-score/history` | Get score history for a validation run | Learnings.View  |

#### Course Validation (`/api/toolbox-talks/courses/{courseId}`)

| Method | Endpoint                   | Description                            | Permission        |
| ------ | -------------------------- | -------------------------------------- | ----------------- |
| GET    | `/validation-runs`         | List validation runs for a course      | ToolboxTalks.View |
| GET    | `/validation/runs/{runId}` | Get course-level validation run detail | ToolboxTalks.View |

#### Safety Glossary (`/api/toolbox-talks/glossaries`)

| Method | Endpoint               | Description                                          | Permission         |
| ------ | ---------------------- | ---------------------------------------------------- | ------------------ |
| GET    | `/`                    | List glossaries (system defaults + tenant overrides) | ToolboxTalks.View  |
| GET    | `/{id}`                | Get glossary with terms                              | ToolboxTalks.View  |
| POST   | `/`                    | Create tenant glossary                               | ToolboxTalks.Admin |
| PUT    | `/{id}`                | Update glossary                                      | ToolboxTalks.Admin |
| DELETE | `/{id}`                | Delete glossary (tenant only)                        | ToolboxTalks.Admin |
| POST   | `/{id}/terms`          | Add term to glossary                                 | ToolboxTalks.Admin |
| PUT    | `/{id}/terms/{termId}` | Update glossary term                                 | ToolboxTalks.Admin |
| DELETE | `/{id}/terms/{termId}` | Delete glossary term                                 | ToolboxTalks.Admin |

#### Regulatory Ingestion (`/api/regulatory`)

| Method | Endpoint                                     | Description                                               | Permission    |
| ------ | -------------------------------------------- | --------------------------------------------------------- | ------------- |
| GET    | `/documents`                                 | List all regulatory documents with body, profiles, counts | Tenant.Manage |
| POST   | `/documents/{documentId}/ingest`             | Start AI ingestion from document URL                      | Tenant.Manage |
| GET    | `/documents/{documentId}/ingestion-status`   | Get ingestion status and counts                           | Tenant.Manage |
| GET    | `/documents/{documentId}/draft-requirements` | List draft requirements for review                        | Tenant.Manage |
| PUT    | `/requirements/{requirementId}/approve`      | Approve draft (with optional edits)                       | Tenant.Manage |
| PUT    | `/requirements/{requirementId}/reject`       | Reject draft with notes                                   | Tenant.Manage |
| PUT    | `/requirements/{requirementId}`              | Update draft without status change                        | Tenant.Manage |
| POST   | `/documents/{documentId}/approve-all`        | Bulk approve all drafts                                   | Tenant.Manage |

#### Requirement Mappings (`/api/toolbox-talks/requirement-mappings`)

| Method | Endpoint                                      | Description                                           | Permission      |
| ------ | --------------------------------------------- | ----------------------------------------------------- | --------------- |
| GET    | `/pending`                                    | Get pending mappings summary + list                   | Learnings.Admin |
| PUT    | `/{mappingId}/confirm`                        | Confirm an AI-suggested mapping                       | Learnings.Admin |
| PUT    | `/{mappingId}/reject`                         | Reject an AI-suggested mapping                        | Learnings.Admin |
| POST   | `/confirm-all`                                | Confirm all suggested mappings for tenant             | Learnings.Admin |
| GET    | `/unconfirmed-count?toolboxTalkId=&courseId=` | Count unconfirmed mappings for content                | Learnings.Admin |
| GET    | `/compliance/{sectorKey}`                     | Get compliance checklist for sector                   | Learnings.Admin |
| POST   | `/manual`                                     | Create manual confirmed mapping (no AI)               | Learnings.Admin |
| GET    | `/content-options`                            | List published talks and courses for mapping dropdown | Learnings.Admin |
| POST   | `/compliance/{sectorKey}/generate-report`     | Generate inspection readiness report PDF              | Learnings.Admin |

---

## Frontend Pages

### Public Pages

| Path                 | Description                       |
| -------------------- | --------------------------------- |
| `/login`             | Login page                        |
| `/auth/set-password` | Set password from invitation link |

### Authenticated Pages

#### Dashboard

| Path         | Description                                     |
| ------------ | ----------------------------------------------- |
| `/dashboard` | Module selector (Toolbox Talks, Administration) |

#### Toolbox Talks — Employee Portal (`/toolbox-talks/*`)

| Path                                | Description                                                      |
| ----------------------------------- | ---------------------------------------------------------------- |
| `/toolbox-talks`                    | Employee training dashboard                                      |
| `/toolbox-talks/[id]`               | View and complete an assigned talk                               |
| `/toolbox-talks/courses/[id]`       | Course detail and progress                                       |
| `/toolbox-talks/certificates`       | View earned certificates                                         |
| `/toolbox-talks/team`               | My Team page (Supervisor only — assign/unassign operators)       |
| `/toolbox-talks/team/skills-matrix` | Skills Matrix (Supervisor view — assigned operators × learnings) |

#### Admin — Toolbox Talks (`/admin/toolbox-talks/*`)

| Path                                                   | Description                                                                                            |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------ |
| `/admin/toolbox-talks`                                 | Overview dashboard with KPIs                                                                           |
| `/admin/toolbox-talks/talks`                           | List all talks                                                                                         |
| `/admin/toolbox-talks/talks/new`                       | Create talk (6-step wizard: Input & Config → Parse → Quiz → Settings → Translate & Validate → Publish) |
| `/admin/toolbox-talks/talks/[id]`                      | View talk details                                                                                      |
| `/admin/toolbox-talks/talks/[id]/edit`                 | Edit talk                                                                                              |
| `/admin/toolbox-talks/courses`                         | List courses                                                                                           |
| `/admin/toolbox-talks/courses/new`                     | Create course                                                                                          |
| `/admin/toolbox-talks/courses/[id]/edit`               | Edit course                                                                                            |
| `/admin/toolbox-talks/schedules`                       | List schedules                                                                                         |
| `/admin/toolbox-talks/schedules/new`                   | Create schedule                                                                                        |
| `/admin/toolbox-talks/schedules/[id]`                  | View schedule details                                                                                  |
| `/admin/toolbox-talks/assignments`                     | List individual assignments                                                                            |
| `/admin/toolbox-talks/reports`                         | Reports landing                                                                                        |
| `/admin/toolbox-talks/reports/compliance`              | Compliance report                                                                                      |
| `/admin/toolbox-talks/reports/completions`             | Completion records                                                                                     |
| `/admin/toolbox-talks/reports/overdue`                 | Overdue assignments                                                                                    |
| `/admin/toolbox-talks/reports/skills-matrix`           | Skills Matrix (Admin view — all employees × learnings)                                                 |
| `/admin/toolbox-talks/certificates`                    | Certificate management                                                                                 |
| `/admin/toolbox-talks/pending-mappings`                | Pending requirement mappings review (Learnings.Admin)                                                  |
| `/admin/toolbox-talks/compliance`                      | Compliance checklist with sector tabs (Learnings.Admin)                                                |
| `/admin/toolbox-talks/settings`                        | Module settings (includes glossary management, threshold config)                                       |
| `/admin/toolbox-talks/talks/[id]/validation`           | Validation history tab (list of runs for a talk)                                                       |
| `/admin/toolbox-talks/talks/[id]/validation/[runId]`   | Validation run detail (section results, reviewer decisions, report download)                           |
| `/admin/toolbox-talks/courses/[id]/validation/[runId]` | Course-level validation run detail                                                                     |

#### Admin — Core (`/admin/*`)

| Path                         | Description                                                                                |
| ---------------------------- | ------------------------------------------------------------------------------------------ |
| `/admin`                     | Admin module home                                                                          |
| `/admin/sites`               | List sites                                                                                 |
| `/admin/sites/new`           | Create site                                                                                |
| `/admin/sites/[id]/edit`     | Edit site                                                                                  |
| `/admin/employees`           | List employees                                                                             |
| `/admin/employees/new`       | Create employee                                                                            |
| `/admin/employees/[id]`      | Employee detail view (read-only summary, certificates, assigned operators for Supervisors) |
| `/admin/employees/[id]/edit` | Edit employee                                                                              |
| `/admin/companies`           | List companies                                                                             |
| `/admin/companies/new`       | Create company                                                                             |
| `/admin/companies/[id]`      | View company with contacts                                                                 |
| `/admin/companies/[id]/edit` | Edit company                                                                               |
| `/admin/users`               | List users                                                                                 |
| `/admin/users/new`           | Create user                                                                                |
| `/admin/users/[id]/edit`     | Edit user                                                                                  |

### Admin — Core (additions)

| Route                          | Description                                                                                | Gate                 |
| ------------------------------ | ------------------------------------------------------------------------------------------ | -------------------- |
| `/admin/employees/bulk-import` | Multi-state bulk import flow: upload → validation summary → processing (polling) → results | Core.ManageEmployees |

### Admin — Monitoring (new section)

| Route                              | Description                                               | Gate      |
| ---------------------------------- | --------------------------------------------------------- | --------- |
| `/admin/monitoring/customer-usage` | Cross-tenant usage metrics with risk flags, mark-reviewed | SuperUser |

### Admin — Regulatory (replaces the old SuperUser-only section)

This section now serves two audiences. The landing page shows role-dependent cards; the sub-nav shows role-dependent tabs.

| Route                                   | Description                                                                        | Gate                                    |
| --------------------------------------- | ---------------------------------------------------------------------------------- | --------------------------------------- |
| `/admin/regulatory`                     | Landing — cards/tabs differ by role                                                | Learnings.Admin (with SuperUser bypass) |
| `/admin/regulatory/regulations`         | Read-only browse of approved regulatory data filtered to tenant sectors            | Learnings.Admin                         |
| `/admin/regulatory/compliance`          | Compliance checklist (moved from `/admin/toolbox-talks/compliance`)                | Learnings.Admin                         |
| `/admin/regulatory/mappings`            | Pending mappings (moved from `/admin/toolbox-talks/pending-mappings`)              | Learnings.Admin                         |
| `/admin/regulatory/my-sectors`          | Add-only sector self-service. Removal requires SuperUser (warning callout on page) | Learnings.Admin                         |
| `/admin/regulatory/system`              | Document list + ingestion administration (moved from `/admin/regulatory`)          | SuperUser                               |
| `/admin/regulatory/system/[documentId]` | Document detail + ingestion trigger                                                | SuperUser                               |

### Redirect stubs (legacy paths)

| Legacy path                             | Redirects to                            |
| --------------------------------------- | --------------------------------------- |
| `/admin/toolbox-talks/compliance`       | `/admin/regulatory/compliance`          |
| `/admin/toolbox-talks/pending-mappings` | `/admin/regulatory/mappings`            |
| `/admin/regulatory/[documentId]`        | `/admin/regulatory/system/[documentId]` |

#### Admin — Regulatory (SuperUser only) (`/admin/regulatory/*`)

| Path                             | Description                                                  |
| -------------------------------- | ------------------------------------------------------------ |
| `/admin/regulatory`              | Regulatory documents list with ingestion status              |
| `/admin/regulatory/[documentId]` | Document detail, ingestion trigger, draft requirement review |

#### User

| Path       | Description                      |
| ---------- | -------------------------------- |
| `/profile` | User profile and password change |

---

## Authentication & Authorization

### JWT Bearer Authentication

- Access tokens expire in 60 minutes
- Refresh tokens expire in 7 days
- Automatic token refresh on 401 responses via Axios interceptor
- "Keep me logged in" uses localStorage (persistent) vs sessionStorage (session only)

### Permissions

#### Core Module

| Permission             | Description                     |
| ---------------------- | ------------------------------- |
| `Core.ManageSites`     | Manage sites                    |
| `Core.ManageEmployees` | Manage employees                |
| `Core.ManageCompanies` | Manage companies and contacts   |
| `Core.ManageUsers`     | Manage user accounts            |
| `Core.ManageRoles`     | Manage roles and permissions    |
| `Core.Admin`           | Full core system administration |

#### Toolbox Talks Module

| Permission                 | Description                       |
| -------------------------- | --------------------------------- |
| `ToolboxTalks.View`        | View toolbox talks and reports    |
| `ToolboxTalks.Create`      | Create new toolbox talks          |
| `ToolboxTalks.Edit`        | Edit existing toolbox talks       |
| `ToolboxTalks.Delete`      | Delete toolbox talks              |
| `ToolboxTalks.Schedule`    | Manage schedules and assignments  |
| `ToolboxTalks.ViewReports` | View toolbox talk reports         |
| `ToolboxTalks.Admin`       | Full toolbox talks administration |

#### Learnings Module

| Permission           | Description                                          |
| -------------------- | ---------------------------------------------------- |
| `Learnings.View`     | View learnings, manage team assignments (Supervisor) |
| `Learnings.Schedule` | Schedule and assign learnings to team members        |

### Roles

| Role           | Description                                                                                                                                                                        |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **SuperUser**  | System-wide administration across all tenants; bypasses tenant query filters and most permission checks. Cannot be assigned through the application UI — seeded or DB-direct only. |
| **Admin**      | Full tenant administration. All permissions except `Tenant.Manage`. Includes `Learnings.Admin`.                                                                                    |
| **Operator**   | Standard learning consumer. `Learnings.View` only.                                                                                                                                 |
| **Supervisor** | Operator + scheduling. `Learnings.View` and `Learnings.Schedule`.                                                                                                                  |

Currently seeded roles: SuperUser, Admin, Operator, Supervisor. Legacy roles (Finance, OfficeStaff, SiteManager, WarehouseStaff, Operative) were deleted in migration `20260218125524_UpdateRolesPermissionsAndAddSuperUser`.

> **Note:** The `DefaultUserRole` constant is `"Operator"` (previously `"SiteManager"`). The seeder includes `CleanupSupervisorPermissionsAsync` to remove stale permissions (e.g., `Core.ManageEmployees`, `Core.ManageSites`) from existing Supervisor roles.

### Report Scoping

Reports (compliance, overdue, completions, certificates, dashboard) are auto-scoped by role via `ToolboxTalksController.ResolveScopedEmployeeIdsAsync()`:

| Role                  | Scope                   | employeeIds                         |
| --------------------- | ----------------------- | ----------------------------------- |
| **SuperUser / Admin** | All data                | `null` (no filter)                  |
| **Supervisor**        | Assigned operators only | Resolved from SupervisorAssignments |
| **Operator**          | Own data only           | Current user's EmployeeId           |

### ICurrentUserService

Now includes `EmployeeId` (`Guid?`) resolved from the JWT `employee_id` claim, in addition to existing `UserId`, `TenantId`, etc.

### Frontend Auth

The `User` TypeScript type now includes `employeeId` (`string | null`), populated from the `/api/auth/me` response.

### Navigation by Role

| Role                  | Profile Menu                               | Employee Nav Items                                                  |
| --------------------- | ------------------------------------------ | ------------------------------------------------------------------- |
| **Admin / SuperUser** | "Administration" → all admin tabs          | N/A (admin-focused)                                                 |
| **Supervisor**        | "Training Management" → Learnings tab only | My Learnings, My Certificates, My Team, Skills Matrix, Team Reports |
| **Operator**          | No admin access                            | My Learnings, My Certificates                                       |

### Frontend Guards

- `admin/employees/layout.tsx` — requires `Core.ManageEmployees` or `Core.ManageUsers`
- `admin/users/layout.tsx` — requires `Core.ManageUsers`
- Admin layout gate excludes `Learnings.View`-only users (Operators) from accessing admin
- Admin nav tabs filtered by individual permissions per role

---

## Business Workflows

### Toolbox Talk Content Workflow

```
Draft → Processing (AI generation) → ReadyForReview → Published
```

**Content Creation Options:**

1. **Manual** — Admin writes sections and quiz questions directly
2. **AI from Video** — Upload video → transcribe → AI generates sections + questions
3. **AI from PDF** — Upload PDF → extract text → AI generates sections + questions
4. **Smart Generate** — Checks for duplicate content first, reuses if found, generates remaining

### Talk Assignment Workflow

```
Schedule Created → Schedule Processed → ScheduledTalk (Pending) → InProgress → Completed
                                                                              → Overdue
                                                                              → Cancelled
```

### Employee Completion Flow

1. Employee opens assigned talk
2. **Start** — Records geolocation (optional), marks as InProgress
3. **Watch video** — Must watch minimum % (default 90%, anti-skip tracking)
4. **Read sections** — Must acknowledge each section sequentially
5. **View slideshow** — If PDF slides are attached
6. **Take quiz** — If required, must pass (default 80%); can retry (rewatches video first)
7. **Sign** — Captures signature + geolocation
8. **Complete** — Generates certificate (if enabled), schedules refresher (if enabled)

### Course Workflow

```
Course Created → Course Items Added (ordered talks) → Course Assigned to Employees
→ ScheduledTalks created per item → Employee completes talks sequentially → Course Complete
```

### Refresher Scheduling

- Talks/Courses can have `RequiresRefresher = true` with `RefresherIntervalMonths`
- On completion, system schedules a refresher ScheduledTalk/CourseAssignment
- Reminders sent at 2 weeks and 1 week before due date

### Translation Validation (TransVal) Workflow

```
Start Validation → Back-translate sections (multi-provider consensus) → Score & classify → Generate results → Audit report
```

**Validation Process per Section:**

1. **Safety classification** — Scan for glossary terms + regex patterns (prohibition, emergency, hazard)
2. **Threshold adjustment** — Bump pass threshold by `SafetyCriticalBump` (default +10) for safety-critical sections
3. **Multi-round consensus engine:**
   - Round 1: Claude Haiku + DeepL back-translate → lexical scoring → check agreement (≤10pt tolerance)
   - Round 2 (if inconclusive): Add Google Gemini → recalculate average
   - Round 3 (if still inconclusive): Add DeepSeek → final determination
4. **Glossary verification** — Check expected translations are present; downgrade to Review if mismatches found
5. **Outcome:** Pass (≥ threshold), Review (≥ threshold-15), or Fail

**Reviewer Workflow:**

- Reviewers can Accept or Edit each section result (no explicit Reject button — implicit rejection is recorded automatically on Edit or Retry)
- Edited sections trigger automatic re-validation
- Full audit trail with reviewer name, decision time, metadata

**Audit Report:**

- Professional PDF generated with QuestPDF
- Cover page, executive summary, per-section details with colour-coded outcomes
- Uploaded to R2 storage, URL stored on the validation run

**Real-time Progress:**

- SignalR `TranslationValidationHub` sends progress updates, section completions, and run completion events

### Subtitle Processing Flow

```
Start Processing → Transcribing (ElevenLabs) → Translating (Claude API) → Uploading (R2) → Completed
```

Progress updates sent via SignalR hub in real-time.

---

## Core Domain Entities

### SupervisorAssignment (New)

- **SupervisorAssignment** — Many-to-many join between Employee (supervisor) and Employee (operator) at the TenantEntity level
  - `SupervisorEmployeeId` (Guid) — FK to Employee acting as supervisor
  - `OperatorEmployeeId` (Guid) — FK to Employee acting as operator
  - Unique composite index on `{TenantId, SupervisorEmployeeId, OperatorEmployeeId}`
  - **Soft delete for unassignment:** `CreatedBy`/`CreatedAt` = assigned by/when; `IsDeleted` + `UpdatedBy`/`UpdatedAt` = unassigned by/when
  - **Restore-on-reassign pattern:** Re-assigning a previously unassigned operator restores the soft-deleted row (clears `IsDeleted`, updates `UpdatedBy`/`UpdatedAt`) rather than inserting a new row that would violate the unique index
  - Employee deletion validates no active supervisor assignments exist before allowing soft delete

### Sector & TenantSector

> **Note:** Sector and TenantSector entities physically live in the ToolboxTalks module domain — see entities #28-29 in the ToolboxTalks section below for full documentation.

---

## Toolbox Talks Module Entities (36 Total)

### Content

1. **ToolboxTalk** — Core entity: code (unique per tenant, max 20 chars, auto-generated from title initials), title, description, category, video, PDF, sections, questions, quiz settings, certificate/refresher options, AI generation state, translations
2. **ToolboxTalkSection** — Content section with title, HTML content, ordering, acknowledgment requirement
3. **ToolboxTalkQuestion** — Quiz question (MultipleChoice, TrueFalse, ShortAnswer) with options, correct answer, points
4. **ToolboxTalkSlide** — PDF page slide with image path and extracted text

### Courses

5. **ToolboxTalkCourse** — Ordered collection of talks with sequential completion, refresher, certificate, auto-assign options
6. **ToolboxTalkCourseItem** — Join entity linking talk to course with order index
7. **ToolboxTalkCourseAssignment** — Course assigned to employee with progress tracking

### Assignment & Completion

8. **ToolboxTalkSchedule** — Schedule for assigning talks (one-time or recurring)
9. **ToolboxTalkScheduleAssignment** — Employee assignment within a schedule
10. **ScheduledTalk** — Individual talk assignment with video progress, geolocation, status
11. **ScheduledTalkCompletion** — Completion record with signature, quiz results, certificate URL, geolocation
12. **ScheduledTalkSectionProgress** — Per-section read tracking
13. **ScheduledTalkQuizAttempt** — Quiz attempt with answers (JSON), score, pass/fail

### Certificates

14. **ToolboxTalkCertificate** — Issued for talk or course completion with snapshot data

### Translation (Multi-language)

15. **ToolboxTalkTranslation** — Translated title, description, sections JSON, questions JSON, email templates per language
16. **ToolboxTalkVideoTranslation** — Video dubbing tracking (ElevenLabs)
17. **ToolboxTalkSlideshowTranslation** — Translated HTML slideshow
18. **ToolboxTalkSlideTranslation** — Translated slide text
19. **ToolboxTalkCourseTranslation** — Translated course title/description

### Subtitles

20. **SubtitleProcessingJob** — Transcription + translation job tracking
21. **SubtitleTranslation** — Individual language SRT file

### Settings

22. **ToolboxTalkSettings** — Tenant-level config (due days, reminders, passing score, translation settings)

### Translation Validation (TransVal)

23. **TranslationValidationRun** — Top-level validation session: talk/course reference, language, sector, pass threshold, overall score/outcome/safety verdict, audit metadata (reviewer name/org/role, document ref, client name, audit purpose), report URL
24. **TranslationValidationResult** — Per-section result: original/translated text, back-translations A-D, scores A-D, final score, rounds used, outcome, safety classification, glossary mismatches, reviewer decision (Pending/Accepted/Rejected/Edited), edited translation
25. **SafetyGlossary** — Sector-based safety glossary (TenantId nullable: null = system default, Guid = tenant override), sector key/name/icon
26. **SafetyGlossaryTerm** — Individual term: English term, category, isCritical flag, translations JSON (language code → translated term)

### Content Creation

27. **ContentCreationSession** — Wizard session state: InputMode, OutputType, status, sectorKey, language config, subtitle job link, draft talk reference

### Sector Management

28. **Sector** — First-class sector entity (`BaseEntity`): Key (string, max 50, unique — canonical string matching `SafetyGlossary.SectorKey` and `TranslationValidationRun.SectorKey`), Name, Icon, DisplayOrder, IsActive. String FK fields intentionally not converted to real FKs
29. **TenantSector** — Junction entity linking Tenant to Sector (`BaseEntity`): TenantId, SectorId, IsDefault. Composite unique index on `{TenantId, SectorId}`. Cross-module FK to Tenant (`DeleteBehavior.Restrict`). Restore-on-reassign pattern (note 12)

### Regulatory Profile Chain

30. **RegulatoryBody** — System-managed (no TenantId): Name, Code (unique, max 20), Country, Website. e.g. HIQA, HSA, FSAI, RSA
31. **RegulatoryDocument** — System-managed: Title, Version, EffectiveDate, Source, SourceUrl, IsActive. FK to RegulatoryBody
32. **RegulatoryProfile** — System-managed intersection of RegulatoryDocument × Sector. SectorKey is a denormalised copy of Sector.Key maintained for quick lookup. CategoryWeightsJson holds JSON array of {Key, Label, Weight} scoring categories. Composite unique index on {RegulatoryDocumentId, SectorId}
33. **RegulatoryCriteria** — Individual criteria items within a profile. Supports tenant overrides following the SafetyGlossary pattern (TenantId nullable: null = system default, Guid = tenant override). Query filter is `!IsDeleted` only — tenant filtering handled at service level. Composite unique index on {RegulatoryProfileId, TenantId, CategoryKey, DisplayOrder}

### Regulatory Scoring

34. **ValidationRegulatoryScore** — TenantEntity: ValidationRunId (FK to TranslationValidationRun), ScoreType (ValidationScoreType enum), RegulatoryProfileId (nullable FK to RegulatoryProfile), OverallScore, CategoryScoresJson (JSON), Verdict, Summary, RunLabel, RunNumber, FullResponseJson, ScoredSectionCount, TargetLanguage, RegulatoryBody (denormalised code)

### Regulatory Requirements & Compliance Mapping

35. **RegulatoryRequirement** — System-managed (`BaseEntity`, no TenantId): specific compliance obligation within a RegulatoryProfile. Title, Description, Section/SectionLabel, Principle/PrincipleLabel, Priority (high/med/low), DisplayOrder, IngestionSource (Manual/Automated), IngestionStatus (Draft/Approved/Rejected — gates visibility to tenants), IngestionNotes, IsActive. FK to RegulatoryProfile (Restrict). Seeded with 15 HIQA homecare requirements
36. **RegulatoryRequirementMapping** — Tenant-scoped (`TenantEntity`): maps a RegulatoryRequirement to either a ToolboxTalk or ToolboxTalkCourse (never both — enforced by check constraint). MappingStatus (Suggested/Confirmed/Rejected), ConfidenceScore (0-100, AI), AiReasoning, ReviewedBy, ReviewedAt. Composite unique indexes on `{TenantId, RequirementId, TalkId}` and `{TenantId, RequirementId, CourseId}` with filtered nulls. Tenant query filter in ApplicationDbContext

### Pipeline Audit

- **PipelineChangeRecord** — System-level (no TenantId): append-only record of pipeline configuration changes. **No update or delete endpoints, ever — audit integrity depends on this.** Status state machine: Draft → ReadyForReview → PendingApproval → Approved; BlockedRegression → Approved requires justification.
- **TranslationDeviation** — TenantEntity: CAPA record linked to a validation run/result. `ValidationRunId` and `ValidationResultId` are `SetNull` on deletion so deviations survive run deletion. Status: Open/InProgress/Closed.

### Enums (24 Total — 23 ToolboxTalks + 1 Core)

#### Core Module

- **TenantStatus** — Active, Inactive, Suspended

#### ToolboxTalks Module

- **CertificateType** — Talk, Course
- **ContentCreationSessionStatus** — Active, Completed, Expired, Cancelled
- **ContentSource** — Manual, Video, Pdf, Both
- **CourseAssignmentStatus** — Assigned, InProgress, Completed, Overdue
- **InputMode** — Video, Pdf
- **OutputType** — Talk, Course
- **QuestionType** — MultipleChoice, TrueFalse, ShortAnswer
- **ReviewerDecision** — Pending, Accepted, Rejected, Edited
- **ScheduledTalkStatus** — Pending, InProgress, Completed, Overdue, Cancelled
- **SubtitleProcessingStatus** — Pending, Transcribing, Translating, Uploading, Completed, Failed, Cancelled
- **SubtitleTranslationStatus** — Pending, InProgress, Completed, Failed
- **SubtitleVideoSourceType** — GoogleDrive, AzureBlob, DirectUrl
- **ToolboxTalkFrequency** — Once, Weekly, Monthly, Annually
- **ToolboxTalkScheduleStatus** — Draft, Active, Completed, Cancelled
- **ToolboxTalkStatus** — Draft, Processing, ReadyForReview, Published
- **ValidationOutcome** — Pass, Review, Fail
- **ValidationRunStatus** — Pending, Running, Completed, Failed, Cancelled
- **ValidationScoreType** — SourceDocument, PureTranslation, RegulatoryTranslation
- **VideoSource** — None, YouTube, GoogleDrive, Vimeo, DirectUrl
- **RequirementIngestionSource** — Manual, Automated
- **RequirementIngestionStatus** — Draft, Approved, Rejected
- **RequirementMappingStatus** — Suggested, Confirmed, Rejected
- **VideoTranslationStatus** — Pending, Processing, Completed, Failed, ManualRequired

---

## Background Jobs (Hangfire)

| Job                            | Schedule         | Description                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ------------------------------ | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ContentGenerationJob           | On-demand        | AI content generation from video/PDF. Slideshow-only mode (`GenerateSlideshowOnlyAsync`) chains to MissingTranslationsJob immediately after saving slideshow HTML — ensures translated slideshow delivery without timing gap.                                                                                                                                                                                                   |
| MissingTranslationsJob         | On-demand        | Single-talk translation gap fill. Triggered by content generation, smart-generate reuse, direct content reuse, new employee language detection, and ContentGenerationJob after slideshow generation. Completeness check logic: checks TranslatedTitle, TranslatedSections, TranslatedQuestions (when RequiresQuiz=true), and ToolboxTalkSlideshowTranslation existence (when SlidesGenerated=true) — not just record existence. |
| DailyTranslationScanJob        | Daily at 2am UTC | Scans talks created/modified in last 25 hours across all tenants for translation gaps. Dispatches MissingTranslationsJob per gap found.                                                                                                                                                                                                                                                                                         |
| ProcessToolboxTalkSchedulesJob | Daily            | Process active schedules to create assignments                                                                                                                                                                                                                                                                                                                                                                                  |
| SendRefresherRemindersJob      | Daily            | Send reminders for upcoming refresher due dates                                                                                                                                                                                                                                                                                                                                                                                 |
| SendToolboxTalkRemindersJob    | Daily            | Send reminders for overdue/pending talks                                                                                                                                                                                                                                                                                                                                                                                        |
| UpdateOverdueToolboxTalksJob   | Daily            | Mark past-due assignments as Overdue                                                                                                                                                                                                                                                                                                                                                                                            |
| TranslationValidationJob       | On-demand        | Multi-round back-translation consensus validation per section (SignalR progress)                                                                                                                                                                                                                                                                                                                                                |
| ValidationReportJob            | On-demand        | Generate audit report PDF (QuestPDF) and upload to R2                                                                                                                                                                                                                                                                                                                                                                           |
| ExpiredSessionCleanupJob       | Daily            | Clean up expired validation sessions                                                                                                                                                                                                                                                                                                                                                                                            |
| RequirementIngestionJob        | On-demand        | AI-powered extraction of regulatory requirements from document URLs (Claude Sonnet)                                                                                                                                                                                                                                                                                                                                             |
| RequirementMappingJob          | On-demand        | AI-powered mapping of published content to regulatory requirements (Claude Sonnet), triggered from publish flow                                                                                                                                                                                                                                                                                                                 |
| `BulkEmployeeImportJob`        | On-demand        | Process CSV rows: create employees + linked user accounts. Tracks per-row outcome (Created/AlreadyExisted/Failed). Uses `IServiceScopeFactory` for per-row DbContext isolation (see note 23). `IsRerun` flag causes duplicate-email failures to count as AlreadyExisted rather than Failed. Enqueued via concrete class to ensure `[AutomaticRetry(Attempts = 0)]` is visible (see note 21).                                    |

---

## HTTP Resilience & Retry Policies

All external HTTP calls use Polly retry policies defined in `ResiliencePolicies.cs` (located in `QuantumBuild.Core.Application/Http/`).

### Policies

| Policy                | Retries | Backoff                              | Triggers                                                      | Used By                  |
| --------------------- | ------- | ------------------------------------ | ------------------------------------------------------------- | ------------------------ |
| `GetClaudePolicy`     | 3       | 2s/4s/8s exponential + ±500ms jitter | `HttpRequestException`, HTTP 429/500/502/503/529              | Claude API clients       |
| `GetElevenLabsPolicy` | 2       | 2s/4s exponential + jitter           | Same as Claude                                                | ElevenLabs transcription |
| `GetTransientPolicy`  | 3       | 1s/2s/4s exponential                 | `HttpRequestException`, HTTP 408/429/5xx (standard transient) | DeepL, Gemini, DeepSeek  |

### How Policies Are Applied

Policies are chained at **HttpClient registration time** in `ServiceCollectionExtensions.cs`, `Program.cs`, and `LessonParserInfrastructureExtensions.cs` — not inside service classes.

```csharp
services.AddHttpClient<MyService>()
    .AddPolicyHandler(ResiliencePolicies.GetClaudePolicy(logger));
```

### Rules

- When adding a new external HTTP client, always chain the appropriate policy via `.AddPolicyHandler(ResiliencePolicies.Get*Policy(logger))` at registration
- Do **NOT** add manual retry loops inside service classes — Polly handles retries at the HttpClient level

### Global Exception Handler

Registered in `Program.cs` via `UseExceptionHandler` — returns `application/problem+json` with HTTP 500 and no stack trace leak.

---

## AI Usage Logging

All Claude API calls are logged per-tenant to support billing.

**Entities:**

- `AiUsageLog` (TenantEntity) — raw log, one row per API call, retained for 3 months
- `AiUsageSummary` (TenantEntity) — daily aggregates, written by AggregateAiUsageJob after raw rows age out

**Key types:**

- `AiOperationCategory` enum — tags each call with its operation type (ContentParsing, SectionGeneration, QuizGeneration, SlideshowGeneration, ContentTranslation, BackTranslation, RegulatoryScoring, RequirementIngestion, RequirementMapping, LessonGeneration, DialectDetection)
- `IAiUsageLogger` — scoped service, call LogAsync after every successful Claude API response. Never throws — logging failures are silent.
- `AnthropicResponseParser` — static utility, use Parse(responseBody) to extract ContentText, InputTokens, OutputTokens, and Model from all Anthropic API responses. Replaces ad-hoc JsonDocument navigation.

**Rules:**

- Always use AnthropicResponseParser.Parse() instead of manual JsonDocument navigation in any new Claude call site
- Always call IAiUsageLogger.LogAsync() after every successful Claude response
- Pass IsSystemCall = true for any call made from a Hangfire background job
- Pass ReferenceEntityId where the call relates to a specific entity (ToolboxTalkId, CourseId etc.)
- A logging failure must NEVER fail the AI operation — the try/catch in AiUsageLogger swallows all exceptions

**Aggregation:**

- AggregateAiUsageJob runs monthly (1st of month, 3am UTC)
- Aggregates raw rows older than 3 months into daily AiUsageSummary rows
- Deletes raw rows after successful aggregation in the same transaction

---

## Cloudflare R2 Storage

Used for storing videos, PDFs, subtitle files, certificate PDFs, and validation report PDFs.

**Configuration (environment variables):**

```
SubtitleProcessing__SrtStorage__Type=CloudflareR2
SubtitleProcessing__SrtStorage__CloudflareR2__AccountId=<account-id>
SubtitleProcessing__SrtStorage__CloudflareR2__ServiceUrl=https://<account-id>.r2.cloudflarestorage.com
SubtitleProcessing__SrtStorage__CloudflareR2__AccessKeyId=<access-key>
SubtitleProcessing__SrtStorage__CloudflareR2__SecretAccessKey=<secret-key>
SubtitleProcessing__SrtStorage__CloudflareR2__BucketName=rascor-videos
SubtitleProcessing__SrtStorage__CloudflareR2__PublicUrl=https://pub-cb8b7507e2a34ca2a366caa3bca24d08.r2.dev
SubtitleProcessing__SrtStorage__CloudflareR2__Path=subs
```

---

## Running the Application

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- PostgreSQL

### Backend (API)

```bash
cd src/QuantumBuild.API
dotnet run
# Runs on http://localhost:5222
# Swagger: http://localhost:5222/swagger
```

### Frontend

```bash
cd web
npm install
npm run dev
# Runs on http://localhost:3000
```

### Database

- Host: localhost (127.0.0.1)
- Port: 5432
- Database: rascor_stock
- Username: postgres

### EF Migrations

```bash
cd src/QuantumBuild.API
dotnet ef migrations add MigrationName --project ../Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure
dotnet ef database update
```

### Applied Migrations (Session Log)

| Migration                                     | Description                                                                                                                                           |
| --------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| AddRegulatoryRequirements                     | RegulatoryRequirement + RegulatoryRequirementMapping entities, check constraint, indexes                                                              |
| AddLastIngestedAtToRegulatoryDocument         | LastIngestedAt nullable DateTimeOffset on RegulatoryDocument                                                                                          |
| AddReviewNotesToRegulatoryRequirementMappings | ReviewNotes column on RegulatoryRequirementMapping                                                                                                    |
| FixPrincipleLabelCanonicalForm                | Normalise PrincipleLabel to use "&" instead of "and"                                                                                                  |
| AddCorpusEntities                             | AuditCorpora, AuditCorpusEntries, CorpusRuns, CorpusRunResults, ProviderResultCache tables + Status column on PipelineChangeRecords (default 'Draft') |

---

## Deployment (Railway)

### Branch Strategy

- The **TransVal feature** is developed on the `transval` branch
- Two remotes must be kept in sync: `origin/transval` and `company/transval`
- Railway auto-deploys from the `transval` branch, but **auto-deploy does not always trigger on push**
- To force a Railway redeploy: `git commit --allow-empty -m "chore: trigger Railway redeploy"`

---

## Phase 13 (TransVal) — Implementation Details

### Create Content Wizard — Step Order

1. **Input & Config** — Upload video/PDF, set title, category, language
2. **Parse** — AI extracts/generates sections from uploaded content
3. **Quiz** — AI-generated quiz questions (min 5, up to 10 for longer content — count is AI-determined, not hardcoded per section)
4. **Settings** — Quiz settings, certificate, refresher, due days
5. **Translate & Validate** — Translation + back-translation validation
6. **Publish** — Final review and publish

Quiz and Settings come **before** Translate & Validate so all content (sections, quiz questions, title, description) exists before translation runs.

### Create Content Wizard — Key Architecture Decisions

- **Draft ToolboxTalk created in `ContentCreationSessionService.StartTranslateValidateAsync`** — sections, quiz questions, quiz settings, title, description and category are all synced to the draft talk before the translation job fires
- **TranslationValidationJob translates everything** — section content, quiz question text, answer options, talk title and description into all target languages
- **Quiz question count is AI-determined** within a range (min 5, up to 10 for longer content) — not hardcoded per section
- **Reject button removed from reviewer UI** — implicit rejection is recorded automatically when Edit or Retry is triggered on a non-Accepted section
- **Upsert pattern for TranslationValidationResult rows** — `TranslationValidationService.ValidateSectionAsync` queries for existing `{ValidationRunId, SectionIndex}` and updates in place rather than delete-then-insert, preventing race conditions with parallel validation workers

### SignalR — Translation Validation Hub

- **Hub route:** `/api/hubs/translation-validation`
- **Event names are case-sensitive** — backend sends `ValidationProgress`, `SectionCompleted`, `ValidationComplete` (PascalCase on both server and client)
- **`ValidationComplete` sets `percentComplete` to 100** on the frontend, fixing the progress bar stuck at 95% issue
- **Retry actions call `hub.reset()`** to clear stale `isComplete` state before re-running validation
- **Manual reconnect fallback** with 10 retry attempts (~2 min exponential backoff) handles WebSocket 1006 drops mid-validation via `onclose` event handler

### Known Issues / Watch Points

1. **DeepL base URL** — Must be `https://api.deepl.com/v2` (no trailing `/translate`) for paid keys; free tier uses `https://api-free.deepl.com/v2`
2. **Railway auto-deploy** does not always trigger on push — workaround: `git commit --allow-empty -m "chore: trigger Railway redeploy"` pushed to both `origin` and `company` remotes
3. **CamelCaseJson for session data** — `ContentCreationSessionService` uses camelCase `JsonSerializerOptions` throughout for quiz and settings JSON deserialization
4. **SettingsStep textarea focus** — Mutation refs stabilised via `useCallback` dependencies to prevent textarea losing focus on every keystroke

---

## Reusable Frontend Components

### shadcn/ui Components (31)

accordion, alert, alert-dialog, avatar, badge, button, calendar, card, checkbox, combobox, command, dialog, dropdown-menu, form, input, label, multi-select-combobox, popover, progress, radio-group, select, separator, sheet, skeleton, sonner, sortable, switch, table, tabs, textarea, tooltip

### Shared Components

| Component                | Location                                           | Description                          |
| ------------------------ | -------------------------------------------------- | ------------------------------------ |
| DataTable                | `components/shared/data-table.tsx`                 | Paginated table with sorting, search |
| DeleteConfirmationDialog | `components/shared/delete-confirmation-dialog.tsx` | Confirm delete with toast            |
| ExportButtons            | `components/shared/export-buttons.tsx`             | Export functionality                 |
| PendingTrainingBanner    | `components/shared/pending-training-banner.tsx`    | Training reminder banner             |
| TopNav                   | `components/layout/top-nav.tsx`                    | Header with user dropdown            |

### Form Pattern

```tsx
const schema = z.object({ ... });
type FormData = z.infer<typeof schema>;
const form = useForm<FormData>({
  resolver: zodResolver(schema),
  defaultValues: { ... }
});
```

### Query Pattern

```tsx
const { data, isLoading } = useQuery({
  queryKey: ["toolbox-talks", page, search],
  queryFn: () => apiClient.get("/toolbox-talks", { params: { page, search } }),
});

const mutation = useMutation({
  mutationFn: (data) => apiClient.post("/toolbox-talks", data),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ["toolbox-talks"] });
    toast.success("Created");
  },
});
```

---

## Coding Conventions

### C# / .NET

- Use `record` types for DTOs
- Async all the way — suffix with `Async`
- Use primary constructors for simple classes
- Nullable reference types enabled
- File-scoped namespaces
- CQRS pattern for ToolboxTalks (Commands/Queries)

### Entity Framework

- Configure entities in separate `EntityConfiguration` classes
- Use `HasQueryFilter` for tenant and soft delete filtering
- Migrations named descriptively (see Note 28 — CLI-generated only)

### Frontend

- Use Server Components by default, Client Components when needed
- API calls through TanStack Query with cache invalidation
- Forms with React Hook Form + Zod
- Toast notifications with Sonner
- Feature-based component organization (`features/toolbox-talks/`)

---

## ToolboxTalk Code Field

- **Required field** on ToolboxTalk entity, `string`, max 20 chars
- **Unique per tenant** — index `IX_ToolboxTalks_TenantId_Code`
- **Auto-generated from title initials** + numeric suffix (e.g., "Manual Handling Safety" → `MHS-001`)
- User can override on create; edit freely on update
- Code propagated to all DTOs: scheduled talks, schedules, courses, certificates, dashboard, reports
- **Display:** First column in list views, badge in detail views, prefix in employee-facing views
- **Frontend auto-generation:** Generated as user types title; tracks dirty state to avoid overwriting manual edits

---

## Skills Matrix

### Overview

Employee × learning grid showing training status per combination. Role-scoped via `ResolveScopedEmployeeIdsAsync()`.

### API Endpoints

| Method | Endpoint                                                    | Description              |
| ------ | ----------------------------------------------------------- | ------------------------ |
| GET    | `/api/toolbox-talks/reports/skills-matrix?category=`        | Skills matrix grid data  |
| GET    | `/api/toolbox-talks/reports/skills-matrix/export?category=` | Excel export (ClosedXML) |

### Response: `SkillsMatrixDto`

- **Employees** (rows) — name, code, site
- **Learnings** (columns) — talk code, title, category
- **Cells** — status per employee × learning combination

### Cell Statuses (5)

| Status          | Description              |
| --------------- | ------------------------ |
| **Completed**   | With score percentage    |
| **InProgress**  | Currently in progress    |
| **Overdue**     | With days overdue count  |
| **Assigned**    | Assigned but not started |
| **NotAssigned** | No assignment exists     |

### Data Derivation (Admin Path)

- Both employees and learnings are derived from `ScheduledTalks` (not independent ToolboxTalk query)
- Unassigned employees included for compliance visibility

### Role Scoping

| Role              | Employees Shown         |
| ----------------- | ----------------------- |
| Admin / SuperUser | All employees           |
| Supervisor        | Assigned operators only |
| Operator          | Self only               |

### Frontend Pages

| Path                                         | Context                                                   |
| -------------------------------------------- | --------------------------------------------------------- |
| `/toolbox-talks/team/skills-matrix`          | Supervisor view (nav: after My Team, before Team Reports) |
| `/admin/toolbox-talks/reports/skills-matrix` | Admin view (from Reports landing page)                    |

### UI Features

- **Category filter** — TrainingCategory lookups
- **Learning multi-select filter** — Client-side column filtering from fetched data
- **Compact mode toggle** — Dot/icon cells for dense grids; auto-enables at 6+ learnings
- **Client-side pagination** — 25 employees per page
- **Sticky first column** — Employee name/code for horizontal scrolling
- **Columns grouped by category** with header row

### Excel Export

- Generated with **ClosedXML** (dependency in ToolboxTalks.Infrastructure)
- Colour-coded cells matching UI status colours
- Frozen panes + auto-fit columns
- Separate **Legend sheet** explaining colour codes
- Filename: `SkillsMatrix_{date}.xlsx`

---

## Translation Validation (TransVal)

### Overview

Multi-round back-translation consensus engine that validates AI-generated translations for accuracy and safety compliance. Produces formal audit reports for compliance purposes.

### Backend Services

| Service                             | Purpose                                                                                                                                                                                                                                     |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **LexicalScoringService**           | Token-overlap similarity scoring (0-100) between original and back-translated text                                                                                                                                                          |
| **WordDiffService**                 | LCS-based word-level diff with Insert/Delete/Equal operations and similarity percentage                                                                                                                                                     |
| **ConsensusEngine**                 | Escalating multi-round back-translation: Round 1 (Haiku + DeepL), Round 2 (+Gemini), Round 3 (+DeepSeek)                                                                                                                                    |
| **SafetyClassificationService**     | Glossary term detection + regex patterns (prohibition, emergency, hazard) for safety-critical content                                                                                                                                       |
| **GlossaryTermVerificationService** | Verifies expected glossary translations are present in translated text                                                                                                                                                                      |
| **TranslationValidationService**    | Orchestrator: safety classify → bump threshold → consensus → glossary verify → persist result                                                                                                                                               |
| **TranslationValidationJob**        | Hangfire job orchestrator. Injects `ISafetyClassificationService` for per-section safety classification. Pre-loads glossary terms via `LoadGlossaryTermsAsync` once per run (not per section) — prefers tenant override over system default |
| **ValidationReportService**         | QuestPDF-based audit report: cover page, executive summary, per-section details, colour-coded outcomes                                                                                                                                      |
| **RegulatoryScoreService**          | Claude Sonnet scoring: source document quality, pure linguistic translation, regulatory-aware translation with sector criteria                                                                                                              |

### Configuration (`TranslationValidation` settings section)

```json
{
  "DeepL": { "ApiKey": "...", "BaseUrl": "https://api.deepl.com/v2" },
  "Gemini": { "ApiKey": "...", "Model": "gemini-2.0-flash", "BaseUrl": "..." },
  "DeepSeek": { "ApiKey": "...", "Model": "deepseek-chat", "BaseUrl": "..." },
  "DefaultThreshold": 75,
  "SafetyCriticalBump": 10,
  "MaxRounds": 3,
  "SessionExpiryHours": 24
}
```

### SignalR Hub

**Route:** `/api/hubs/translation-validation`

- `ValidationProgress` — Progress update: stage, percentComplete, message
- `SectionCompleted` — Section result: index, outcome, score, isSafetyCritical
- `ValidationComplete` — Run completion: success, message (note: event names are PascalCase on the wire)
- **Reconnection:** Extended to 10 retry attempts with exponential backoff; manual reconnect fallback on `onclose`

### Frontend

- **Validation history tab** on talk detail page — lists all runs with status, score, outcome
- **Run detail page** — section-by-section results with back-translations, scores, reviewer decision UI
- **6-step creation wizard** — configures language, sector, threshold, audit metadata before starting validation
- **Real-time progress panel** — SignalR-powered progress display during validation via `useValidationHub` hook
- **Settings UI** — glossary management (CRUD terms per sector), threshold configuration, audit purpose defaults

### Unit Tests

| Test Class                           | Coverage                                                                                      |
| ------------------------------------ | --------------------------------------------------------------------------------------------- |
| **LexicalScoringServiceTests**       | Identical strings, partial overlap, empty strings, case insensitivity, punctuation stripping  |
| **WordDiffServiceTests**             | LCS algorithm, Insert/Delete/Equal operations, similarity percentage                          |
| **ConsensusEngineTests**             | Round 1-3 escalation, threshold pass/review/fail, agreement tolerance                         |
| **SafetyClassificationServiceTests** | Glossary detection, regex patterns (prohibition, emergency, hazard), critical term extraction |

### Integration Tests

| Test Class                     | Coverage                                                                                                                          |
| ------------------------------ | --------------------------------------------------------------------------------------------------------------------------------- |
| **TranslationValidationTests** | Multi-tenant isolation, glossary CRUD, system default protection, validation run lifecycle, report generation, reviewer decisions |

---

## Notes for Claude Code

Archived notes 1-89 are in CLAUDE-archive.md

1. **Glossary hard-block enforcement — pre-scoring replacement pass** — `GlossaryReplacementService` applies case-insensitive regex replacement of locked glossary terms BEFORE the consensus engine runs, so the corrected text is what gets back-translated and scored. Insertion point is step 2 in `TranslationValidationService.ValidateSectionAsync()` — after initial translation is returned from the provider, before consensus. Corrections stored as `GlossaryCorrectionsJson` (nullable string) and `GlossaryHardBlockApplied` (nullable bool) on `TranslationValidationResult` for audit. Title translation now also receives glossary instructions via `TranslationValidationJob` — previously excluded from Tier 4 prompt injection. Frontend shows amber Wand2 badge on `ValidationSectionCard` when corrections were applied.
2. **DeepSeek removed as Round 3 back-translation provider** — DeepSeek is removed as a back-translation provider (GDPR — indefinite retention, China-based servers). `DeepSeekTranslationService` is `[Obsolete]`. Do NOT re-enable without legal review. Round 3 provider is `ClaudeSonnetBackTranslationService` (`claude-sonnet-4-20250514`).
3. **General System Audit Log** — `SystemAuditLog` entity is append-only, no `BaseEntity`, no soft-delete, nullable `TenantId` as a plain field (not FK with query filter) so it survives tenant resets and captures SuperUser cross-tenant actions. `ISystemAuditLogger` resolves user context from `ICurrentUserService` internally and swallows exceptions so audit logging never breaks the calling operation. `AuditActions` static constants define all action strings. Wired into: full auth path (login success/failure/inactive, logout, token refresh, password set), `UsersController`, `TenantsController` (`ResetData` always audited regardless of outcome), `TenantModulesController`, `EmployeesController`. `LastLoginAt` and `LastLoginIp` added to `User` entity — set on successful login. `DeletedBy` added to `BaseEntity` — set on all soft deletes. `AccessFailedAsync` now called on wrong password — lockout threshold works correctly for the first time.
8. **Translation Pipeline Audit Controls — Phase 4 (Corpus) — COMPLETE** — All four phases of pipeline audit controls are now implemented. `AuditCorpus` is TenantEntity; `AuditCorpusEntry` and `CorpusRunResult` are `BaseEntity` (scoped via parent). `ProviderResultCache` is system-level (no TenantId) — safe because corpus entries contain no PII. Cache key is `(CorpusEntryId, Provider, ProviderVersion)` — only re-calls providers that actually changed. `ValidateSectionAsync` now accepts `persist = false` for dry-run scoring (identical pipeline, no DB writes) — used by corpus runs. `CostEstimationService` has static EUR rate table per model ID; smoke test = 5 entries; Round 3 assumed 30% hit rate. Two-step trigger: estimate → confirm (>€3 threshold) → SuperUser approval (>€10). 10-minute cooldown enforced in service layer. Auto-deviation created on Fail verdict; linked `PipelineChangeRecord` moves to `BlockedRegression`. SuperUser override requires written justification which closes the auto-deviation. Draft → ReadyForReview auto-triggers corpus runs for all locked corpora matching the affected sector/language pairs. `TranslationDeviation.MetadataJson` stores `corpusRunId` for corpus-linked deviations (not `ValidationRunId` FK since that expects a validation run).
10. **QR Code Location Training — COMPLETE** — Public `/qr/[codeToken]` page outside the `(authenticated)` route group — no JWT required. 6-digit PIN hashed via `IPasswordHasher<Employee>`, 5-attempt lockout with 15-minute window, `QrPinFailedAttempts` and `QrPinLockedUntil` on Employee entity. PIN NEVER returned in any API response — emailed only. `QrLocation` + `QrCode` entities (TenantEntity), `CodeToken = Guid.NewGuid("N")`, QR PNG generated via QRCoder and uploaded to R2 at `{tenantId}/qr-codes/{codeToken}.png` on code creation. Three content modes: `ViewOnly`, `Training`, `Induction`. `QrSession` entity (TenantEntity) tracks active/completed/abandoned sessions via `sessionToken` (Guid, no JWT). `QrScanController` has no `[Authorize]` — validates via `sessionToken` only. On completion: creates `ScheduledTalkCompletion` linked to the employee. `GenerateEmployeePinsJob` is one-off (not recurring) — enqueued exactly once when tenant first enables `QrLocationTrainingEnabled`, detected by reading previous setting value before save in `TenantSettingsController`. Welcome email includes PIN section only when QR feature enabled. Admin: `/admin/toolbox-talks/qr-locations` two-panel UI with sessions panel. Employee detail shows `QrTrainingHistorySection` when QR enabled. `GET /api/employees/{id}/training-history` combines `ScheduledTalks` and `QrSessions` sorted by date. `QrLocationTrainingEnabled` in `TenantSettingKeys.cs`.
12. **Production deployment** — Production Railway instance deployed from `main` branch on `QuantumBuildIE/quantumbuild-lms`. Development deploys from `transval`. Demo is disconnected. Deployment workflow: develop on `transval` → push to `company/transval` (auto-deploys to Development) → when ready: `git checkout main` → `git merge transval` → `git push company main` (auto-deploys to Production). Both `origin` and `company` remotes must be kept in sync. `AppSettings__BaseUrl` on Production web service points to `quantumbuild-lms-web-production.up.railway.app` until custom domain `certifiediq.ai` is configured.
14. **Cross-tenant data leak fix (CRITICAL)** — `TenantEntity`-derived ToolboxTalks entities had `HasQueryFilter` configured with `!IsDeleted` only in their individual `EntityConfiguration` classes — no tenant predicate. Any query path missing an explicit `.Where(t => t.TenantId == ...)` clause returned rows from all tenants. Fix: moved all 12 `HasQueryFilter` calls to `ApplicationDbContext` (matching Core entity pattern) with `BypassTenantFilter || TenantId == currentTenantId` predicate; removed the soft-delete-only filters from the 12 entity configurations. `ExpiredSessionCleanupJob` and `CorpusRunJob` updated with `IgnoreQueryFilters()` on the queries that must cross tenant boundaries. No migration required — model-level change only. **Coverage update (2026-06-23):** four additional TenantEntities (`ToolboxTalkCourseAssignment`, `TranslationDeviation`, `AiUsageLog`, `AiUsageSummary`) were not in the original 12 and had only entity-config-level soft-delete filters. These were tightened to the same DbContext-level pattern in the same commit. See `docs/cross-tenant-status-recon.md`.
15. **TransVal reviewer-edit workflow defects (A–D) fixed** — Four defects in the section reviewer-edit flow resolved: **(A)** `ValidateSectionAsync` no longer wipes `ReviewerDecision`/`EditedTranslation` on re-validation — those fields are only cleared on first-time entity creation (`Id == Guid.Empty`). **(B)** `LoadSectionsAsync` now loads existing reviewer edits into a dictionary and substitutes `EditedTranslation` for the published translation before scoring, so re-validation scores the corrected text. **(C)** `TranslationValidationJob.ExecuteAsync` gains an optional `sectionIndices` parameter — `EditSection` and `RetrySection` pass `[sectionIndex]` to re-validate a single section only; initial validate and wizard flow pass `null` for a full run. **(D)** `AcceptSection` in `TranslationValidationController` now calls `PropagateEditedTranslationAsync` when `EditedTranslation` is non-null — resolves `SectionIndex` to `SectionId` via `ToolboxTalkSections` ordered by `SectionNumber`, deserialises `TranslatedSections` JSON, replaces matching section `Content`, and flushes both mutations in a single atomic `SaveChangesAsync` — employees see the reviewer's corrected translation immediately on acceptance.
18. **Two API response conventions — match the controller's existing pattern** — Core CRUD controllers (Employees, Users, Sites, Companies, Contacts, Tenants, Roles, SupervisorAssignments, ToolboxTalks most endpoints, Reports, Certificates, CourseAssignments, Schedules, LessonParser) return `Result<T>` envelopes: `{ success, data, errors }`. Frontend reads `response.data.data`. Newer controllers (Monitoring, PipelineAudit, RegulatoryIngestion, RequirementMapping, SafetyGlossary, TenantSettings, TenantSectors, TenantModules, QrLocation, TranslationValidation GetById endpoints) return DTOs directly. Frontend reads `response.data`. The tell-tale sign on the backend is `if (!result.Success) return BadRequest(result)` before `return Ok(result)` — that means `Result<T>` envelope. Before writing a new frontend API function, check what the corresponding controller action returns.
19. **Course assignment deletion — completed talks survive as standalone history** — `DeleteCourseAssignmentCommandHandler` skips `ScheduledTalk` rows whose `Status == Completed` when soft-deleting an assignment's child talks. Those completed talks remain visible in the employee's Completed tab and all reports (compliance, skills matrix, completion history) as standalone records. Their `ScheduledTalkCompletion` rows are unaffected. `ScheduledTalkCertificate` records are also safe — all certificate queries project snapshot fields stored at issuance time and do not navigate to `CourseAssignment`. Course-level certificates cannot exist for a deletable (in-progress) assignment because the issuance guard in `CourseProgressService` only fires at full completion, and `DeleteCourseAssignmentCommandHandler` already blocks deletion of fully-completed assignments.
20. **SuperUser role — cannot be assigned through the application** — `UserService.CreateAsync` and `UpdateAsync` both reject any request that includes the SuperUser role (`NormalizedName == "SUPERUSER"` check returns an error before the Identity layer is touched). `user-form.tsx` filters SuperUser from the role checkboxes (`roles.filter(role => role.name !== "SuperUser")`). SuperUser accounts must be seeded or created directly in the database. Role updates go through `PUT /api/users/{id}` — `UpdateUserDto` carries `RoleIds`; there is no separate `PUT /api/users/{id}/roles` route. The employee form role dropdown is restricted to Operator/Supervisor and only renders in create mode (inside `{!isEditing && ...}`) — there is no role editing on the employee edit form; roles for existing employees are managed via the linked user's edit form.

**Note 21 — Hangfire enqueue + attributes**: Hangfire reads `[AutomaticRetry]` and similar attributes from the type parameter of `BackgroundJob.Enqueue<T>(...)`. Enqueueing via an interface (`Enqueue<IFooJob>`) makes attributes on the concrete class **invisible** to Hangfire — it falls back to its default 10-retry policy. **Always enqueue via the concrete class**. Codebase-wide sweep completed: all 25 enqueue sites use concrete types. Two production bugs in one session traced to this trap (`BulkEmployeeImportJob` and `GenerateEmployeePinsJob`).

**Note 22 — TenantEntity creation inside Hangfire jobs**: `ApplicationDbContext.SaveChangesAsync` auto-stamps `TenantId` from `ICurrentUserService`, which returns `Guid.Empty` when there is no HttpContext (Hangfire job context). Any `TenantEntity` created inside a job must have its `TenantId` explicitly set before `Add()`. The auto-stamp's `if Guid.Empty` guard makes explicit assignment a no-op on the UI path, so this is safe everywhere. `EmployeeService.CreateAsync` now does this explicitly — was the source of a production bug where bulk-imported employees landed with `TenantId = Guid.Empty`.

**Note 23 — Long-running jobs and DbContext isolation**: A Hangfire job processing many independent units must use a fresh DbContext per unit via `IServiceScopeFactory.CreateAsyncScope()`. The shared-context pattern causes change-tracker contamination — a failed `SaveChangesAsync` leaves the bad entity in the tracker in `Added` state, so every subsequent save (including the failure handler's status write) re-submits it and re-fails, leaving the session stuck. The service AND its DbContext must be resolved from the same scope. `BulkEmployeeImportJob` is the reference implementation; the outer constructor-injected DbContext is retained only for status writes on the single session entity.

**Note 24 — Class-level `[Authorize]` + action-level `[Authorize]` stack**: Both must pass independently. A controller class-gated by one policy with an action gated by a lower-privilege policy rejects lower-privilege users regardless of the action gate. Don't try to serve two audiences from one controller — create a dedicated controller for the lower-privilege audience. `RegulatoryBrowseController` was extracted from `RegulatoryIngestionController` (class-gated `Tenant.Manage` for SuperUser document administration) for exactly this reason — the tenant-admin browse endpoint 403'd despite carrying action-level `Learnings.Admin`.

**Note 25 — Structured failure codes**: Callers must branch on `Result.ErrorCode` (type `FailureCode` enum), not error message text — messages can be reworded silently. Use the `Fail<T>(error, code)` factory overload.

**Note 27 — QrPinPlain security trade-off (product decision, 2026-05-28)**: `Employee.QrPinPlain` stores QR PINs in plaintext. Visible on the employee detail view to SuperUsers and tenant Admins (admins are scoped to their own tenant's employees via existing tenant filtering). Explicit product decision (boss request) to allow admins to read out PINs to employees who have forgotten them. Known trade-offs: plaintext PINs are recoverable from any DB access or backup; all tenant admins — not just internal staff — can read them. Revisit if a more secure approach becomes viable (e.g. one-time reveal at reset time only, or encryption with an external key that DB access alone cannot decrypt).

**Note 28 — EF migrations must be CLI-generated**: Always create migrations with `dotnet ef migrations add <MigrationName>` (from `src/QuantumBuild.API`, with `--project` pointing to the target Infrastructure project). The CLI generates both `<Name>.cs` and `<Name>.Designer.cs` together. A hand-written `.cs` without its `.Designer.cs` causes EF to silently skip the migration on startup — the `[Migration]` attribute EF reads lives in the `.Designer.cs` partial class declaration. Four instances of this trap in one session (one production bug, three Development drift cases). Always verify both files exist after every `migrations add`.

**Note 29 — Wizard cutover toggle (§5.27)**: Two wizard versions run in parallel during the cutover period. The `UseNewWizard` `TenantSettings` key controls which one the "Create New" button routes to.

- **Legacy wizard** (`/admin/toolbox-talks/create`) — SPA, all 7 steps at one URL, React state. Talks created here have `lastEditedStep == null`.
- **New wizard** (`/admin/toolbox-talks/learnings/**`) — URL-per-step, server-side state. Talks created here have `lastEditedStep != null`.

**Discriminator:** `lastEditedStep != null` → new wizard; `null` → legacy. This is the only reliable signal — there is no `CreatedByWizard` field.

**Toggle:** `TenantSettings.UseNewWizard`. Default `"false"` (legacy). When `"true"`, Create New on the Learnings list and "Create Another" on the legacy wizard's publish step both route to the new wizard. The drafts list shows both wizard types; legacy drafts get a "Legacy" badge and Resume routes to the talk detail page instead of a wizard URL.

**URL override for smoke-testing:** append `?wizard=new` or `?wizard=old` to any page with the Create New button. One-shot — disappears on next navigation. Not persisted to localStorage/cookie.

**Flip sequence:** Settings → General tab → "Wizard Version" toggle. Only flip after §24 (Edit workflow) is sufficiently complete for production use. The toggle is gated to users who can access the Settings page (Admin / SuperUser).

**Eventual removal (§7.1):** when all tenants are on the new wizard and the legacy wizard is decommissioned, remove: the `UseNewWizard` key, `useWizardPreference` hook, `WizardToggleSection` component, `wizard-toggle-section.tsx`, and the Legacy badge logic in `drafts/page.tsx`.

**Note 30 — Running Playwright E2E tests locally**: Playwright is installed in `web/`. Tests live under `web/e2e/`. Run with `npm run e2e` from `web/`. Variants: `e2e:ui` (Playwright UI mode, recommended for debugging), `e2e:headed` (visible browser), `e2e:debug` (step-through), `e2e:report` (open the last HTML report).

**Prerequisites:**
- **PostgreSQL** — must be running (Playwright does not start it)
- **Next.js dev server** — auto-spawned via `webServer` config; reuses `http://localhost:3000` if already running
- **CORS** — `playwright.config.ts` injects `Cors__AllowedOrigins__0=http://localhost:3000` as an env var when it spawns the API. If you run the API manually before `npm run e2e`, add this to your local (gitignored) `appsettings.Development.json` instead: `{ "Cors": { "AllowedOrigins": ["http://localhost:3000"] } }` — without this, Chromium cannot reach the API from the Next.js origin.

**Auto-spawned servers:** `webServer` in `playwright.config.ts` manages both the Next.js dev server AND the .NET API (`dotnet run --project ../src/QuantumBuild.API --launch-profile http`). Playwright polls `http://localhost:5222/health` until the API is ready before running tests. If either server is already running, Playwright reuses it and leaves it alive after tests complete.

**Project structure:** three projects — `setup` (runs `auth.setup.ts` once, saves SuperUser session to `e2e/.auth/superuser.json`), `unauthenticated` (root-level `e2e/*.spec.ts`, no auth state), `authenticated` (files under `e2e/authenticated/`, loads SuperUser session automatically). The setup project must succeed for authenticated tests to run.

**Auth credentials:** SuperUser (`superuser@certifiediq.ai` / `SuperUser123!`) from `appsettings.Development.json` Seed section. Session saved to `web/e2e/.auth/superuser.json` — gitignored, never committed.

**Current coverage:**
- `e2e/login-page.spec.ts` — unauthenticated smoke: login page renders
- `e2e/auth.setup.ts` — setup project: logs in as SuperUser, saves session state
- `e2e/authenticated/login-flow.spec.ts` — authenticated: SuperUser reaches `/admin/tenants`

**Workers:** locked to 1 (`fullyParallel: false`). The Dev DB is shared, and parallel runs are unsafe until per-test data isolation is established. Do not increase without revisiting test data strategy.

**Adding new authenticated tests:** create files under `web/e2e/authenticated/`. They automatically inherit the SuperUser storage state. Read-only tests need no setup/teardown. Write tests must use the Playwright `request` fixture for API-based setup and cleanup in `beforeEach`/`afterEach`.

**Note 31 — DataSeeder credentials are Development and Demo-sourced from config**: `DataSeeder.SeedAsync` creates two credentialled accounts only when `environment.IsDevelopment() || environment.IsEnvironment("Demo")` is true. Credentials are read from configuration:

- `Seed:SuperUser:Email` / `Seed:SuperUser:Password`
- `Seed:Admin:Email` / `Seed:Admin:Password`

Defaults live in `appsettings.Development.json`. These are dev-only local credentials, not secrets. If credentials are missing from configuration, the seeder logs a warning and skips that account; it does not throw.

**System data** (roles, permissions, sectors, regulatory bodies, lookup categories, language data, training categories, tenant modules) seeds unconditionally in all environments and is independent of the credential gate.

**Bootstrap pattern by environment:**

- **Demo:** Seeded automatically on first deploy provided `Seed__SuperUser__Email`, `Seed__SuperUser__Password`, `Seed__Admin__Email`, and `Seed__Admin__Password` are set as Railway env vars (ASP.NET Core env-var separator is `__` not `:`). Missing values log a warning and skip that account — no throw.
- **Production:** SuperUser must still be inserted via direct DB script or pre-deploy migration (per Note 20). The seeder does not run for Production.

**Note 32 — Config layer migration rule, complete or leave intact:** When migrating a model identifier from a legacy config key (e.g., `SubtitleProcessing:Claude:Model`) to the canonical `AIProviders:*` registry, the migration must be atomic end-to-end: (1) add `IOptions<AIProviderOptions>` to the service constructor, (2) change the model read to the new property, (3) remove the old C# property from the settings class, (4) remove the old key from `appsettings.json`, (5) update the validator if it checked the old property — all in the same commit. A partial migration (changing C# defaults to empty without updating the service read) leaves the system in a state worse than either old or new architecture: the old key in config is silently ignored, the new key may not be set, and the first actual API call fails at runtime rather than at startup. The §5.28 → ElevenLabs `unsupported_model` P0 (2026-06-22) is the reference incident.

Option B (2026-06-22) is the canonical example of this rule applied end-to-end — see BACKLOG §5.29 follow-up item 3 closure and §5.34.

**Note 33 — Multi-chunk migration test runs must rebuild, not `--no-build`.** When a migration spans multiple chunks (e.g., Option B Chunks 1 + 2), running `dotnet test --no-build` between chunks can use a stale compiled binary from an earlier chunk and produce false test failures against logic that no longer exists in source. The Option B Chunk 2 verification initially showed 478 false failures because the loaded test binary still contained Chunk 1's now-removed `SubtitleProcessingSettingsValidator.ElevenLabs.Model` check. Always run a full build (`dotnet build` then `dotnet test`, or `dotnet test` without `--no-build`) when verifying across chunk boundaries. The trade-off is build time vs. trustworthy results — trust wins.

## Backlog

### High

- Bulk SOP import — allow admins to upload multiple SOPs as background batch job with notification
- Cost analysis of running the application, breakdown by number of employees, video sizes, PDF sizes, translations, number of languages, re-translations, validation etc.
- Demo deploy — push current code to the decoupled demo Railway instance. Blocked on Railway CLI account-scoping issue (company projects not visible from CLI). Requires: 14 missing env vars (TranslationValidation block + Cors\_\_AllowedOrigins), demo database backup, then deploy. Re-test User Creation page afterward (expected to self-resolve as a staleness artifact)

### Medium

- **Other services with Hangfire implicit-HTTP dependency (audit, low urgency)**. `TranslationWorkflowService` was fixed in chunk 5.4 (tenant-context fix) by threading `Guid? explicitTenantId` through all non-token public methods. A sweep of the remaining Infrastructure services revealed no other services with both: (a) `TenantEntity` reads/writes and (b) a Hangfire job call site. If any new service is added that meets both criteria, apply the same explicit-tenant pattern documented in `PHASE_5_STANDARDS.md §7.5`. A unified `ServiceContext` abstraction (replacing the ad-hoc parameter approach) remains a future refactor option — low urgency while there is only one instance.
- **[BACKLOG NOTE — do not run as a prompt] Bulk import — partial-row recovery**. If a process restart interrupts the job between creating an employee and creating its linked user account, the re-run classifies the row as `AlreadyExisted` (employee exists) but the missing user account is never created — the row appears successful in the report but the person has no login. Build a proper fix that REUSES `EmployeeService`'s linked-user-account creation; do NOT duplicate that logic into the job. Edge case; low real-world frequency.
- Long-running job UX — fire-and-notify — bulk import (and other long jobs: content generation, translation validation, corpus runs) currently expect the user to wait on-screen for completion. Move to a "kick off and get notified when done" model so users aren't tied to a progress screen. Notification mechanism (in-app, email) to be decided. Cross-cutting — not specific to one feature
- Unify user creation on throwaway-password + invitation-email flow across all three creation paths:
  UI user-create form — currently admin-sets-password; remove password fields, use throwaway token, send invitation email, EmailConfirmed=false
  Tenant-create flow (Contact Name + Email → Admin user) — currently no password, no email; use throwaway token, send invitation email, EmailConfirmed=true (SuperUser implicitly confirms the address)
  Bulk employee import — already does this correctly; reference implementation
  All three should call the same underlying creation logic in EmployeeService / UserService; do not duplicate the throwaway-token + invitation-send logic across paths. The EmployeeService.CreateAsync sendInvitationEmail parameter added in Stage 2 of the bulk import is the existing seam.
- Evaluate per-tenant email uniqueness — email is currently globally unique, enforced twice: RequireUniqueEmail = true (Identity) and the UserNameIndex unique constraint (UserName = email). This blocks the moving-employee scenario (same person, new tenant, same personal email). Changing to per-tenant uniqueness is an auth-architecture change: needs a composite {TenantId, NormalizedEmail} index, altering/removing UserNameIndex, login disambiguation (which tenant for a given email), and ripple into password reset, set-password invitations, and live-data migration. Its own scoped project — not a quick fix. Potential UI retrofit: login flow gains tenant disambiguation
- CLAUDE.md accuracy — dead tenant-scoped duplicate check: UserService.CreateAsync contains a correctly tenant-scoped email duplicate pre-check that never executes, because the global Identity constraints (RequireUniqueEmail + UserNameIndex) reject the cross-tenant case first. Note this so future readers do not mistake it for evidence that email is per-tenant
- Video dubbing feature — currently pure scaffolding (entity, enum, settings flag only; no service/job/API/UI). A real build, not a toggle. Needs client scoping first: automated vs reviewed dubbing, language/video volume. The "dubbing OR subtitles" toggle is the trivial last 1%
- AI Chat Assistant — UI Help — knowledge base needs UI navigation paths augmented over time based on user questions
- Auto reschedule testing — verify refresher/reschedule flow at 6 month and 1 year intervals
- Translations as background task — run translation pipeline as background task with notification when complete
- Clickable wizard progress indicator — allow jumping back to any previously completed step
- Translation Pass Threshold plain language — replace 75% with Strict/Standard/Lenient labels
- Passing Score input improvement — add numeric input or slider alongside +/- buttons
- Auto-assign and Due-in-days visual grouping
- Source language detection audit
- Translation Quality Phase 2 — Safety term registry maintenance UI
- **MailerSendEmailProvider 429 handling**. Currently logs and silently drops HTTP 429 responses with no retry/backoff. A dropped invitation email is marked unsent and must be resent manually. Bulk employee import is the first feature with volume to realistically hit MailerSend's 10 req/sec API throughput limit. Add retry-after backoff. (Promoted from Low.)
- **R2 orphan file cleanup nightly job**. After the Development DB wipe, R2 still holds every file referenced by deleted tenants. A sweeper job should periodically identify R2 prefixes whose tenant no longer exists in the DB and remove them.
- Expand Content Creation E2E Tests
- YouTube Caption Integration
- Two-Factor Authentication (2FA)
- Cross-section remediation — document-level analysis
- Employee Training Audit & Reporting
- Dialect detection UI
- Sector preset quick-add
- Iteration guard
- Training Evidence Pack — extend sector-aware appendix to remaining sectors: Healthcare/HIQA, Construction/HSA, Homecare/HIQA, Manufacturing/HSA, Transport, General

### Low

- Pre-existing technical warnings — Model.Validation[10622] query-filter warnings on required relationships; DataProtection ephemeral keys on Railway. Noted, not urgent, not regressions
- AI Chat Assistant — Data Q&A — deferred until demand confirmed
- AI quiz generation on existing lesson edit
- Mixed voice in parse log
- Wizard step descriptions on hover
- Drag-to-reorder discoverability
- My Learnings no-op fix
- Active Learnings count clickable
- Demo deployment workflow — currently freeze-by-disconnect-from-GitHub. Each Demo refresh requires reconnecting, redeploying, then disconnecting. Cleaner pattern: a dedicated demo branch that Railway watches, with promotions done by git merge transval → demo. Lets Demo stay auto-deploy-from-its-own-branch while still being controlled (no accidental updates from Development churn). Same pattern that exists between transval (Development) and main (Production).
- **Tenant `IsActive` / sector remove for tenant admins**. `My Sectors` is add-only by design (avoids data-orphaning risk from removing a sector that's referenced by existing content). If demand emerges: either a soft-deactivation flag, or SuperUser-only remove with explicit confirmation.
- **SuperUser lockdown may be too strict on role assignment**. SuperUser currently cannot assign the Admin role to a user via UI — requires a DB workaround. If creating admin users becomes a frequent need, consider relaxing to "SuperUser can assign any role including Admin; non-SuperUsers cannot assign Admin."
- **Next.js 15+ `params` shape**. The redirect stubs created for moved regulatory routes use synchronous `params`. Next 16 may eventually require `await params`. Not breaking yet; apply when Next escalates the deprecation.

---

## Claude Code prompt conventions

Every Claude Code prompt sent during build sessions must include the
following preamble at the top, before the chunk-specific scope:

---

### Scope discipline

The prompt below defines the scope of this chunk. If during
implementation you discover that work outside that scope is required to
make the stated scope succeed — failing tests caused by pre-existing
infrastructure, broken seeders, missing interface implementations,
schema issues, migration problems, anything — STOP immediately and
report:

  - What you found
  - Why it blocks the stated scope
  - The smallest change that would unblock you
  - Whether it appears to be a pre-existing bug or freshly introduced

Do NOT fix it yourself. Wait for explicit go-ahead before touching
anything outside the stated scope. This applies even if the fix seems
obvious or trivial.

Pre-existing tests that fail for reasons unrelated to this chunk are
acceptable — note them in the final report but do not attempt to fix.

### Reporting format

The final report must be structured as:

  1. Test results — lead with the actual numbers, e.g.
     "320 of 321 passing, 1 pre-existing failure unrelated to this chunk"
     not "all green" or "tests pass"

  2. Files changed in scope — full paths, grouped by purpose

  3. Files changed outside the stated scope — full paths, with rationale
     for each. If this section is non-empty, the work is on hold
     pending explicit approval before commit.

  4. Build output — pass/fail and any warnings introduced by this chunk

### Commit discipline

Do not commit unless the prompt explicitly tells you to commit. "Ready
for commit" means "ready for review." Stop and report; wait for the
user's explicit "commit + push" before staging anything.

---

_Last Updated: June 7, 2026 (added the 'Claude Code prompt conventions' section)_
_Architecture: Modular Monolith with Clean Architecture_
