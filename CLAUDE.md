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
| Technology | Version | Purpose |
|------------|---------|---------|
| ASP.NET Core | 9.0 | Web API Framework |
| Entity Framework Core | 9.0 | ORM |
| PostgreSQL | Latest | Database |
| ASP.NET Identity | 9.0 | Authentication |
| FluentValidation | Latest | Request validation |
| Hangfire | Latest | Background job processing |
| SignalR | Latest | Real-time progress updates |
| QuestPDF | Latest | Certificate & validation report PDF generation |
| ClosedXML | 0.105.0 | Excel export (Skills Matrix) |
| Cloudflare R2 | — | File storage (videos, PDFs, subtitles, certificates, validation reports) |

### Frontend
| Technology | Version | Purpose |
|------------|---------|---------|
| Next.js | 16.0.10 | React framework (App Router) |
| React | 19.2.1 | UI library |
| TailwindCSS | 4.x | Styling |
| shadcn/ui | Latest | UI component library |
| TanStack Query | 5.90.12 | Data fetching & caching |
| React Hook Form | 7.68.0 | Form handling |
| Zod | 4.2.1 | Schema validation |
| Recharts | 3.6.0 | Charts & analytics |
| Axios | 1.13.2 | HTTP client |
| date-fns | 4.1.0 | Date utilities |
| @dnd-kit | Latest | Drag-and-drop (course item reordering) |
| @microsoft/signalr | 10.0.0 | Real-time hub client |

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
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/login` | Authenticate and get JWT tokens | No |
| POST | `/register` | Register new user | No |
| POST | `/refresh-token` | Refresh expired access token | No |
| POST | `/revoke-token` | Logout (revoke refresh token) | Yes |
| GET | `/me` | Get current user info + permissions | Yes |
| POST | `/set-password` | Set password from invitation link | No |

### Core Module

#### Users (`/api/users`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List users (paginated) | Core.ManageUsers |
| GET | `/{id}` | Get user by ID | Core.ManageUsers |
| POST | `/` | Create user | Core.ManageUsers |
| PUT | `/{id}` | Update user | Core.ManageUsers |
| DELETE | `/{id}` | Delete user | Core.ManageUsers |
| PUT | `/{id}/toggle-active` | Toggle user active status | Core.ManageUsers |
| PUT | `/{id}/roles` | Update user roles | Core.ManageUsers |

#### Roles (`/api/roles`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List all roles | Core.ManageRoles |
| GET | `/{id}` | Get role by ID | Core.ManageRoles |
| GET | `/permissions` | List all permissions | Core.ManageRoles |

#### Sites (`/api/sites`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List sites (paginated) | Core.ManageSites |
| GET | `/{id}` | Get site by ID | Core.ManageSites |
| POST | `/` | Create site | Core.ManageSites |
| PUT | `/{id}` | Update site | Core.ManageSites |
| DELETE | `/{id}` | Delete site (soft) | Core.ManageSites |

#### Employees (`/api/employees`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List employees (paginated) | Core.ManageEmployees |
| GET | `/{id}` | Get employee by ID | Core.ManageEmployees |
| POST | `/` | Create employee | Core.ManageEmployees |
| PUT | `/{id}` | Update employee | Core.ManageEmployees |
| DELETE | `/{id}` | Delete employee (soft, checks for active supervisor assignments) | Core.ManageEmployees |

#### Supervisor Assignments (`/api/employees`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/{supervisorId}/operators` | List operators assigned to a supervisor | Learnings.View |
| GET | `/{supervisorId}/operators/available` | List employees available for assignment | Learnings.View |
| POST | `/{supervisorId}/operators` | Assign operator to supervisor (restore-on-reassign) | Learnings.View |
| DELETE | `/{supervisorId}/operators/{operatorId}` | Unassign operator from supervisor (soft delete) | Learnings.View |
| GET | `/my-operators` | List current supervisor's operators (uses JWT employee_id) | Learnings.View |

> **Note:** All supervisor assignment endpoints use `Learnings.View` as the auth policy, with business-level scoping ensuring supervisors can only manage their own assignments.

#### Companies (`/api/companies`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List companies (paginated) | Core.ManageCompanies |
| GET | `/{id}` | Get company by ID | Core.ManageCompanies |
| POST | `/` | Create company | Core.ManageCompanies |
| PUT | `/{id}` | Update company | Core.ManageCompanies |
| DELETE | `/{id}` | Delete company (soft) | Core.ManageCompanies |

#### Contacts (`/api/contacts`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List contacts (paginated) | Core.ManageCompanies |
| GET | `/{id}` | Get contact by ID | Core.ManageCompanies |
| POST | `/` | Create contact | Core.ManageCompanies |
| PUT | `/{id}` | Update contact | Core.ManageCompanies |
| DELETE | `/{id}` | Delete contact (soft) | Core.ManageCompanies |

### Toolbox Talks Module

#### Toolbox Talks — Admin (`/api/toolbox-talks`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List toolbox talks (paginated, searchable, filterable) | ToolboxTalks.View |
| GET | `/{id}` | Get talk by ID with sections and questions | ToolboxTalks.View |
| GET | `/{id}/preview` | Preview talk as employee sees it | ToolboxTalks.View |
| GET | `/{id}/preview/slides` | Get slides for preview | ToolboxTalks.View |
| GET | `/{id}/slideshow-html` | Get AI-generated HTML slideshow | ToolboxTalks.View |
| POST | `/` | Create a new toolbox talk | ToolboxTalks.Create |
| PUT | `/{id}` | Update a toolbox talk | ToolboxTalks.Edit |
| DELETE | `/{id}` | Delete a toolbox talk (soft) | ToolboxTalks.Delete |
| GET | `/dashboard` | Dashboard KPIs and statistics | ToolboxTalks.View |
| GET | `/settings` | Get tenant settings | ToolboxTalks.View |
| PUT | `/settings` | Update tenant settings | ToolboxTalks.Admin |
| POST | `/{id}/extract-content` | Extract content from video/PDF for AI generation | ToolboxTalks.Admin |
| POST | `/{id}/check-duplicate` | Check file deduplication | ToolboxTalks.Admin |
| POST | `/{id}/reuse-content` | Reuse content from another talk | ToolboxTalks.Admin |
| POST | `/{id}/update-file-hash` | Update file hash for dedup tracking | ToolboxTalks.Admin |
| POST | `/{id}/generate` | Start AI content generation (Hangfire + SignalR) | ToolboxTalks.Admin |
| POST | `/{id}/generate-slides` | Generate AI HTML slideshow from PDF | ToolboxTalks.Edit |
| POST | `/{id}/smart-generate` | Smart content generation (dedup + AI) | ToolboxTalks.Edit |
| POST | `/{id}/translations/generate` | Generate content translations | ToolboxTalks.Admin |
| GET | `/{id}/translations` | Get existing translations | ToolboxTalks.View |

