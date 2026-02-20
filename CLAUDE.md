# QuantumBuild LMS

A multi-tenant Learning Management System for workplace safety training and compliance, built with a modular monolith architecture.

---

## Project Overview

### Business Context
- **Primary Use:** Toolbox Talks — video-based safety training with quizzes, certificates, and compliance tracking
- **Scale:** Multi-language support, AI-generated content, subtitle processing, course management
- **Key Workflows:** Talk creation (manual + AI-generated), scheduling & assignment, employee completion with signature, quiz assessment, certificate generation, refresher scheduling

### Currently Implemented
- **Toolbox Talks Module** — Full training lifecycle: content creation, AI generation, scheduling, assignment, completion, certificates, courses, reports
- **Admin Module (Core)** — Sites, Employees, Companies, Users management
- **Supervisor-Operator Assignments** — Supervisors manage their team of operators; reports auto-scoped by role
- **Authentication & Authorization** — JWT with permission-based policies, role-based report scoping
- **Dashboard** — Module selector
- **Subtitle Processing** — Video transcription (ElevenLabs) + translation (Claude API) to SRT files
- **Content Translation** — AI-powered translation of sections, quizzes, slideshows, email templates
- **Certificate Generation** — PDF certificates for talk and course completions
- **Background Jobs** — Hangfire for scheduling, reminders, overdue tracking, content generation

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
| QuestPDF | Latest | Certificate PDF generation |
| ClosedXML | 0.105.0 | Excel export (Skills Matrix) |
| Cloudflare R2 | — | File storage (videos, PDFs, subtitles, certificates) |

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
│   │       │   ├── Entities/                    # 22 entities
│   │       │   └── Enums/                       # 13 enums
│   │       ├── QuantumBuild.Modules.ToolboxTalks.Application/
│   │       │   ├── Abstractions/                # Pdf, Storage, Subtitles, Translations interfaces
│   │       │   ├── Commands/                    # CQRS commands (17 command handlers)
│   │       │   ├── Queries/                     # CQRS queries (12 query handlers)
│   │       │   ├── Features/                    # Certificates, CourseAssignments, Courses
│   │       │   ├── DTOs/                        # Data transfer objects
│   │       │   ├── Services/                    # Business logic services
│   │       │   └── Common/Interfaces/           # IToolboxTalksDbContext
│   │       └── QuantumBuild.Modules.ToolboxTalks.Infrastructure/
│   │           ├── Configuration/               # R2StorageSettings, SubtitleProcessingSettings
│   │           ├── Hubs/                        # SignalR hubs (ContentGeneration, SubtitleProcessing)
│   │           ├── Jobs/                        # Hangfire background jobs (6 jobs)
│   │           ├── Persistence/                 # DbContext, Entity Configurations, Seed Data
│   │           └── Services/                    # AI, Pdf, Storage, Subtitles, Translations
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
| `/admin/toolbox-talks/talks/new` | Create talk (multi-step wizard) |
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
| `/admin/toolbox-talks/settings` | Module settings |

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

---

## Toolbox Talks Module Entities (22 Total)

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

### Enums (13 Total)
- **CertificateType** — Talk, Course
- **ContentSource** — Manual, Video, Pdf, Both
- **CourseAssignmentStatus** — Assigned, InProgress, Completed, Overdue
- **QuestionType** — MultipleChoice, TrueFalse, ShortAnswer
- **ScheduledTalkStatus** — Pending, InProgress, Completed, Overdue, Cancelled
- **SubtitleProcessingStatus** — Pending, Transcribing, Translating, Uploading, Completed, Failed, Cancelled
- **SubtitleTranslationStatus** — Pending, InProgress, Completed, Failed
- **SubtitleVideoSourceType** — GoogleDrive, AzureBlob, DirectUrl
- **ToolboxTalkFrequency** — Once, Weekly, Monthly, Annually
- **ToolboxTalkScheduleStatus** — Draft, Active, Completed, Cancelled
- **ToolboxTalkStatus** — Draft, Processing, ReadyForReview, Published
- **VideoSource** — None, YouTube, GoogleDrive, Vimeo, DirectUrl
- **VideoTranslationStatus** — Pending, Processing, Completed, Failed, ManualRequired

---

## Background Jobs (Hangfire)

| Job | Schedule | Description |
|-----|----------|-------------|
| ContentGenerationJob | On-demand | AI content generation from video/PDF |
| MissingTranslationsJob | Periodic | Find and generate missing translations |
| ProcessToolboxTalkSchedulesJob | Daily | Process active schedules to create assignments |
| SendRefresherRemindersJob | Daily | Send reminders for upcoming refresher due dates |
| SendToolboxTalkRemindersJob | Daily | Send reminders for overdue/pending talks |
| UpdateOverdueToolboxTalksJob | Daily | Mark past-due assignments as Overdue |

---

## Cloudflare R2 Storage

Used for storing videos, PDFs, subtitle files, and certificate PDFs.

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

## Notes for Claude Code

1. **Multi-tenancy is critical** — every query filters by TenantId
2. **Soft deletes everywhere** — set `IsDeleted = true`, never hard delete
3. **Audit fields are automatic** — SaveChanges override handles CreatedAt/UpdatedAt
4. **Permission-based auth** — use `[Authorize(Policy = "Permission.Name")]`
5. **CQRS in ToolboxTalks** — Commands for writes, Queries for reads
6. **SignalR for real-time** — Content generation and subtitle processing progress
7. **Follow established patterns** — check existing code before creating new
8. **Translation is JSON-based** — Sections and questions stored as JSON arrays in translation entities
9. **File deduplication** — SHA-256 hashes used to detect duplicate PDF/video uploads across talks
10. **Quiz randomization** — Questions can be shuffled, pooled, and option-randomized per attempt
11. **Role-based report scoping** — Reports auto-scope via `ResolveScopedEmployeeIdsAsync()`. Admin sees all, Supervisor sees assigned operators, Operator sees only self
12. **Restore-on-reassign pattern** — SupervisorAssignment uses soft-delete unassignment with restore instead of insert to avoid unique index violations
13. **Employee delete validation** — Must check for active supervisor assignments before allowing soft delete
14. **ICurrentUserService.EmployeeId** — Available from JWT `employee_id` claim; used for supervisor scoping and operator self-service
15. **ToolboxTalk Code field** — Auto-generated from title initials + numeric suffix; unique per tenant (`IX_ToolboxTalks_TenantId_Code`); propagated to all DTOs throughout the system
16. **Skills Matrix** — Employee × learning grid with 5 cell statuses; role-scoped; derives data from ScheduledTalks; Excel export via ClosedXML

---

*Last Updated: February 20, 2026*
*Architecture: Modular Monolith with Clean Architecture*
