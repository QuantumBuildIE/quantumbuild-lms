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

1. **Multi-tenancy is critical** — every query filters by TenantId
2. **Soft deletes everywhere** — set `IsDeleted = true`, never hard delete
3. **Audit fields are automatic** — SaveChanges override handles CreatedAt/UpdatedAt
4. **Permission-based auth** — use `[Authorize(Policy = "Permission.Name")]`
5. **CQRS in ToolboxTalks** — Commands for writes, Queries for reads
6. **SignalR for real-time** — Content generation, subtitle processing, and translation validation progress
7. **Follow established patterns** — check existing code before creating new
8. **Translation is JSON-based** — Sections and questions stored as JSON arrays in translation entities
9. **File deduplication** — SHA-256 hashes used to detect duplicate PDF/video uploads across talks
10. **Quiz randomization** — Questions can be shuffled, pooled, and option-randomized per attempt
11. **Role-based report scoping** — Reports auto-scope via `ResolveScopedEmployeeIdsAsync()`. Admin sees all, Supervisor sees assigned operators, Operator sees only self
12. **Restore-on-reassign pattern** — SupervisorAssignment uses soft-delete unassignment with restore instead of insert to avoid unique index violations. Also applied to: TenantSector, RegulatoryRequirementMapping, SafetyGlossaryTerm (glossary CSV import restores soft-deleted terms with matching EnglishTerm)
13. **Employee delete validation** — Must check for active supervisor assignments before allowing soft delete
14. **ICurrentUserService.EmployeeId** — Available from JWT `employee_id` claim; used for supervisor scoping and operator self-service
15. **ToolboxTalk Code field** — Auto-generated from title initials + numeric suffix; unique per tenant (`IX_ToolboxTalks_TenantId_Code`); propagated to all DTOs throughout the system
16. **Skills Matrix** — Employee × learning grid with 5 cell statuses; role-scoped; derives data from ScheduledTalks; Excel export via ClosedXML
17. **Translation Validation (TransVal)** — Multi-round back-translation consensus with up to 4 providers (Claude Haiku, DeepL, Gemini, DeepSeek); safety classification via glossary + regex patterns; configurable thresholds with safety-critical bump; reviewer accept/reject/edit workflow; audit PDF reports
18. **SafetyGlossary scoping** — System defaults (TenantId = null) vs tenant-specific overrides; sector-based (construction, mining, manufacturing, transport, food & hospitality, homecare, healthcare — any sector with regulatory translation requirements)
19. **TransVal uses direct services** — Not CQRS; uses ITranslationValidationService, ILexicalScoringService, IConsensusEngine, ISafetyClassificationService, IGlossaryTermVerificationService
20. **TransVal configuration** — `TranslationValidation` settings section: DeepL/Gemini/DeepSeek API keys, DefaultThreshold (75), SafetyCriticalBump (10), MaxRounds (3), SessionExpiryHours (24)
21. **Create Content wizard step order** — Input & Config → Parse → Quiz → Settings → Translate & Validate → Publish (Quiz and Settings come before TransVal)
22. **DeepL paid vs free** — Paid API base URL is `https://api.deepl.com/v2`; free tier is `https://api-free.deepl.com/v2`. Do NOT append `/translate` to the base URL
23. **CamelCaseJson for session data** — All `JsonSerializer.Deserialize` calls in `ContentCreationSessionService` must use camelCase `JsonSerializerOptions` for correct deserialization. This is a recurring bug — always check this when adding new deserialization calls
24. **Railway deployment** — `transval` branch deployed to Railway; keep `origin/transval` and `company/transval` in sync; use empty commits to force redeploy when auto-deploy doesn't trigger
25. **TranslationValidationResult upsert pattern** — `ValidateSectionAsync` uses find-or-create on `{ValidationRunId, SectionIndex}` instead of delete-then-insert. The job no longer pre-deletes existing results before re-validation. This prevents unique constraint violations from parallel workers racing on the same run
26. **DPA Acceptance Gate** — Every tenant must accept the DPA (v1.0) before accessing the application. Acceptance stored in `DpaAcceptances` table. `DpaConstants.CurrentDpaVersion` controls the active version — bumping this string forces all tenants to re-accept. SuperUsers bypass the gate. Enforced client-side in `auth-context.tsx` and `(authenticated)/layout.tsx`. No `middleware.ts` exists — all auth is client-side
27. **Create Content Wizard architecture** — Quiz step is skippable via `includeQuiz` toggle in Step 1. `SubtitleJobId` on `ContentCreationSession` links to background subtitle processing job. For Video + Course output: draft talk is repurposed as standalone "Full Video" learning (`OrderIndex 0`), section-based talks start at `OrderIndex 1`. Slideshow generation runs as a Hangfire background job at publish time via `ContentGenerationJob.GenerateSlideshowOnlyAsync`. `SubtitleProcessingOrchestrator.StartProcessingAsync` expects language names ("Spanish", "French") not codes ("es", "fr") — use `ILanguageCodeService.GetLanguageNameAsync()` to resolve before calling. `isStandaloneVideoTalk` detection in `TalkViewer.tsx`: VideoUrl set + exactly one section + placeholder content — Sections step hidden for these talks. **Sector selection in InputConfigStep** — three cases based on `useTenantSectors(user.tenantId)`: Case A (single sector) auto-selects and shows read-only info row; Case B (multiple sectors) shows required Select dropdown defaulting to `isDefault` sector; Case C (no sectors / fetch error) shows amber warning and optional fallback dropdown from `useAvailableSectors()`. `sectorKey` is part of `WizardState`, passed in `CreateSessionRequest`, and stored on `ContentCreationSession`. PublishStep resolves `session.sectorKey` to display name + icon via `useAvailableSectors()`. **Per-learning slideshow dispatch:** `PublishAsCourseAsync` loops through section learning IDs and enqueues `ContentGenerationJob.GenerateSlideshowOnlyAsync` per learning; Full Video Learning (`OrderIndex 0`) is excluded from slideshow generation for Video+Course output. **Title/description re-translation at publish time:** `IContentTranslationService` re-translates `TranslatedTitle` and `TranslatedDescription` when the title or description has changed at publish — old values captured before update for comparison, applies to all language records for the talk
28. **Code generation** — `GenerateCodeAsync` uses `IgnoreQueryFilters()` so soft-deleted talks' codes are included in availability checks. `SaveWithCodeRetryAsync` has 10 retry attempts with 50–200ms random jitter. `IX_ToolboxTalks_TenantId_Code` is a filtered unique index (`WHERE IsDeleted = false`)
29. **Public pages (no auth required)** — `/ai-system-card` and `/dpa-acceptance` are outside the `(authenticated)` route group. Both are linked from login page, set-password page, and top-nav user dropdown
30. **Sector entity** — `Sector.Key` is the canonical string value for sectors. `SafetyGlossary.SectorKey` and `TranslationValidationRun.SectorKey` remain as plain strings (not FKs) and must match `Sector.Key` values exactly. `TenantSector` is the junction entity linking tenants to their sectors
31. **Regulatory profile chain** — `RegulatoryBody` → `RegulatoryDocument` → `RegulatoryProfile` are system-managed (no TenantId, inherit from `BaseEntity`). `RegulatoryCriteria` supports tenant overrides (nullable TenantId, same pattern as `SafetyGlossary`). `RegulatoryProfile.CategoryWeightsJson` holds a JSON array of `{Key, Label, Weight}` scoring categories. `RegulatoryProfile.SectorKey` is a denormalised copy of `Sector.Key` maintained for quick lookup — must match `Sector.Key` values exactly. Seeded with Irish regulatory bodies (HIQA, HSA, FSAI, RSA) and sector-specific profiles with criteria
32. **Regulatory Score service** — `IRegulatoryScoreService` / `RegulatoryScoreService` scores validation runs via Claude Sonnet (`claude-sonnet-4-20250514`). Three scoring types (`ValidationScoreType` enum): `SourceDocument` (source quality against regulatory standard), `PureTranslation` (pure linguistic — fixed 5 categories: Accuracy, Fluency, Completeness, Consistency, Style), `RegulatoryTranslation` (translation against sector-specific regulatory criteria). `ValidationRegulatoryScore` entity (TenantEntity) persists scores with `RunNumber` sequential per `{ValidationRunId, ScoreType}`, `RunLabel` (Source Assessment / Linguistic Assessment / Pre-Remediation Baseline / Post-Remediation Pass N), `CategoryScoresJson`, `FullResponseJson` for audit. Critical prompt rule: RegulatoryTranslation scoring must NOT penalise the translation for faithfully reflecting weaknesses in the source document — only penalise translator-introduced problems. API: `POST /api/toolbox-talks/validation-runs/{runId}/regulatory-score` (Learnings.Admin), `GET .../history` (Learnings.View). Registered as HttpClient service in `ServiceCollectionExtensions`