#### Toolbox Talks — Reports (`/api/toolbox-talks/reports`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/compliance` | Compliance report by department/talk | ToolboxTalks.View |
| GET | `/overdue` | Overdue assignments list | ToolboxTalks.View |
| GET | `/completions` | Detailed completion records (paginated) | ToolboxTalks.View |
| GET | `/skills-matrix` | Skills matrix: employees × learnings grid with cell statuses | ToolboxTalks.View |
| GET | `/overdue/export` | Export overdue report as Excel | ToolboxTalks.View |
| GET | `/completions/export` | Export completions as Excel | ToolboxTalks.View |
| GET | `/compliance/export` | Export compliance as PDF | ToolboxTalks.View |
| GET | `/skills-matrix/export` | Export skills matrix as colour-coded Excel (ClosedXML) | ToolboxTalks.View |

#### Toolbox Talks — Certificates (`/api/toolbox-talks/certificates`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/report` | Certificate report with summary stats | ToolboxTalks.View |
| GET | `/by-employee/{employeeId}` | Certificates for a specific employee | ToolboxTalks.View |
| GET | `/{id}/download` | Download certificate PDF (admin) | ToolboxTalks.View |

#### My Toolbox Talks — Employee Portal (`/api/my/toolbox-talks`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List assigned talks (paginated, filterable) | Authenticated |
| GET | `/{id}` | Get assigned talk with full content | Authenticated |
| POST | `/{id}/start` | Start a toolbox talk (captures geolocation) | Authenticated |
| POST | `/{id}/sections/{sectionId}/read` | Mark a section as read | Authenticated |
| POST | `/{id}/quiz/submit` | Submit quiz answers | Authenticated |
| POST | `/{id}/video-progress` | Update video watch progress | Authenticated |
| POST | `/{id}/reset-video-progress` | Reset video progress | Authenticated |
| POST | `/{id}/complete` | Complete talk with signature + geolocation | Authenticated |
| GET | `/pending` | Pending talks | Authenticated |
| GET | `/in-progress` | In-progress talks | Authenticated |
| GET | `/overdue` | Overdue talks | Authenticated |
| GET | `/completed` | Completed talks | Authenticated |
| GET | `/summary` | Summary counts (pending, in-progress, overdue) | Authenticated |
| GET | `/{id}/subtitles/status` | Subtitle processing status | Authenticated |
| GET | `/{id}/subtitles/{languageCode}` | Get subtitle file (SRT/WebVTT) | Authenticated |
| GET | `/{id}/slides` | Get slides with optional translation | Authenticated |
| GET | `/{id}/slideshow` | Get HTML slideshow with optional translation | Authenticated |
| GET | `/courses` | Course assignments for current employee | Authenticated |
| GET | `/courses/{id}` | Specific course assignment | Authenticated |
| GET | `/certificates` | Employee's certificates | Authenticated |
| GET | `/certificates/{id}/download` | Download certificate PDF | Authenticated |

#### Scheduled Talks (`/api/toolbox-talks/assigned`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List all assignments (paginated, filterable) | ToolboxTalks.View |
| GET | `/{id}` | Get assignment by ID | ToolboxTalks.View |
| GET | `/by-employee/{employeeId}` | Assignments for employee | ToolboxTalks.View |
| GET | `/overdue` | Overdue assignments | ToolboxTalks.View |
| GET | `/pending` | Pending assignments | ToolboxTalks.View |
| GET | `/in-progress` | In-progress assignments | ToolboxTalks.View |
| GET | `/completed` | Completed assignments | ToolboxTalks.View |
| POST | `/{id}/reminder` | Send reminder | ToolboxTalks.Schedule |
| DELETE | `/{id}` | Cancel assignment | ToolboxTalks.Schedule |

#### Schedules (`/api/toolbox-talks/schedules`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List schedules (paginated, filterable) | ToolboxTalks.View |
| GET | `/{id}` | Get schedule with assignments | ToolboxTalks.View |
| POST | `/` | Create schedule | ToolboxTalks.Schedule |
| PUT | `/{id}` | Update schedule | ToolboxTalks.Schedule |
| DELETE | `/{id}` | Cancel schedule | ToolboxTalks.Schedule |
| POST | `/{id}/process` | Process schedule to create assignments | ToolboxTalks.Schedule |

#### Courses (`/api/toolbox-talks/courses`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List courses (searchable, filterable) | ToolboxTalks.View |
| GET | `/{id}` | Get course with items and translations | ToolboxTalks.View |
| POST | `/` | Create course | ToolboxTalks.Create |
| PUT | `/{id}` | Update course | ToolboxTalks.Edit |
| DELETE | `/{id}` | Delete course (soft) | ToolboxTalks.Delete |
| POST | `/{id}/items` | Add talk to course | ToolboxTalks.Edit |
| DELETE | `/{id}/items/{talkId}` | Remove talk from course | ToolboxTalks.Edit |
| PUT | `/{id}/items` | Reorder/bulk update items | ToolboxTalks.Edit |

#### Course Assignments (`/api/toolbox-talks/course-assignments`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/preview` | Preview assignment (shows completed talks) | ToolboxTalks.View |
| POST | `/` | Assign course to employees | ToolboxTalks.Create |
| GET | `/by-course/{courseId}` | Assignments for a course | ToolboxTalks.View |
| GET | `/{id}` | Get course assignment details | ToolboxTalks.View |
| DELETE | `/{id}` | Delete course assignment | ToolboxTalks.Delete |

#### Subtitle Processing (`/api/toolbox-talks/{toolboxTalkId}/subtitles`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/process` | Start subtitle processing (transcription + translation) | ToolboxTalks.Edit |
| GET | `/status` | Get processing status | ToolboxTalks.View |
| POST | `/cancel` | Cancel active processing | ToolboxTalks.Edit |
| POST | `/retry` | Retry failed translations | ToolboxTalks.Edit |
| GET | `/{languageCode}` | Download subtitle file (SRT/WebVTT) | ToolboxTalks.View |

#### File Management (`/api/toolbox-talks/{toolboxTalkId}`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/video` | Upload video (max 500MB) | ToolboxTalks.Edit |
| POST | `/pdf` | Upload PDF (max 50MB) | ToolboxTalks.Edit |
| PUT | `/video-url` | Set external video URL | ToolboxTalks.Edit |
| DELETE | `/video` | Delete video | ToolboxTalks.Edit |
| DELETE | `/pdf` | Delete PDF | ToolboxTalks.Edit |
| DELETE | `/files` | Delete all files | ToolboxTalks.Edit |

#### Translation Validation (`/api/toolbox-talks/{talkId}/validation`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/validate` | Start new validation run | ToolboxTalks.Admin |
| GET | `/runs` | List validation runs (paginated) | ToolboxTalks.View |
| GET | `/runs/{runId}` | Get run with all results | ToolboxTalks.View |
| PUT | `/runs/{runId}/sections/{idx}/accept` | Reviewer accepts section | ToolboxTalks.Admin |
| PUT | `/runs/{runId}/sections/{idx}/reject` | Reviewer rejects section | ToolboxTalks.Admin |
| PUT | `/runs/{runId}/sections/{idx}/edit` | Reviewer edits & re-validates | ToolboxTalks.Admin |
| POST | `/runs/{runId}/sections/{idx}/retry` | Retry section validation | ToolboxTalks.Admin |
| GET | `/runs/{runId}/report` | Download audit report PDF | ToolboxTalks.View |
| POST | `/runs/{runId}/report/generate` | Generate audit report PDF | ToolboxTalks.Admin |
| DELETE | `/runs/{runId}` | Soft-delete validation run | ToolboxTalks.Admin |

#### Sectors (`/api/toolbox-talks/sectors`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List all active system-wide sectors | Authenticated |

#### Tenant Sectors (`/api/tenants/{tenantId}/sectors`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List tenant's sectors (tenant ID guard: own tenant only unless SuperUser) | Authenticated |
| POST | `/` | Assign sector to tenant | Tenant.Manage |
| DELETE | `/{sectorId}` | Remove sector from tenant (400 if last sector) | Tenant.Manage |
| PUT | `/{sectorId}/set-default` | Set sector as tenant default | Tenant.Manage |

#### Regulatory Scoring (`/api/toolbox-talks/validation-runs/{runId}`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| POST | `/regulatory-score` | Trigger regulatory scoring run | Learnings.Admin |
| GET | `/regulatory-score/history` | Get score history for a validation run | Learnings.View |

#### Course Validation (`/api/toolbox-talks/courses/{courseId}`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/validation-runs` | List validation runs for a course | ToolboxTalks.View |
| GET | `/validation/runs/{runId}` | Get course-level validation run detail | ToolboxTalks.View |

#### Safety Glossary (`/api/toolbox-talks/glossaries`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/` | List glossaries (system defaults + tenant overrides) | ToolboxTalks.View |
| GET | `/{id}` | Get glossary with terms | ToolboxTalks.View |
| POST | `/` | Create tenant glossary | ToolboxTalks.Admin |
| PUT | `/{id}` | Update glossary | ToolboxTalks.Admin |
| DELETE | `/{id}` | Delete glossary (tenant only) | ToolboxTalks.Admin |
| POST | `/{id}/terms` | Add term to glossary | ToolboxTalks.Admin |
| PUT | `/{id}/terms/{termId}` | Update glossary term | ToolboxTalks.Admin |
| DELETE | `/{id}/terms/{termId}` | Delete glossary term | ToolboxTalks.Admin |

#### Regulatory Ingestion (`/api/regulatory`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/documents` | List all regulatory documents with body, profiles, counts | Tenant.Manage |
| POST | `/documents/{documentId}/ingest` | Start AI ingestion from document URL | Tenant.Manage |
| GET | `/documents/{documentId}/ingestion-status` | Get ingestion status and counts | Tenant.Manage |
| GET | `/documents/{documentId}/draft-requirements` | List draft requirements for review | Tenant.Manage |
| PUT | `/requirements/{requirementId}/approve` | Approve draft (with optional edits) | Tenant.Manage |
| PUT | `/requirements/{requirementId}/reject` | Reject draft with notes | Tenant.Manage |
| PUT | `/requirements/{requirementId}` | Update draft without status change | Tenant.Manage |
| POST | `/documents/{documentId}/approve-all` | Bulk approve all drafts | Tenant.Manage |

#### Requirement Mappings (`/api/toolbox-talks/requirement-mappings`)
| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| GET | `/pending` | Get pending mappings summary + list | Learnings.Admin |
| PUT | `/{mappingId}/confirm` | Confirm an AI-suggested mapping | Learnings.Admin |
| PUT | `/{mappingId}/reject` | Reject an AI-suggested mapping | Learnings.Admin |
| POST | `/confirm-all` | Confirm all suggested mappings for tenant | Learnings.Admin |
| GET | `/unconfirmed-count?toolboxTalkId=&courseId=` | Count unconfirmed mappings for content | Learnings.Admin |
| GET | `/compliance/{sectorKey}` | Get compliance checklist for sector | Learnings.Admin |
| POST | `/manual` | Create manual confirmed mapping (no AI) | Learnings.Admin |
| GET | `/content-options` | List published talks and courses for mapping dropdown | Learnings.Admin |
| POST | `/compliance/{sectorKey}/generate-report` | Generate inspection readiness report PDF | Learnings.Admin |

---

## Frontend Pages

### Public Pages
| Path | Description |
|------|-------------|
| `/login` | Login page |
| `/auth/set-password` | Set password from invitation link |

### Authenticated Pages

#### Dashboard
| Path | Description |
|------|-------------|
| `/dashboard` | Module selector (Toolbox Talks, Administration) |

#### Toolbox Talks — Employee Portal (`/toolbox-talks/*`)
| Path | Description |
|------|-------------|
| `/toolbox-talks` | Employee training dashboard |
| `/toolbox-talks/[id]` | View and complete an assigned talk |
| `/toolbox-talks/courses/[id]` | Course detail and progress |
| `/toolbox-talks/certificates` | View earned certificates |
| `/toolbox-talks/team` | My Team page (Supervisor only — assign/unassign operators) |
| `/toolbox-talks/team/skills-matrix` | Skills Matrix (Supervisor view — assigned operators × learnings) |