33. **Sector & TenantSector API** — `GET /api/toolbox-talks/sectors` returns all active system-wide sectors, accessible to any authenticated user (used by TransVal wizard sector picker). TenantSector CRUD at `GET|POST|DELETE|PUT /api/tenants/{tenantId}/sectors[/{sectorId}[/set-default]]` — SuperUser only via `Tenant.Manage` policy (same as TenantsController and TenantModulesController). Follows **restore-on-reassign pattern** (CLAUDE.md note 12): soft-deleted TenantSector records are restored on re-assignment rather than inserting new rows, preventing unique index violations on `{TenantId, SectorId}`. `GetDefaultSectorAsync` logic: returns the sector marked `IsDefault = true`; if none marked, falls back to returning the single active sector; returns `null` if multiple exist with no default set (ambiguous). Delete validates at least one sector remains; auto-promotes lowest-DisplayOrder sector as default if the deleted sector was default
34. **Sector entity** — `Sector.Key` is the canonical string that ties to `SafetyGlossary.SectorKey` and `TranslationValidationRun.SectorKey`. These string fields are intentionally not converted to FKs — they must match `Sector.Key` values exactly. Current keys: `construction`, `homecare`, `manufacturing`, `transport`, `food_hospitality`, `healthcare`, `general`
35. **TenantSector** — Minimum one sector required per tenant. DELETE returns 400 if removing last sector. Restore-on-reassign pattern applies — soft-delete on remove, restore existing record on re-add. `IsDefault` auto-promotes to remaining sector on delete of default. `GetDefaultSectorAsync` logic: `IsDefault` first, single-sector fallback, `null` if multiple with no default (ambiguous)
36. **Regulatory profile chain** — System-managed: `RegulatoryBody` → `RegulatoryDocument` → `RegulatoryProfile` → `RegulatoryCriteria`. Only `RegulatoryCriteria` supports tenant overrides (nullable TenantId, SafetyGlossary pattern). `CategoryWeightsJson` on `RegulatoryProfile` is a JSON array — deserialise with camelCase `JsonSerializerOptions` (note 23 applies)
37. **Regulatory Score service** — Three scoring types via `IRegulatoryScoreService`. `RunLabel`: first regulatory run = "Pre-Remediation Baseline", subsequent = "Post-Remediation Pass {n}". Source and Pure runs always labelled "Source Assessment" and "Linguistic Assessment". Critical prompt rule: do not penalise translation for faithfully reflecting source document weaknesses — only penalise translator-introduced problems. All three scoring modes use `claude-sonnet-4-20250514`
38. **ValidationRun course association** — `TranslationValidationRun` has both `ToolboxTalkId` (nullable) and `CourseId` (nullable). For standalone talks: `ToolboxTalkId` set, `CourseId` null. For course output: wizard `PublishAsCourseAsync` reassociates runs from draft `ToolboxTalkId` to `CourseId` after course is persisted. `IsPartOfCourse` (bool, default false) on `ToolboxTalk` entity — set to true by `PublishAsCourseAsync` for section talks (OrderIndex 1+). Full Video talk at OrderIndex 0 remains `IsPartOfCourse = false`. Validation tab hidden on section talks via `talk.isPartOfCourse`. The `AddIsPartOfCourse` migration includes a SQL backfill (`migrationBuilder.Sql()`) that sets `IsPartOfCourse = true` for existing talks linked to a course at `OrderIndex >= 1`
39. **Wizard sector logic** — Three cases in InputConfigStep: (A) single TenantSector → auto-selected, read-only display, no dropdown; (B) multiple TenantSectors → dropdown showing tenant sectors only, `IsDefault` pre-selected; (C) no TenantSectors → amber warning, optional full sector list shown, does not block progression. `SectorKey` flows through `WizardState` → `CreateSessionRequest` → `ContentCreationSession` → `TranslationValidationRun`
40. **TenantSectorsController auth pattern** — Class-level `[Authorize]` (any authenticated user). GET endpoints have tenant ID guard: non-SuperUsers may only read their own tenant's sectors. Write endpoints (POST, DELETE, PUT) have method-level `[Authorize(Policy = "Tenant.Manage")]` — SuperUser only. Note: ASP.NET Core `[Authorize]` attributes are additive not overriding — method-level `[Authorize]` does NOT override class-level policy, both must pass. Always move the restrictive policy to the write endpoints individually
41. **RegulatoryRequirement** — System-managed (`BaseEntity`, no TenantId), seeded from HIQA homecare profile data. `IngestionStatus` gates visibility: only `Approved` requirements are shown to tenants. `IngestionSource` tracks whether the requirement was `Manual` or `Automated` (AI-ingested). FK to `RegulatoryProfile` (Restrict). Seeded after `RegulatoryProfileSeedData` in `Program.cs`
42. **RegulatoryRequirementMapping** — Tenant-scoped (`TenantEntity`), AI-suggested or manually created. `MappingStatus` drives compliance checklist status: `Suggested` (AI), `Confirmed` (reviewer), `Rejected`. `ConfidenceScore` (0-100) and `AiReasoning` populated when AI suggests mappings. Check constraint enforces exactly one of `ToolboxTalkId` or `CourseId` — never both, never neither. Tenant query filter applied in `ApplicationDbContext` alongside `SupervisorAssignment` and `TenantSector`
43. **RequirementIngestionSource/Status/MappingStatus enums** — Three enums in ToolboxTalks.Domain.Enums: `RequirementIngestionSource` (Manual, Automated), `RequirementIngestionStatus` (Draft, Approved, Rejected), `RequirementMappingStatus` (Suggested, Confirmed, Rejected). All stored as strings via `.HasConversion<string>()` in EF configurations
44. **Requirement Ingestion Pipeline** — AI-powered extraction of `RegulatoryRequirement` records from regulatory document URLs. Flow: SuperUser triggers ingestion via `POST /api/regulatory/documents/{id}/ingest` → `RequirementIngestionJob` (Hangfire, `content-generation` queue) fetches document text (PDF via `IPdfExtractionService.ExtractTextFromUrlAsync`, web pages via `HttpClient` + HTML stripping) → sends to Claude Sonnet for structured extraction → persists as `Draft` requirements → SuperUser reviews/edits/approves/rejects. `LastIngestedAt` (nullable `DateTimeOffset`) added to `RegulatoryDocument`. Multiple profiles per document: HSA covers both construction and manufacturing — drafts created for all active profiles. Duplicate check uses `IgnoreQueryFilters()` to include soft-deleted records. JSON parsing uses camelCase `JsonSerializerOptions` (note 23). Frontend at `/admin/regulatory` (SuperUser-only nav tab) with list page and detail/ingestion page with inline editing, approve/reject/approve-all actions, and 3-second polling during ingestion
45. **RegulatoryIngestionController** — SuperUser-only via class-level `[Authorize(Policy = "Tenant.Manage")]`. Routes: `GET /api/regulatory/documents`, `POST .../documents/{id}/ingest`, `GET .../documents/{id}/ingestion-status`, `GET .../documents/{id}/draft-requirements`, `PUT .../requirements/{id}/approve`, `PUT .../requirements/{id}/reject`, `PUT .../requirements/{id}`, `POST .../documents/{id}/approve-all`. No tenant filtering — system-wide records
46. **RequirementIngestionJob** — Hangfire background job (`[Queue("content-generation")]`, `[AutomaticRetry(Attempts = 1)]`). Registered as `AddScoped<RequirementIngestionJob>()` with `AddHttpClient<RequirementIngestionJob>()` for HTTP access. PDF extraction reuses existing `IPdfExtractionService` (PdfPig). Claude extraction uses Claude Sonnet via direct HTTP (same pattern as `RegulatoryScoreService`). Error handling: never throws from job — catches all exceptions, logs, and exits gracefully. Partial success: saves successfully parsed items as drafts, logs failed items. Retry on invalid JSON once with stricter prompt
47. **RequirementMappingJob** — Fire-and-forget Hangfire job (`[Queue("content-generation")]`, `[AutomaticRetry(Attempts = 1)]`). Triggered from both `PublishAsLessonAsync` and `PublishAsCourseAsync` in `ContentCreationSessionService` after content is published. Loads training content (talk sections or course with all talk sections), loads approved `RegulatoryRequirement` records matching the tenant's sector keys via `TenantSector`, sends to Claude Sonnet for AI mapping analysis. Creates `RegulatoryRequirementMapping` records with `MappingStatus = Suggested`. Follows restore-on-reassign pattern (note 12) — soft-deleted mappings are restored rather than duplicated. Rejected mappings are never overwritten by new AI suggestions. Never throws — catches all exceptions, logs, exits gracefully. Registered with `AddHttpClient<RequirementMappingJob>()` (5-min timeout)
48. **RequirementMappingService** — `IRequirementMappingService` / `RequirementMappingService` manages AI-suggested regulatory requirement mappings for tenant admins. All queries scoped to current tenant via `ICurrentUserService.TenantId`. Methods: `GetPendingMappingsAsync` (loads all mappings with summary counts, returns `Suggested` ordered by confidence descending), `ConfirmMappingAsync`, `RejectMappingAsync` (stores rejection notes in `AiReasoning`), `ConfirmAllSuggestedAsync`, `GetUnconfirmedCountAsync` (used by assignment flow warning). Registered as `AddScoped` in `ServiceCollectionExtensions`
49. **RequirementMappingController** — Tenant admin controller at `api/toolbox-talks/requirement-mappings`, class-level `[Authorize(Policy = "Learnings.Admin")]`. Routes: `GET .../pending` (summary + pending list), `PUT .../{id}/confirm`, `PUT .../{id}/reject`, `POST .../confirm-all`, `GET .../unconfirmed-count?toolboxTalkId=&courseId=`. All endpoints auto-scoped to current tenant
50. **Assignment warning for unconfirmed mappings** — Both `ScheduleDialog` and `AssignCourseDialog` show a dismissible amber warning banner when the talk/course has unreviewed `Suggested` mappings. Uses `useUnconfirmedMappingCount` hook calling `GET .../unconfirmed-count`. Warning text links to `/admin/toolbox-talks/pending-mappings`. Does not block assignment — advisory only
51. **Pending Mappings page** — Admin page at `/admin/toolbox-talks/pending-mappings`, gated by `Learnings.Admin` permission. Shows summary stat cards (Suggested/Confirmed/Rejected), "Confirm All" bulk action, and per-mapping cards with content link, requirement details, confidence badge (green ≥80, amber ≥60, red <60), AI reasoning callout, confirm/reject buttons. Nav tab "Mappings" added to admin toolbox-talks layout between Certificates and Settings
52. **Compliance Checklist (Feature C)** — Admin page at `/admin/toolbox-talks/compliance`, gated by `Learnings.Admin`. Shows regulatory requirement coverage across training content per sector. Coverage status logic: **Covered** = at least one `Confirmed` mapping with a `Pass` or `Review` validation run; **Pending** = `Suggested` mapping exists or `Confirmed` mapping without passing validation; **Gap** = no non-rejected mappings exist. Grouped by Principle with collapsible accordion sections. Multi-sector tenants see tabs per sector; single-sector tenants see flat layout. Includes coverage progress bar, principle/status filters, and "Generate Inspection Report" button (see note 56). Always-visible blue disclaimer banner: CertifiedIQ does not provide legal or regulatory advice
53. **Manual mapping** — `POST /api/toolbox-talks/requirement-mappings/manual` creates a mapping immediately as `Confirmed` with no AI involvement (`ConfidenceScore = null`, `AiReasoning = null`). Follows restore-on-reassign pattern (note 12). Used by AddMappingDialog on Gap requirements in the compliance checklist
54. **Content options endpoint** — `GET /api/toolbox-talks/requirement-mappings/content-options` returns flat list of published talks and active courses for manual mapping dropdown. Courses listed first, then talks
55. **Compliance checklist validation run resolution** — `GetComplianceChecklistAsync` determines the most recent validation run per mapped content by grouping completed `TranslationValidationRun` records (with `Pass` or `Review` outcome) by `ToolboxTalkId`/`CourseId` and selecting the one with the latest `CompletedAt` date
56. **Inspection Readiness Report (Feature D)** — `IInspectionReportService` / `InspectionReportService` generates a QuestPDF-based Inspection Readiness Report from compliance checklist data. Flow: reuses `GetComplianceChecklistAsync` for data → loads tenant name from `ICoreDbContext` → generates multi-page PDF (cover, executive summary, per-principle requirement detail, declaration + disclaimer) → uploads to R2 at `inspection-reports/{tenantId}/{sectorKey}/{timestamp}.pdf` → returns download URL. Requires `ResponsiblePersonName` and `ResponsiblePersonRole` — stored in PDF only, not persisted to DB. API: `POST /api/toolbox-talks/requirement-mappings/compliance/{sectorKey}/generate-report` (Learnings.Admin). Frontend: `GenerateReportDialog.tsx` wired to "Generate Inspection Report" button on compliance checklist page. No migration required — reports stored in R2 only
57. **RegulatoryRequirement canonical forms** — `PrincipleLabel` uses "&" not "and" (e.g., "Rights & Responsibilities" not "Rights and Responsibilities") — enforced in seed data and ingestion prompt. Section format for AI-ingested records uses "Standard X.Y" format (e.g., "Standard 3.1"); manually seeded records use "§N" format (e.g., "§3"). `IngestionStatus` gates visibility: only `Approved` requirements appear in compliance features and mapping jobs
58. **RegulatoryRequirementMapping field semantics** — `AiReasoning` is AI-only — populated by `RequirementMappingJob`, never overwritten by reviewer actions. `ReviewNotes` stores reviewer feedback on reject — cleared on restore-on-reassign. Rejected mappings are never overwritten by new AI suggestions (job skips them). Check constraint `ck_regulatory_requirement_mappings_talk_or_course` enforces exactly one of `ToolboxTalkId` or `CourseId` — never both, never neither
59. **Compliance coverage status logic** — **Covered** requires: at least one `Confirmed` mapping + at least one `TranslationValidationRun` with `OverallOutcome` of `Pass` or `Review` on the mapped content. **Pending** = `Suggested` mapping exists (not yet confirmed), OR `Confirmed` mapping exists but no passing validation run. **Gap** = no non-rejected mappings exist. Resolution uses most recent completed validation run per content item grouped by `ToolboxTalkId`/`CourseId`
60. **RequirementIngestionJob details** — Hangfire fire-and-forget (`[Queue("content-generation")]`, `[AutomaticRetry(Attempts = 1)]`). Flow: fetch URL (PDF via PdfPig `IPdfExtractionService` or HTML via `HttpClient` with HTML tag stripping) → Claude Sonnet extraction → persist as `Draft` → SuperUser review. Duplicate check uses `IgnoreQueryFilters()` to include soft-deleted records. JSON parsed with camelCase options (note 23). Retries Claude once with stricter prompt on invalid JSON. Multiple profiles per document supported (e.g., HSA covers construction + manufacturing — drafts created for all active profiles). `LastIngestedAt` updated on `RegulatoryDocument` after successful ingestion
61. **RequirementMappingJob trigger paths** — Fire-and-forget from 2 paths in `PublishAsLessonAsync` (direct publish and wizard publish) and 1 path in `PublishAsCourseAsync`. Loads tenant sectors via `TenantSector`, loads approved `RegulatoryRequirement` records matching those sector keys, sends content + requirements to Claude Sonnet for AI mapping analysis. Creates `RegulatoryRequirementMapping` records with `MappingStatus = Suggested`. Restore-on-reassign pattern applies (note 12). Rejected mappings never overwritten by new AI suggestions. Never throws — catches all exceptions, logs, exits gracefully
62. **Inspection Readiness Report R2 path** — Stored at `{tenantId}/inspection-reports/{sectorKey}/{yyyyMMdd-HHmmss}.pdf`. Footer truncates organisation name at 30 characters. Disclaimer text required on both the compliance checklist page (always-visible blue banner) and the generated PDF report (declaration section). `ResponsiblePersonName` and `ResponsiblePersonRole` stored in PDF only — not persisted to DB
63. **Compliance Checklist page** — `/admin/toolbox-talks/compliance`, gated by `Learnings.Admin`. Single sector: flat view with no tabs. Multi-sector: tab per sector. Disclaimer banner always visible — not dismissible. Manual mapping via `AddMappingDialog` creates `Confirmed` mapping immediately (no AI, no pending review). "Generate Inspection Report" button opens `GenerateReportDialog` requiring responsible person name and role. Grouped by Principle with collapsible accordion sections, coverage progress bar, principle/status filters
64. **Admin layout redirect** — `admin/layout.tsx` uses `useMemo`-derived `nonTenantScopedPaths` from nav items where `tenantScoped: false`. SuperUser redirect skips these paths. Adding future non-tenant-scoped pages to nav automatically excludes them from redirect — no code change needed
65. **TenantSectorsController auth pattern** — Class-level `[Authorize]` (any authenticated user). GET endpoints have tenant ID guard: non-SuperUsers may only read their own tenant's sectors (`ICurrentUserService.TenantId` must match route `tenantId`). Write endpoints have method-level `[Authorize(Policy = "Tenant.Manage")]`. Critical: ASP.NET Core `[Authorize]` attributes are additive — method-level does NOT override class-level, both must pass. Always move restrictive policy to write endpoints individually
66. **Stream disposal in R2 uploads** — When passing a `MemoryStream` to `PutObjectAsync` in an async HTTP request context, read `stream.Length` BEFORE the upload call. The S3 SDK consumes the stream during upload — accessing any stream property afterwards throws `ObjectDisposedException`. Do not use `using var stream` — manage disposal explicitly with `await stream.DisposeAsync()` after upload completes
67. **Validation run course association** — `TranslationValidationRun` has both `ToolboxTalkId` (nullable) and `CourseId` (nullable). Standalone talks: `ToolboxTalkId` set, `CourseId` null. Course output: `PublishAsCourseAsync` reassociates runs from draft `ToolboxTalkId` to `CourseId` after course is persisted. `IsPartOfCourse` (bool, default false) on `ToolboxTalk` entity — set to true by `PublishAsCourseAsync` for section talks (OrderIndex 1+). Full Video talk at OrderIndex 0 remains `IsPartOfCourse = false`. Validation tab hidden on section talks via `talk.isPartOfCourse`
68. **Publish flow translation remapping** — `ExtractTranslatedSectionForId` and `ExtractTranslatedQuestionForId` helpers remap translated content JSON by positional index matching when draft sections/questions are soft-deleted and recreated with new GUIDs at publish time. Applied in both `PublishAsLessonAsync` and `PublishAsCourseAsync`. Old IDs captured before delete, new IDs captured after recreation, mapped positionally (index 0 → index 0). `IContentTranslationService` re-translates `TranslatedTitle` and `TranslatedDescription` when title/description changed at publish time — old value captured before update for comparison. Applies to all language records for the talk
69. **Tiered translation prompt system** — `TranslationPrompts.cs` implements 5 tiers: Tier 1 base rules (register, terminology, fluency — always applied), Tier 2 sector-specific (homecare/healthcare=HIQA, construction/manufacturing=HSA, food=FSAI, transport=RSA), Tier 3 safety-critical boost (absolute precision required), Tier 4 glossary injection (approved translations injected as mandatory), Tier 5 language-specific rules (Polish mandatory language rules, Romanian formal address, Ukrainian Cyrillic/formal, German noun capitalisation). `BuildGenericTranslationPrompt` for subtitles and non-compliance content — Tier 1 only. `GlossaryTermInstruction` record (`EnglishTerm`, `ApprovedTranslation`) structures term data for prompt injection
70. **TranslationValidationJob glossary pre-loading** — `LoadGlossaryTermsAsync` private method loads glossary terms for the run's sector and tenant before the section loop — once per run, not per section. Prefers tenant override over system default. Parses Translations JSON (camelCase, note 23) to extract approved translation for target language. `ISafetyClassificationService` injected into `TranslationValidationJob` — `ClassifyAsync` called per section before translation to determine `isSafetyCritical`. `SectorKey` flows from `GenerateContentTranslationsCommand` through all dispatch sites — `ITenantSectorService.GetDefaultSectorAsync` used in `MissingTranslationsJob`, `ContentGenerationJob`, `ToolboxTalksController` for sector resolution
71. **MissingTranslationsJob completeness check** — Job checks field completeness within existing `ToolboxTalkTranslation` records, not just record existence. Re-triggers `GenerateContentTranslationsCommand` if: `TranslatedTitle` null/empty, `TranslatedSections` null/empty/[], `TranslatedQuestions` null/empty/[] when `RequiresQuiz=true`, no `ToolboxTalkSlideshowTranslation` exists when `SlidesGenerated=true`. Slideshow check is a separate query — independent of main translation record check. `ContentGenerationJob.GenerateSlideshowOnlyAsync` enqueues `MissingTranslationsJob` immediately after saving slideshow HTML — closes timing gap for translated slideshow delivery
72. **Course slideshow dispatch** — `PublishAsCourseAsync` generates slideshows per section Learning. `minOrderIndex = 1` for Video+Course (Full Video Learning at OrderIndex 0 excluded — video content covers everything). `minOrderIndex = 0` for PDF/Text+Course (all section Learnings get slideshows). All gated behind `GenerateSlideshow` session setting. Section talk IDs queried from `ToolboxTalkCourseItems` by `OutputCourseId` after course creation. `ITenantSectorService` injected into `ContentGenerationJob` for sector resolution
73. **Healthcare and General sectors** — `healthcare` sector added to `SectorSeedData` (DisplayOrder 6, hospital icon), `RegulatoryProfileSeedData` (HIQA regulatory profile), `SafetyGlossarySeedData` (15 terms). `general` fallback glossary added with 16 universal safety terms (emergency, prohibition, hazard, procedure, regulatory categories) — used by `TranslationValidationService` when sectorKey is null via `sectorKey ?? "general"`. `general` SectorKey is NOT in the Sector table — glossary-only concept. `SafetyGlossarySeedData` refactored to per-sector idempotency — checks each SectorKey individually before inserting rather than all-or-nothing check
74. **Glossary CSV bulk import** — `POST /api/toolbox-talks/glossary/sectors/{id}/terms/import` — accepts multipart CSV, permission `Learnings.Admin`, rejects system defaults (403). CSV format: `english_term,category,is_critical,fr,pl,ro,uk,pt,es,lt,de,lv`. Manual line-by-line parsing — no CSV library. Restore-on-reassign pattern: soft-deleted terms with matching `EnglishTerm` are restored and updated rather than causing unique constraint violation. Active term = skip. Download template generates client-side. `refetchQueries` (not `invalidateQueries`) used after import — forces immediate network request regardless of `staleTime=60_000` on global query client. `invalidateQueries` only marks stale and respects staleTime check
75. **Employee portal auth guards** — Admin users without employee records (`user.employeeId == null`) are redirected from `/toolbox-talks` to `/admin/toolbox-talks` via separate `useEffect` in layout — does not affect SuperUser redirect. `top-nav.tsx` logo and "My Learnings" links route to `/toolbox-talks` when `employeeId` present, `/admin/toolbox-talks` when absent. `MyToolboxTalksController` listing endpoints (`GetMyTalks`, `GetPending`, `GetInProgress`, `GetOverdue`, `GetCompleted`, `GetMyCourses`) return 200 with empty array instead of 400 when no employee record — `GetSummary` was already correct. `PendingTrainingBanner` skips `GET /api/my/toolbox-talks/summary` call entirely when no `employeeId` via TanStack Query `enabled` flag
76. **Compliance page uncategorised sentinel** — Null or empty `Principle` values on `RegulatoryRequirement` records produce empty string `Select.Item` values which crash Radix UI. Fix: filter null/empty principles before building Select options, use `__uncategorised__` sentinel string as value for the uncategorised group in both `SelectItem` and `AccordionItem`. Filter logic matches `__uncategorised__` against groups with falsy principle. `RequirementIngestionJob` prompt updated to mandate section, sectionLabel, principle, principleLabel — infer from context if not explicit in source document
77. **Glossary UI enhancements** — Structured per-language translation editor replaces raw JSON textarea in both edit and add term forms. `TranslationFields` component renders 3-col/2-col/1-col grid of labelled inputs (flag + language name). `parseTranslationsToRecord` and `serialiseTranslations` helpers handle JSON conversion internally — API types remain JSON string. "Override exists" muted indicator replaces Create Override button when `sectors.some(s => !s.isSystemDefault && s.sectorKey === sector.sectorKey)`. Sector detail lookup uses `GET /glossary/sectors/by-id/{id}` not `GET /glossary/sectors/{sectorKey}` — fixes PostgreSQL NULL ordering bug where `OrderByDescending(g => g.TenantId)` returns system default first (NULLs sort first in DESC). Fixed with `OrderByDescending(g => g.TenantId.HasValue)`
78. **Preview modal video player** — "Preview as Employee" modal (`PreviewModal.tsx`) includes `VideoPlayer` component above slideshow when `talk.videoUrl` is present. Anti-skip tracking disabled: `minimumWatchPercent={0}`, `currentWatchPercent={0}`, `onProgressUpdate` is a no-op callback. Subtitles loaded via admin subtitle endpoint (`/api/toolbox-talks/{id}/subtitles/{languageCode}`) — requires `ToolboxTalks.View` which admins already have. `previewLanguage` passed as `preferredLanguageCode` to `VideoPlayer` — subtitle track auto-switches when language selector changes
79. **Wizard output type default** — Content creation wizard output type always defaults to Learning (`OutputType.Lesson`) regardless of parsed section count. Previously defaulted to Course when 3+ sections were parsed. Changed in both `ContentParserService.cs` and `ParseStep.tsx` (3 locations). User can still manually switch to Course. AI-assisted Learning vs Course suggestion is parked for future implementation

---

*Last Updated: March 29, 2026*
*Architecture: Modular Monolith with Clean Architecture*