#### Admin — Toolbox Talks (`/admin/toolbox-talks/*`)
| Path | Description |
|------|-------------|
| `/admin/toolbox-talks` | Overview dashboard with KPIs |
| `/admin/toolbox-talks/talks` | List all talks |
| `/admin/toolbox-talks/talks/new` | Create talk (6-step wizard: Input & Config → Parse → Quiz → Settings → Translate & Validate → Publish) |
| `/admin/toolbox-talks/talks/[id]` | View talk details |
| `/admin/toolbox-talks/talks/[id]/edit` | Edit talk |
| `/admin/toolbox-talks/courses` | List courses |
| `/admin/toolbox-talks/courses/new` | Create course |
| `/admin/toolbox-talks/courses/[id]/edit` | Edit course |
| `/admin/toolbox-talks/schedules` | List schedules |
| `/admin/toolbox-talks/schedules/new` | Create schedule |
| `/admin/toolbox-talks/schedules/[id]` | View schedule details |
| `/admin/toolbox-talks/assignments` | List individual assignments |
| `/admin/toolbox-talks/reports` | Reports landing |
| `/admin/toolbox-talks/reports/compliance` | Compliance report |
| `/admin/toolbox-talks/reports/completions` | Completion records |
| `/admin/toolbox-talks/reports/overdue` | Overdue assignments |
| `/admin/toolbox-talks/reports/skills-matrix` | Skills Matrix (Admin view — all employees × learnings) |
| `/admin/toolbox-talks/certificates` | Certificate management |
| `/admin/toolbox-talks/pending-mappings` | Pending requirement mappings review (Learnings.Admin) |
| `/admin/toolbox-talks/compliance` | Compliance checklist with sector tabs (Learnings.Admin) |
| `/admin/toolbox-talks/settings` | Module settings (includes glossary management, threshold config) |
| `/admin/toolbox-talks/talks/[id]/validation` | Validation history tab (list of runs for a talk) |
| `/admin/toolbox-talks/talks/[id]/validation/[runId]` | Validation run detail (section results, reviewer decisions, report download) |
| `/admin/toolbox-talks/courses/[id]/validation/[runId]` | Course-level validation run detail |

#### Admin — Core (`/admin/*`)
| Path | Description |
|------|-------------|
| `/admin` | Admin module home |
| `/admin/sites` | List sites |
| `/admin/sites/new` | Create site |
| `/admin/sites/[id]/edit` | Edit site |
| `/admin/employees` | List employees |
| `/admin/employees/new` | Create employee |
| `/admin/employees/[id]` | Employee detail view (read-only summary, certificates, assigned operators for Supervisors) |
| `/admin/employees/[id]/edit` | Edit employee |
| `/admin/companies` | List companies |
| `/admin/companies/new` | Create company |
| `/admin/companies/[id]` | View company with contacts |
| `/admin/companies/[id]/edit` | Edit company |
| `/admin/users` | List users |
| `/admin/users/new` | Create user |
| `/admin/users/[id]/edit` | Edit user |

#### Admin — Regulatory (SuperUser only) (`/admin/regulatory/*`)
| Path | Description |
|------|-------------|
| `/admin/regulatory` | Regulatory documents list with ingestion status |
| `/admin/regulatory/[documentId]` | Document detail, ingestion trigger, draft requirement review |

#### User
| Path | Description |
|------|-------------|
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
| Permission | Description |
|------------|-------------|
| `Core.ManageSites` | Manage sites |
| `Core.ManageEmployees` | Manage employees |
| `Core.ManageCompanies` | Manage companies and contacts |
| `Core.ManageUsers` | Manage user accounts |
| `Core.ManageRoles` | Manage roles and permissions |
| `Core.Admin` | Full core system administration |

#### Toolbox Talks Module
| Permission | Description |
|------------|-------------|
| `ToolboxTalks.View` | View toolbox talks and reports |
| `ToolboxTalks.Create` | Create new toolbox talks |
| `ToolboxTalks.Edit` | Edit existing toolbox talks |
| `ToolboxTalks.Delete` | Delete toolbox talks |
| `ToolboxTalks.Schedule` | Manage schedules and assignments |
| `ToolboxTalks.ViewReports` | View toolbox talk reports |
| `ToolboxTalks.Admin` | Full toolbox talks administration |

#### Learnings Module
| Permission | Description |
|------------|-------------|
| `Learnings.View` | View learnings, manage team assignments (Supervisor) |
| `Learnings.Schedule` | Schedule and assign learnings to team members |

### Roles
| Role | Description |
|------|-------------|
| **Admin** | All permissions |
| **Supervisor** | `Learnings.View`, `Learnings.Schedule` only — manages team via "My Team" page, not the Employees admin section |
| **Operator** | No admin permissions — default user role; employee-facing pages only (My Learnings, My Certificates) |
| **Finance** | View-only access |
| **OfficeStaff** | Core admin + Toolbox Talks view |
| **SiteManager** | Site operations + Toolbox Talks view |
| **WarehouseStaff** | Warehouse operations |

> **Note:** The `DefaultUserRole` constant is `"Operator"` (previously `"SiteManager"`). The seeder includes `CleanupSupervisorPermissionsAsync` to remove stale permissions (e.g., `Core.ManageEmployees`, `Core.ManageSites`) from existing Supervisor roles.

### Report Scoping
Reports (compliance, overdue, completions, certificates, dashboard) are auto-scoped by role via `ToolboxTalksController.ResolveScopedEmployeeIdsAsync()`:

| Role | Scope | employeeIds |
|------|-------|-------------|
| **SuperUser / Admin** | All data | `null` (no filter) |
| **Supervisor** | Assigned operators only | Resolved from SupervisorAssignments |
| **Operator** | Own data only | Current user's EmployeeId |

### ICurrentUserService
Now includes `EmployeeId` (`Guid?`) resolved from the JWT `employee_id` claim, in addition to existing `UserId`, `TenantId`, etc.

### Frontend Auth
The `User` TypeScript type now includes `employeeId` (`string | null`), populated from the `/api/auth/me` response.

### Navigation by Role

| Role | Profile Menu | Employee Nav Items |
|------|-------------|-------------------|
| **Admin / SuperUser** | "Administration" → all admin tabs | N/A (admin-focused) |
| **Supervisor** | "Training Management" → Learnings tab only | My Learnings, My Certificates, My Team, Skills Matrix, Team Reports |
| **Operator** | No admin access | My Learnings, My Certificates |

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

| Job | Schedule | Description |
|-----|----------|-------------|
| ContentGenerationJob | On-demand | AI content generation from video/PDF. Slideshow-only mode (`GenerateSlideshowOnlyAsync`) chains to MissingTranslationsJob immediately after saving slideshow HTML — ensures translated slideshow delivery without timing gap. |
| MissingTranslationsJob | On-demand | Single-talk translation gap fill. Triggered by content generation, smart-generate reuse, direct content reuse, new employee language detection, and ContentGenerationJob after slideshow generation. Completeness check logic: checks TranslatedTitle, TranslatedSections, TranslatedQuestions (when RequiresQuiz=true), and ToolboxTalkSlideshowTranslation existence (when SlidesGenerated=true) — not just record existence. |
| DailyTranslationScanJob | Daily at 2am UTC | Scans talks created/modified in last 25 hours across all tenants for translation gaps. Dispatches MissingTranslationsJob per gap found. |
| ProcessToolboxTalkSchedulesJob | Daily | Process active schedules to create assignments |
| SendRefresherRemindersJob | Daily | Send reminders for upcoming refresher due dates |
| SendToolboxTalkRemindersJob | Daily | Send reminders for overdue/pending talks |
| UpdateOverdueToolboxTalksJob | Daily | Mark past-due assignments as Overdue |
| TranslationValidationJob | On-demand | Multi-round back-translation consensus validation per section (SignalR progress) |
| ValidationReportJob | On-demand | Generate audit report PDF (QuestPDF) and upload to R2 |
| ExpiredSessionCleanupJob | Daily | Clean up expired validation sessions |
| RequirementIngestionJob | On-demand | AI-powered extraction of regulatory requirements from document URLs (Claude Sonnet) |
| RequirementMappingJob | On-demand | AI-powered mapping of published content to regulatory requirements (Claude Sonnet), triggered from publish flow |

---

## HTTP Resilience & Retry Policies

All external HTTP calls use Polly retry policies defined in `ResiliencePolicies.cs` (located in `QuantumBuild.Core.Application/Http/`).

### Policies

| Policy | Retries | Backoff | Triggers | Used By |
|--------|---------|---------|----------|---------|
| `GetClaudePolicy` | 3 | 2s/4s/8s exponential + ±500ms jitter | `HttpRequestException`, HTTP 429/500/502/503/529 | Claude API clients |
| `GetElevenLabsPolicy` | 2 | 2s/4s exponential + jitter | Same as Claude | ElevenLabs transcription |
| `GetTransientPolicy` | 3 | 1s/2s/4s exponential | `HttpRequestException`, HTTP 408/429/5xx (standard transient) | DeepL, Gemini, DeepSeek |

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
| Migration | Description |
|-----------|-------------|
| AddRegulatoryRequirements | RegulatoryRequirement + RegulatoryRequirementMapping entities, check constraint, indexes |
| AddLastIngestedAtToRegulatoryDocument | LastIngestedAt nullable DateTimeOffset on RegulatoryDocument |
| AddReviewNotesToRegulatoryRequirementMappings | ReviewNotes column on RegulatoryRequirementMapping |
| FixPrincipleLabelCanonicalForm | Normalise PrincipleLabel to use "&" instead of "and" |
| AddCorpusEntities | AuditCorpora, AuditCorpusEntries, CorpusRuns, CorpusRunResults, ProviderResultCache tables + Status column on PipelineChangeRecords (default 'Draft') |

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
| Component | Location | Description |
|-----------|----------|-------------|
| DataTable | `components/shared/data-table.tsx` | Paginated table with sorting, search |
| DeleteConfirmationDialog | `components/shared/delete-confirmation-dialog.tsx` | Confirm delete with toast |
| ExportButtons | `components/shared/export-buttons.tsx` | Export functionality |
| PendingTrainingBanner | `components/shared/pending-training-banner.tsx` | Training reminder banner |
| TopNav | `components/layout/top-nav.tsx` | Header with user dropdown |

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
  queryKey: ['toolbox-talks', page, search],
  queryFn: () => apiClient.get('/toolbox-talks', { params: { page, search } })
});

const mutation = useMutation({
  mutationFn: (data) => apiClient.post('/toolbox-talks', data),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['toolbox-talks'] });
    toast.success('Created');
  }
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
- Migrations named descriptively

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
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/toolbox-talks/reports/skills-matrix?category=` | Skills matrix grid data |
| GET | `/api/toolbox-talks/reports/skills-matrix/export?category=` | Excel export (ClosedXML) |

### Response: `SkillsMatrixDto`
- **Employees** (rows) — name, code, site
- **Learnings** (columns) — talk code, title, category
- **Cells** — status per employee × learning combination

### Cell Statuses (5)
| Status | Description |
|--------|-------------|
| **Completed** | With score percentage |
| **InProgress** | Currently in progress |
| **Overdue** | With days overdue count |
| **Assigned** | Assigned but not started |
| **NotAssigned** | No assignment exists |

### Data Derivation (Admin Path)
- Both employees and learnings are derived from `ScheduledTalks` (not independent ToolboxTalk query)
- Unassigned employees included for compliance visibility

### Role Scoping
| Role | Employees Shown |
|------|----------------|
| Admin / SuperUser | All employees |
| Supervisor | Assigned operators only |
| Operator | Self only |

### Frontend Pages
| Path | Context |
|------|---------|
| `/toolbox-talks/team/skills-matrix` | Supervisor view (nav: after My Team, before Team Reports) |
| `/admin/toolbox-talks/reports/skills-matrix` | Admin view (from Reports landing page) |

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
| Service | Purpose |
|---------|---------|
| **LexicalScoringService** | Token-overlap similarity scoring (0-100) between original and back-translated text |
| **WordDiffService** | LCS-based word-level diff with Insert/Delete/Equal operations and similarity percentage |
| **ConsensusEngine** | Escalating multi-round back-translation: Round 1 (Haiku + DeepL), Round 2 (+Gemini), Round 3 (+DeepSeek) |
| **SafetyClassificationService** | Glossary term detection + regex patterns (prohibition, emergency, hazard) for safety-critical content |
| **GlossaryTermVerificationService** | Verifies expected glossary translations are present in translated text |
| **TranslationValidationService** | Orchestrator: safety classify → bump threshold → consensus → glossary verify → persist result |
| **TranslationValidationJob** | Hangfire job orchestrator. Injects `ISafetyClassificationService` for per-section safety classification. Pre-loads glossary terms via `LoadGlossaryTermsAsync` once per run (not per section) — prefers tenant override over system default |
| **ValidationReportService** | QuestPDF-based audit report: cover page, executive summary, per-section details, colour-coded outcomes |
| **RegulatoryScoreService** | Claude Sonnet scoring: source document quality, pure linguistic translation, regulatory-aware translation with sector criteria |

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
| Test Class | Coverage |
|------------|----------|
| **LexicalScoringServiceTests** | Identical strings, partial overlap, empty strings, case insensitivity, punctuation stripping |
| **WordDiffServiceTests** | LCS algorithm, Insert/Delete/Equal operations, similarity percentage |
| **ConsensusEngineTests** | Round 1-3 escalation, threshold pass/review/fail, agreement tolerance |
| **SafetyClassificationServiceTests** | Glossary detection, regex patterns (prohibition, emergency, hazard), critical term extraction |

### Integration Tests
| Test Class | Coverage |
|------------|----------|
| **TranslationValidationTests** | Multi-tenant isolation, glossary CRUD, system default protection, validation run lifecycle, report generation, reviewer decisions |

---

## Notes for Claude Code

Archived notes 1-89 are in CLAUDE-archive.md

1. **Glossary hard-block enforcement — pre-scoring replacement pass** — `GlossaryReplacementService` applies case-insensitive regex replacement of locked glossary terms BEFORE the consensus engine runs, so the corrected text is what gets back-translated and scored. Insertion point is step 2 in `TranslationValidationService.ValidateSectionAsync()` — after initial translation is returned from the provider, before consensus. Corrections stored as `GlossaryCorrectionsJson` (nullable string) and `GlossaryHardBlockApplied` (nullable bool) on `TranslationValidationResult` for audit. Title translation now also receives glossary instructions via `TranslationValidationJob` — previously excluded from Tier 4 prompt injection. Frontend shows amber Wand2 badge on `ValidationSectionCard` when corrections were applied.
2. **Pipeline v6.4: DeepSeek removed as Round 3 back-translation provider** — GDPR risk (indefinite retention, China-based servers). Replaced with Claude Sonnet (`claude-sonnet-4-20250514`). Round 3 fires only when Rounds 1 and 2 disagree. `DeepSeekTranslationService.cs` and `IDeepSeekTranslationService.cs` retained but marked `[Obsolete]` — do not re-enable without GDPR legal review. New provider pair: `IClaudeSonnetBackTranslationService` / `ClaudeSonnetBackTranslationService` — same Anthropic API key as Haiku, logs AI usage via `IAiUsageLogger`. `TranslationValidationSettings.DeepSeek` property also marked `[Obsolete]`. `appsettings.json` retains the DeepSeek section (inert) with `PipelineVersion: "6.4"` and `Round3Provider: "claude-sonnet-4-20250514"` added to the `TranslationValidation` config block.
3. **General System Audit Log** — `SystemAuditLog` entity is append-only, no `BaseEntity`, no soft-delete, nullable `TenantId` as a plain field (not FK with query filter) so it survives tenant resets and captures SuperUser cross-tenant actions. `ISystemAuditLogger` resolves user context from `ICurrentUserService` internally and swallows exceptions so audit logging never breaks the calling operation. `AuditActions` static constants define all action strings. Wired into: full auth path (login success/failure/inactive, logout, token refresh, password set), `UsersController`, `TenantsController` (`ResetData` always audited regardless of outcome), `TenantModulesController`, `EmployeesController`. `LastLoginAt` and `LastLoginIp` added to `User` entity — set on successful login. `DeletedBy` added to `BaseEntity` — set on all soft deletes. `AccessFailedAsync` now called on wrong password — lockout threshold works correctly for the first time.
4. **Translation Pipeline Audit Controls — Phase 1** — `PipelineVersion` entity is system-level (no TenantId), append-only hash record of pipeline configuration. Hash is SHA-256 of `ComponentsJson` (models, thresholds, prompt version, processing mode) — first 12 hex chars prefixed with `"sha256:"`. Only one active record at a time — `IsActive` flag managed by `PipelineVersionService`. `PipelineChangeRecord` is also system-level and append-only — no update or delete endpoints, ever. `TranslationValidationRun` now carries `PipelineVersionId` (nullable for graceful degradation on pre-existing runs). Claude Haiku and Sonnet model IDs moved from hardcoded constants to `TranslationValidationSettings` (`Round1AModel`, `Round3DModel`). `AgreementThreshold` moved from hardcoded `ConsensusEngine` constant to config. `PromptVersion` added to config — bump manually on prompt changes. `PipelineVersionService.GetOrCreateCurrentAsync()` called on startup to ensure active version always exists. Phase 2: Deviations + Modules tab + Dashboard. Phase 3: Term Gate UI. Phase 4: Corpus.
5. **Translation Pipeline Audit Controls — Phase 2** — `TranslationDeviation` is TenantEntity with sequential `DeviationId` per tenant (DEV-001 resets per tenant; CR-0001 is system-wide). CAPA fields: Nature, RootCauseCategory, RootCauseDetail, CorrectiveAction, PreventiveAction, Approver, Status (Open/InProgress/Closed). SetNull on `ValidationRunId` and `ValidationResultId` so deviations survive run deletion. `PipelineAuditController` at `/api/toolbox-talks/pipeline` — `POST /changes` is SuperUser only, bumps active pipeline version. Flag Deviation button on `ValidationSectionCard` only appears after reviewer makes a decision (never on Pending). Pipeline Audit page at `/admin/toolbox-talks/pipeline` — Changes tab hidden entirely for non-SuperUser (not just disabled). Modules tab links to existing run detail pages. Dashboard locked terms count comes from `SafetyGlossaryTerms`. Phase 3 next: Term Gate UI backed by `GlossaryTermVerificationService`. Phase 4: Corpus (most complex — involves live pipeline calls).
6. **Translation Pipeline Audit Controls — Phase 3** — Term Gate UI added as 5th tab on `/admin/toolbox-talks/pipeline`. `POST /term-gate/check` runs gate logic against tenant + system default glossary terms (tenant override preferred). Two failure types: `missing_approved` (approved translation not found in target) and `forbidden_present` (forbidden variant detected via `ISafetyTermRegistryService`). `GET /term-gate/summary` returns term counts, per-sector breakdown, and language coverage. Gate tester is informational only — writes nothing to DB unless user explicitly clicks "Log as Deviation" which pre-fills the deviation form with nature, root cause category, source/target excerpts, and language pair.
7. **Translation Pipeline Audit Controls — Phase 4 — Corpus** — 5 new entities: `AuditCorpus` (TenantEntity, sequential CorpusId per tenant), `AuditCorpusEntry` (frozen section text + expected outcome + thresholds), `CorpusRun` (TenantEntity, execution record with verdict/stats/cost), `CorpusRunResult` (per-entry dry-run result with regression flag), `ProviderResultCache` (system-level, no TenantId, cached back-translations by provider+version). 4 new enums: `CorpusTriggerType`, `CorpusRunStatus`, `CorpusVerdict`, `PipelineChangeStatus`. `ValidateSectionAsync` gains `persist = true` parameter — `persist=false` exercises identical pipeline logic but writes nothing to DB (used by corpus runs). `ICostEstimationService` / `CostEstimationService` with EUR rate table for Haiku, Sonnet, Gemini, DeepL. `CorpusRunJob` Hangfire job iterates entries, calls `ValidateSectionAsync(persist: false)`, detects regressions (`(int)actual > (int)expected`), auto-creates `TranslationDeviation` on Fail with corpus metadata in `RootCauseDetail`, sets `PipelineChangeRecord.Status = BlockedRegression` on linked change, `PendingApproval` on pass. `CorpusRunHub` SignalR at `/api/hubs/corpus-run` with group `corpus-{runId}`. Two-step run trigger: `PrepareRunAsync` (creates CorpusRun in Pending, estimates cost) → `EnqueueRunAsync` (actually queues Hangfire job). 10-minute cooldown between runs enforced in service layer. Cost threshold: ≥€3 `requiresConfirmation`, ≥€10 `requiresSuperUserApproval`. `PipelineChangeStatus` state machine: Draft → ReadyForReview (triggers auto corpus runs on all locked corpora) → PendingApproval → Approved; `BlockedRegression → Approved` requires justification + closes auto-deviation. Frontend: Corpus tab (6th tab, between Modules and Term Gate) with corpus list, freeze-from-talk dialog, corpus detail with entries table + lock, run history, run detail dialog with live SignalR progress + diff vs previous run. Changes tab enhanced with status badges + transition buttons (SuperUser only). `useCorpusRunHub` hook mirrors `useValidationHub` pattern. Migration: `AddCorpusEntities` (includes `Status` column on `PipelineChangeRecords` with default 'Draft').
8. **Translation Pipeline Audit Controls — Phase 4 (Corpus) — COMPLETE** — All four phases of pipeline audit controls are now implemented. `AuditCorpus` is TenantEntity; `AuditCorpusEntry` and `CorpusRunResult` are `BaseEntity` (scoped via parent). `ProviderResultCache` is system-level (no TenantId) — safe because corpus entries contain no PII. Cache key is `(CorpusEntryId, Provider, ProviderVersion)` — only re-calls providers that actually changed. `ValidateSectionAsync` now accepts `persist = false` for dry-run scoring (identical pipeline, no DB writes) — used by corpus runs. `CostEstimationService` has static EUR rate table per model ID; smoke test = 5 entries; Round 3 assumed 30% hit rate. Two-step trigger: estimate → confirm (>€3 threshold) → SuperUser approval (>€10). 10-minute cooldown enforced in service layer. Auto-deviation created on Fail verdict; linked `PipelineChangeRecord` moves to `BlockedRegression`. SuperUser override requires written justification which closes the auto-deviation. Draft → ReadyForReview auto-triggers corpus runs for all locked corpora matching the affected sector/language pairs. `TranslationDeviation.MetadataJson` stores `corpusRunId` for corpus-linked deviations (not `ValidationRunId` FK since that expects a validation run).
9. **QR Code Location Training — Phase 1 (PIN Infrastructure)** — Employee entity has 6 new PIN fields: `QrPin` (PBKDF2 hashed via `IPasswordHasher<Employee>`), `QrPinIsSet`, `QrPinGeneratedAt`, `QrPinLastUsedAt`, `QrPinFailedAttempts` (default 0), `QrPinLockedUntil` (null = not locked). Lockout triggers after 5 failed attempts, lasts 15 minutes. PIN is NEVER returned in any API response — emailed to employee only. PIN auto-generated on employee creation when `QrLocationTrainingEnabled` tenant setting is true. `GenerateEmployeePinsJob` is a one-off batch job (not recurring) enqueued exactly once when tenant first enables the feature — detects first-time enable by reading previous setting value before save in `TenantSettingsController`. Welcome email includes PIN section only when QR feature is enabled. PIN reset at `POST /api/employees/{id}/reset-pin` — admin or self, audited. `QrLocationTrainingEnabled` lives in `TenantSettingKeys.cs`. Frontend: new QR Training tab in settings, Reset Workstation PIN button on employee detail page. Phase 2 next: `QrLocation` + `QrCode` entities, admin CRUD, QR image generation via QRCoder.
10. **QR Code Location Training — COMPLETE** — Public `/qr/[codeToken]` page outside the `(authenticated)` route group — no JWT required. 6-digit PIN hashed via `IPasswordHasher<Employee>`, 5-attempt lockout with 15-minute window, `QrPinFailedAttempts` and `QrPinLockedUntil` on Employee entity. PIN NEVER returned in any API response — emailed only. `QrLocation` + `QrCode` entities (TenantEntity), `CodeToken = Guid.NewGuid("N")`, QR PNG generated via QRCoder and uploaded to R2 at `{tenantId}/qr-codes/{codeToken}.png` on code creation. Three content modes: `ViewOnly`, `Training`, `Induction`. `QrSession` entity (TenantEntity) tracks active/completed/abandoned sessions via `sessionToken` (Guid, no JWT). `QrScanController` has no `[Authorize]` — validates via `sessionToken` only. On completion: creates `ScheduledTalkCompletion` linked to the employee. `GenerateEmployeePinsJob` is one-off (not recurring) — enqueued exactly once when tenant first enables `QrLocationTrainingEnabled`, detected by reading previous setting value before save in `TenantSettingsController`. Welcome email includes PIN section only when QR feature enabled. Admin: `/admin/toolbox-talks/qr-locations` two-panel UI with sessions panel. Employee detail shows `QrTrainingHistorySection` when QR enabled. `GET /api/employees/{id}/training-history` combines `ScheduledTalks` and `QrSessions` sorted by date. `QrLocationTrainingEnabled` in `TenantSettingKeys.cs`.
11. **CORS configuration moved to environment variables** — CORS origins are now read from `Cors:AllowedOrigins` configuration array (Railway env vars: `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.) rather than hardcoded in `Program.cs`. Policy renamed from `"Development"` to `"CertifiedIQ"`. Each Railway environment has its own origins array. Development: `localhost:3000` + development Railway URL. Production: `certifiediq.ai` + `www.certifiediq.ai` + production Railway URL. Demo: demo Railway URL only.
12. **Production deployment** — Production Railway instance deployed from `main` branch on `QuantumBuildIE/quantumbuild-lms`. Development deploys from `transval`. Demo is disconnected. Deployment workflow: develop on `transval` → push to `company/transval` (auto-deploys to Development) → when ready: `git checkout main` → `git merge transval` → `git push company main` (auto-deploys to Production). Both `origin` and `company` remotes must be kept in sync. `AppSettings__BaseUrl` on Production web service points to `quantumbuild-lms-web-production.up.railway.app` until custom domain `certifiediq.ai` is configured.
13. **QrCode Course support** — `QrCode` entity supports both `ToolboxTalkId` (nullable) and `CourseId` (nullable) as mutually exclusive assignment options. Controller returns 400 if both are provided. `QrScanController` branches on course vs talk for session content delivery, merging sections across all course talks in order.
14. **Cross-tenant data leak fix (CRITICAL)** — `TenantEntity`-derived ToolboxTalks entities had `HasQueryFilter` configured with `!IsDeleted` only in their individual `EntityConfiguration` classes — no tenant predicate. Any query path missing an explicit `.Where(t => t.TenantId == ...)` clause returned rows from all tenants. Fix: moved all 12 `HasQueryFilter` calls to `ApplicationDbContext` (matching Core entity pattern) with `BypassTenantFilter || TenantId == currentTenantId` predicate; removed the soft-delete-only filters from the 12 entity configurations. `ExpiredSessionCleanupJob` and `CorpusRunJob` updated with `IgnoreQueryFilters()` on the queries that must cross tenant boundaries. No migration required — model-level change only.
15. **TransVal reviewer-edit workflow defects (A–D) fixed** — Four defects in the section reviewer-edit flow resolved: **(A)** `ValidateSectionAsync` no longer wipes `ReviewerDecision`/`EditedTranslation` on re-validation — those fields are only cleared on first-time entity creation (`Id == Guid.Empty`). **(B)** `LoadSectionsAsync` now loads existing reviewer edits into a dictionary and substitutes `EditedTranslation` for the published translation before scoring, so re-validation scores the corrected text. **(C)** `TranslationValidationJob.ExecuteAsync` gains an optional `sectionIndices` parameter — `EditSection` and `RetrySection` pass `[sectionIndex]` to re-validate a single section only; initial validate and wizard flow pass `null` for a full run. **(D)** `AcceptSection` in `TranslationValidationController` now calls `PropagateEditedTranslationAsync` when `EditedTranslation` is non-null — resolves `SectionIndex` to `SectionId` via `ToolboxTalkSections` ordered by `SectionNumber`, deserialises `TranslatedSections` JSON, replaces matching section `Content`, and flushes both mutations in a single atomic `SaveChangesAsync` — employees see the reviewer's corrected translation immediately on acceptance.
16. **AI Help Assistant (CertifiedIQ)** — In-app role-aware chat assistant at `/help`. Backend: `HelpChatController` at `POST /api/help/chat` — selects system prompt based on user role (SuperUser/Admin/Supervisor/Employee); Anthropic API called server-side so the API key is never exposed to the browser; role-scoped system prompts prevent cross-role information leakage; `IHttpClientFactory` registered for Anthropic API calls. Frontend: full-page chat interface (`features/help/components/HelpAssistant.tsx`) with left sidebar of role-aware topic shortcut buttons, multi-turn conversation history sent on every request, typing indicator, basic markdown rendering (bold, code, lists, headings), welcome message on load without API call. TopNav has a `HelpCircle` icon linking to `/help`. The Anthropic API key env var must be named correctly — a mismatch was found and corrected after initial deployment.
17. **Training Evidence Pack (formerly Inspection Report)** — `InspectionReportService` renamed PDF title, cover page, headings and filename from "Inspection Report" to "Training Evidence Pack". Disclaimer updated to explicitly scope the document as training/competence evidence and list what it does not cover. Sector-aware appendix added for food/FSAI sector listing 7 records EHOs will ask for that are outside CertifiedIQ's scope (HACCP file, temperature logs, cleaning records, pest control, allergen matrix, traceability, previous EHO report); non-food sectors skip the appendix. `R2StorageService` filename pattern updated to `training-evidence-pack`. Frontend: button label in `compliance/page.tsx` and dialog title/error toast in `GenerateReportDialog.tsx` updated; internal TypeScript identifiers unchanged.
18. **Two API response conventions — match the controller's existing pattern** — Core CRUD controllers (Employees, Users, Sites, Companies, Contacts, Tenants, Roles, SupervisorAssignments, ToolboxTalks most endpoints, Reports, Certificates, CourseAssignments, Schedules, LessonParser) return `Result<T>` envelopes: `{ success, data, errors }`. Frontend reads `response.data.data`. Newer controllers (Monitoring, PipelineAudit, RegulatoryIngestion, RequirementMapping, SafetyGlossary, TenantSettings, TenantSectors, TenantModules, QrLocation, TranslationValidation GetById endpoints) return DTOs directly. Frontend reads `response.data`. The tell-tale sign on the backend is `if (!result.Success) return BadRequest(result)` before `return Ok(result)` — that means `Result<T>` envelope. Before writing a new frontend API function, check what the corresponding controller action returns.

## Backlog

### Critical
- Issue 2: Course assignment deletion orphans completion records — DeleteCourseAssignmentCommandHandler soft-deletes completed ScheduledTalk rows when an in-progress course assignment is deleted, orphaning their ScheduledTalkCompletion records (guard only blocks deletion when the whole assignment is Completed). Causes silent under-counting in any report reading completions through ScheduledTalk: compliance, skills matrix, completion history. Cross-feature blast radius — scope and test deliberately

### High
- Bulk User import — allow admins to upload multiple users with employee records as background batch job with notification
- Bulk SOP import — allow admins to upload multiple SOPs as background batch job with notification
- Cost analysis of running the application, breakdown by number of employees, video sizes, PDF sizes, translations, number of languages, re-translations, validation etc.

- Demo deploy — push current code to the decoupled demo Railway instance. Blocked on Railway CLI account-scoping issue (company projects not visible from CLI). Requires: 14 missing env vars (TranslationValidation block + Cors__AllowedOrigins), demo database backup, then deploy. Re-test User Creation page afterward (expected to self-resolve as a staleness artifact)
- Employee edit-form role edge case — filtered role dropdown (Operator/Supervisor only) cannot display a legacy role an employee already holds; a save could silently drop it. Decide: leave as-is, or include the employee's current role as an option

### Medium
- Customer Usage Analytics — Phase 2 — scheduled/emailed version of the report (on-demand SuperUser page now built)
- CLAUDE.md — document API response conventions — record the two conventions (Result<T> envelope → frontend reads response.data.data; direct DTO → response.data) so the monitoring.ts-style bug does not recur
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
- R2 Orphan File Cleanup — Nightly Hangfire job
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

---

*Last Updated: May 18, 2026 (cross-tenant security fix, TransVal reviewer-edit defects A–D, AI Help Assistant, Training Evidence Pack, API response convention audit)*
*Architecture: Modular Monolith with Clean Architecture*
