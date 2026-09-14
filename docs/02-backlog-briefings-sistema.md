# Technical Backlog — Briefings Management System

**Briefings Management System for the Design Sector**
Based on `docs/requirements-doc.pdf`, requirements **RF004–RF063** (all modules except Users and Access).

> This backlog covers **modules 2 to 14** of the requirements document: Briefings, Briefing Approval, Tasks,
> Workflow, SLA, Notifications, History and Auditing, Briefing Categories, Delivery Approval,
> Versioning, Corporate Integration, Task and Approval Control, and Intelligent Structuring of Briefings.
> Module 1 (Users and Access — RF001–RF003) has its own backlog in `docs/backlog-usuarios-acesso.md` and is a
> **prerequisite** for everything here: every use case below depends on authentication/authorization already
> implemented (`ICurrentUser`, JWT, per-permission policies).

---

## Conventions and assumptions

- **Stack:** .NET 10, Blazor, EF Core, PostgreSQL, Clean Architecture, DDD, Repository + Unit of Work, CQRS
  (where it makes sense), FluentValidation, auditing and SOLID — same assumptions as `docs/backlog-usuarios-acesso.md`.
- **Layers / projects:** `Domain`, `Application`, `Infrastructure`, `Api`/`Web` — see `CLAUDE.md` for the
  dependency rules between layers.
- **Identifiers:** `Guid` for all primary keys.
- **Auditing:** `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on every auditable entity (reuses the
  infrastructure from USR-002/USR-003).
- **Soft delete:** applied where it makes business sense (categories, checklists); briefings/tasks use status,
  not soft delete, to preserve history.
- **File storage:** abstracted via `IFileStorageService` (Application), implemented in
  `Infrastructure` (e.g. local filesystem in dev, blob storage in production — an infra decision outside the
  scope of this backlog).
- **Estimates:** `Low` (≈ up to 0.5 day), `Medium` (≈ 1–2 days), `High` (≈ 3+ days / high complexity/risk).
- **Task naming:** `BRF-XXX`.
- **Traceability:** each task references the RF(s) from `requirements-doc.pdf` that it implements.

### Suggested execution order (waves)

1. **Prerequisite:** module 1 complete (`docs/backlog-usuarios-acesso.md`, USR-001 to USR-054).
2. **Briefing domain core:** Categories (BRF-059–064) before Briefings (BRF-001–014), since every
   briefing requires a category (RF040).
3. **Briefing lifecycle:** Briefings (BRF-001–014) → Briefing Approval (BRF-015–020) → Tasks
   (BRF-021–034).
4. **Orchestration:** Workflow (BRF-035–039) → Task and Approval Control (BRF-083–086) →
   Delivery Approval (BRF-065–072) → Versioning (BRF-073–078).
5. **Operational support:** SLA (BRF-040–046) → Notifications (BRF-047–054) → History and Auditing
   (BRF-055–058).
6. **Extensions:** Intelligent Structuring (BRF-087–094) → Corporate Integration (BRF-079–082,
   parallelizable at any point after module 1).
7. **Quality:** Tests (BRF-095–098), continuous each wave, consolidated at the end.

---

# 2. Briefings Module

## BRF-001 — Data modeling: Briefing and Attachment

**Title:** Define the relational model for `Briefings` and `Attachments` (ERD + data dictionary).

**Goal:** Produce the logical design of the tables supporting RF004–RF010, with columns, types,
relationships, indexes and constraints.

**Rules:**
- Every briefing belongs to a requester (`UserId`) and a category (`CategoryId`, RF040 — depends on
  BRF-059).
- A briefing has zero or more attachments (`Attachments`), 1:N.
- Briefing status: `Draft`, `AwaitingApproval`, `ChangesRequested`, `Approved`, `Rejected`,
  `InProgress`, `Completed`, `Cancelled`.

**Technical details:**
- `Briefings` table: `Id`, `Title`, `Description`, `Objective`, `RequestingArea`, `DesiredDeliveryDate`,
  `Priority` (enum), `CategoryId` (FK), `RequesterId` (FK), `Status` (enum), audit columns.
- `Attachments` table: `Id`, `BriefingId` (FK), `FileName`, `StoragePath`, `SizeBytes`, `UploadedById`,
  `UploadedAt`.
- Indexes: `IX_Briefings_RequesterId`, `IX_Briefings_Status`, `IX_Briefings_CategoryId`.

**Dependencies:** USR-001 (modeling standard), BRF-059 (Category must exist for the FK).

**Acceptance criteria:**
- **Given** requirements RF004–RF010, **When** the model is reviewed, **Then** tables, FKs and indexes cover all
  the mandatory fields of RF005.

**Estimate:** Medium.

---

## BRF-002 — EF Core: BriefingConfiguration, AttachmentConfiguration and migration

**Title:** Materialize the BRF-001 model via EF Core code-first.

**Goal:** Configure the Fluent API mapping, constraints and generate the corresponding migration.

**Rules:**
- `Status` and `Priority` enums mapped as `string` (or `smallint` with conversion) — not implicit `int`, for
  readability in direct database queries.

**Technical details:**
- `BriefingConfiguration : IEntityTypeConfiguration<Briefing>`, `AttachmentConfiguration` analogous.
- `dotnet ef migrations add BriefingsAttachments -p Infrastructure -s Web`.

**Dependencies:** BRF-001, BRF-003, BRF-004, USR-002 (DbContext already exists).

**Acceptance criteria:**
- **Given** the migration, **When** applied, **Then** the `Briefings` and `Attachments` tables are created with
  all the planned indexes and FKs.

**Estimate:** Low.

---

## BRF-003 — Briefing entity

**Title:** Implement the `Briefing` entity with state invariants.

**Goal:** Encapsulate the RF004–RF010 rules inside the entity (private setters, transition methods).

**Rules:**
- `Create(...)` always starts in `Draft`.
- `Edit(...)` is only allowed in `Draft` or `AwaitingApproval` (RF009) — throws `DomainException` otherwise.
- `Cancel()` is only allowed before execution starts (`Status != InProgress/Completed`) (RF010).
- `SubmitForApproval()` requires all mandatory RF005 fields to be filled.

**Technical details:**
- `Priority` VO (enum `Low/Medium/High/Urgent` — to validate with stakeholders).
- Methods: `Create`, `SaveDraft`, `Edit`, `SubmitForApproval`, `Cancel`.

**Dependencies:** None (pure domain).

**Acceptance criteria:**
- **Given** a briefing in `Approved`, **When** `Cancel()` is called after execution has started, **Then** it
  throws `DomainException`.
- **Given** a briefing in `Draft` with missing mandatory fields, **When** `SubmitForApproval()` is called,
  **Then** it returns a validation failure.

**Estimate:** Medium.

---

## BRF-004 — Attachment entity

**Title:** Implement the `Attachment` entity, linked to `Briefing`.

**Goal:** Record metadata for files attached to a briefing (RF005 — "Attachments").

**Rules:**
- An attachment is immutable once created (a new file version produces a new `Attachment`, not an edit).

**Technical details:**
- Properties: `FileName`, `StoragePath`, `SizeBytes`, `UploadedById`, `UploadedAt`.

**Dependencies:** None.

**Acceptance criteria:**
- **Given** a created attachment, **When** `StoragePath` is attempted to change, **Then** there is no public
  setter available (verified by a reflection test or a public-API review).

**Estimate:** Low.

---

## BRF-005 — BriefingRepository

**Title:** Implement `IBriefingRepository` and its EF Core implementation.

**Goal:** Provide access to `Briefing` with support for filtering by requester and status.

**Rules:**
- Interface in `Domain`, implementation in `Infrastructure` — the project's Repository pattern.

**Technical details:**
- Methods: `GetByIdAsync`, `GetByRequesterAsync(userId, filters, pagination)`, `AddAsync`.
- Uses the existing `IUnitOfWork` (USR-010) for `SaveChanges`.

**Dependencies:** BRF-003, USR-010.

**Acceptance criteria:**
- **Given** a requester with 3 briefings, **When** `GetByRequesterAsync` is called, **Then** it returns only that
  user's briefings, paginated.

**Estimate:** Low.

---

## BRF-006 — Use case: Create/save briefing as draft

**Title:** Implement `CreateBriefingCommand` (RF004, RF005, RF006).

**Goal:** Allow the requester to register a briefing and save it as a draft before submission.

**Rules:**
- Validation of the mandatory RF005 fields is about **presence**, not completeness for approval (that is
  BRF-007/BRF-093).
- `CategoryId` must exist and be active (RF040).

**Technical details:**
- `CreateBriefingCommand` + `CreateBriefingCommandHandler` + `CreateBriefingCommandValidator` (FluentValidation).
- Output DTO: `BriefingDto` (never expose the entity).

**Dependencies:** BRF-003, BRF-005, BRF-059 (category).

**Acceptance criteria:**
- **Given** a valid command, **When** executed, **Then** a briefing in `Draft` is persisted and returned.
- **Given** a non-existent `CategoryId`, **When** executed, **Then** it returns a `CategoryNotFound` failure.

**Estimate:** Medium.

---

## BRF-007 — Use case: Submit briefing for approval

**Title:** Implement `SubmitBriefingForApprovalCommand` (RF007).

**Goal:** Transition the briefing from `Draft`/`ChangesRequested` to `AwaitingApproval`.

**Rules:**
- Blocks submission if the category's mandatory checklist is pending (RF063 — depends on BRF-093; if
  BRF-093 is not yet implemented, checklist validation is deferred, but validation of the mandatory RF005
  fields already applies).
- Raises the domain event `BriefingSubmittedForApproval` (consumed later by Notifications).

**Technical details:**
- The handler calls `Briefing.SubmitForApproval()` and, if `IChecklistValidator` (BRF-093) is registered,
  validates the checklist before persisting the transition.

**Dependencies:** BRF-003, BRF-006. Full checklist integration depends on BRF-093.

**Acceptance criteria:**
- **Given** a briefing in `Draft` with all mandatory fields, **When** submitted, **Then** the status changes
  to `AwaitingApproval`.
- **Given** a briefing with a pending checklist (once BRF-093 exists), **When** submitted, **Then** it returns
  a `ChecklistPending` failure.

**Estimate:** Medium.

---

## BRF-008 — Use case: Query the requester's briefings

**Title:** Implement `GetBriefingsByRequesterQuery` (RF008).

**Goal:** Allow the requester to view their own briefings, with basic filters.

**Rules:**
- Only returns briefings of the authenticated user (`ICurrentUser.UserId`), except for profiles with a
  broad-view permission (Manager/Admin — reuses the module 1 policies).

**Technical details:**
- Paginated query with optional filter by `Status` and `CategoryId`.

**Dependencies:** BRF-005.

**Acceptance criteria:**
- **Given** an authenticated requester, **When** querying their briefings, **Then** they receive only their own,
  paginated.

**Estimate:** Low.

---

## BRF-009 — Use case: Edit briefing

**Title:** Implement `EditBriefingCommand` (RF009).

**Goal:** Allow editing a briefing while in `Draft` or `AwaitingApproval`.

**Rules:**
- Only the requester who owns the briefing can edit it (or Admin).
- Reuses `Briefing.Edit(...)` (BRF-003) for the state invariant.

**Technical details:**
- The handler loads the entity, validates ownership, calls `Edit`, persists.

**Dependencies:** BRF-003, BRF-005.

**Acceptance criteria:**
- **Given** a briefing in `Approved`, **When** an edit is attempted, **Then** it returns an `EditNotAllowed`
  failure.

**Estimate:** Low.

---

## BRF-010 — Use case: Cancel briefing

**Title:** Implement `CancelBriefingCommand` (RF010).

**Goal:** Allow the requester to cancel a briefing before execution starts.

**Rules:**
- Reuses `Briefing.Cancel()` (BRF-003).
- Raises the `BriefingCancelled` event (for future notifications, if applicable).

**Technical details:**
- Simple handler: load, validate ownership, call `Cancel`, persist.

**Dependencies:** BRF-003, BRF-005.

**Acceptance criteria:**
- **Given** an `InProgress` briefing, **When** cancellation is attempted, **Then** it returns a
  `CancellationNotAllowed` failure.

**Estimate:** Low.

---

## BRF-011 — Attachment upload infrastructure

**Title:** Implement `IFileStorageService` and its storage implementation.

**Goal:** Provide reusable file upload/download for Briefings (RF005), Tasks (RF022) and
Versioning (RF049–052).

**Rules:**
- Interface in `Application`, implementation in `Infrastructure` — no coupling to a specific provider in the
  inner layers.
- Configurable file size/type validation.

**Technical details:**
- `IFileStorageService.SaveAsync(stream, fileName) : path`, `GetAsync(path) : stream`.
- Initial implementation: local filesystem (dev); interface ready to swap for blob storage without touching
  Application.

**Dependencies:** None.

**Acceptance criteria:**
- **Given** a file stream, **When** `SaveAsync` is called, **Then** it returns a persisted path retrievable via
  `GetAsync`.

**Estimate:** Medium.

---

## BRF-012 — REST endpoints: BriefingsController

**Title:** Expose the Briefings use cases via API.

**Goal:** CRUD + briefing state transitions over HTTP, protected by the module 1 policies.

**Technical details:**
- `POST /briefings`, `PUT /briefings/{id}`, `POST /briefings/{id}/submit`, `POST /briefings/{id}/cancel`,
  `GET /briefings` (paginated).

**Dependencies:** BRF-006 to BRF-010.

**Acceptance criteria:**
- **Given** a user without permission, **When** calling any endpoint, **Then** they receive `403`.

**Estimate:** Low.

---

## BRF-013 — Blazor: briefing create/edit form

**Title:** Briefing registration/edit screen (draft and submission).

**Goal:** UI for RF004–RF007, RF009.

**Technical details:**
- Thin component, calls the use cases via an application service (no business logic in the `.razor`).
- Attachment upload integrated with BRF-011.

**Dependencies:** BRF-012.

**Acceptance criteria:**
- **Given** unfilled mandatory fields, **When** the user tries to submit, **Then** the UI shows validation
  before calling the API.

**Estimate:** Medium.

---

## BRF-014 — Blazor: requester's briefing list

**Title:** Screen to query the requester's briefings (RF008).

**Technical details:**
- Paginated table with filter by status/category; edit/cancel actions depending on status.

**Dependencies:** BRF-012.

**Acceptance criteria:**
- **Given** a briefing in `Approved`, **When** listed, **Then** the edit/cancel actions appear disabled.

**Estimate:** Low.

---

# 3. Briefing Approval Module

## BRF-015 — Domain: approval transitions + Justification VO

**Title:** Extend `Briefing` with `Approve`, `Reject`, `RequestChanges` methods (RF011–RF014).

**Rules:**
- `Reject` and `RequestChanges` require a non-empty `Justification` (RF013).
- Valid transitions only from `AwaitingApproval`.

**Technical details:**
- `Justification` VO (non-empty string, maximum length).
- Domain events: `BriefingApproved`, `BriefingRejected`, `ChangesRequested`.

**Dependencies:** BRF-003.

**Acceptance criteria:**
- **Given** `Reject("")`, **When** called, **Then** it throws a `DomainException` for a required justification.

**Estimate:** Low.

---

## BRF-016 — Use case: Approve briefing

**Title:** Implement `ApproveBriefingCommand` (RF011).

**Rules:** Design Manager only (module 1 policy).

**Dependencies:** BRF-015, BRF-005.

**Acceptance criteria:**
- **Given** a briefing in `AwaitingApproval`, **When** approved, **Then** the status changes to `Approved` and
  the `BriefingApproved` event is published.

**Estimate:** Low.

---

## BRF-017 — Use case: Reject briefing with justification

**Title:** Implement `RejectBriefingCommand` (RF012, RF013).

**Dependencies:** BRF-015, BRF-005.

**Acceptance criteria:**
- **Given** an empty justification, **When** rejection is attempted, **Then** it returns a validation failure
  before touching the entity.

**Estimate:** Low.

---

## BRF-018 — Use case: Request changes

**Title:** Implement `RequestChangesBriefingCommand` (RF014).

**Rules:** Returns the briefing to the requester in status `ChangesRequested` (editable, reuses BRF-009).

**Dependencies:** BRF-015, BRF-005.

**Acceptance criteria:**
- **Given** changes requested, **When** the requester edits and resubmits, **Then** the briefing returns to
  `AwaitingApproval`.

**Estimate:** Low.

---

## BRF-019 — Endpoints: briefing approval/rejection/changes

**Title:** Expose BRF-016 to BRF-018 via API.

**Technical details:** `POST /briefings/{id}/approve`, `/reject`, `/request-changes`.

**Dependencies:** BRF-016, BRF-017, BRF-018.

**Acceptance criteria:**
- **Given** a Designer (without Manager permission), **When** calling `/approve`, **Then** they receive `403`.

**Estimate:** Low.

---

## BRF-020 — Blazor: Design Manager approval panel

**Title:** Screen for the Manager to review, approve, reject or request changes on pending briefings.

**Dependencies:** BRF-019.

**Acceptance criteria:**
- **Given** a rejection, **When** confirmed without a filled justification, **Then** the confirm button stays
  disabled.

**Estimate:** Medium.

---

# 4. Tasks Module

## BRF-021 — Data modeling: Task, Comment, DeliveryFile

**Title:** ERD + data dictionary for `Tasks`, `Comments` and the link to delivery attachments.

**Rules:**
- Every task belongs to exactly one briefing (RF016), 1:N.
- A task has an assignee (`DesignerId`, nullable until assignment), deadline, priority, status.

**Technical details:**
- `Tasks` table: `Id`, `BriefingId` (FK), `Title`, `DesignerId` (FK, nullable), `Deadline`, `Priority`,
  `Status` (7-state enum — RF020), auditing.
- `Comments` table: `Id`, `TaskId` (FK), `AuthorId`, `Text`, `CreatedAt`.
- Reuses `Attachments` (BRF-004) for task deliveries, with an optional `TaskId` in addition to `BriefingId`.

**Dependencies:** BRF-001.

**Acceptance criteria:**
- **Given** the 7 statuses of RF020, **When** the model is reviewed, **Then** all are represented in the
  `TaskStatus` enum.

**Estimate:** Medium.

---

## BRF-022 — EF Core: TaskConfiguration, CommentConfiguration and migration

**Title:** Materialize BRF-021 via EF Core.

**Dependencies:** BRF-021, BRF-023, BRF-024.

**Acceptance criteria:**
- **Given** the migration, **When** applied, **Then** the `Tasks` and `Comments` tables exist with correct FKs.

**Estimate:** Low.

---

## BRF-023 — Task entity (state machine)

**Title:** Implement `Task` with valid transitions between the 7 statuses of RF020.

**Rules:**
- States: `NotStarted`, `InProgress`, `AwaitingTechnicalApproval`, `AwaitingRequesterApproval`,
  `Approved`, `Rejected`, `Blocked`.
- Invalid transitions throw `DomainException` (e.g. `NotStarted` → `Approved` directly is not allowed).
- Each task's `Status` is independent of the others in the same briefing (RF057).

**Technical details:**
- `UpdateStatus(newStatus)` method validates the transition via a table of allowed states.

**Dependencies:** None.

**Acceptance criteria:**
- **Given** a `NotStarted` task, **When** `UpdateStatus(Approved)` is called directly, **Then** it throws
  `DomainException`.

**Estimate:** Medium.

---

## BRF-024 — Comment entity

**Title:** Implement `Comment`, linked to `Task` (RF021).

**Rules:** Immutable once created.

**Dependencies:** None.

**Acceptance criteria:**
- **Given** a created comment, **When** the text is attempted to change, **Then** there is no public setter.

**Estimate:** Low.

---

## BRF-025 — TaskRepository

**Title:** Implement `ITaskRepository`.

**Technical details:** `GetByIdAsync`, `GetByBriefingIdAsync`, `GetByDesignerAsync(filters, pagination)`.

**Dependencies:** BRF-023, USR-010.

**Acceptance criteria:**
- **Given** a briefing with 3 tasks, **When** `GetByBriefingIdAsync` is called, **Then** it returns the 3.

**Estimate:** Low.

---

## BRF-026 — Use case: Create task(s) for an approved briefing

**Title:** Implement `CreateTaskCommand` (RF015, RF016).

**Rules:** Only allows creation if `Briefing.Status == Approved`.

**Dependencies:** BRF-023, BRF-025, BRF-005.

**Acceptance criteria:**
- **Given** a briefing in `AwaitingApproval`, **When** task creation is attempted, **Then** it returns a
  `BriefingNotApproved` failure.

**Estimate:** Medium.

---

## BRF-027 — Use case: Assign assignee

**Title:** Implement `AssignTaskCommand` (RF017).

**Rules:** Raises the `TaskAssigned` event (consumed by Notifications, RF033).

**Dependencies:** BRF-023, BRF-025.

**Acceptance criteria:**
- **Given** a task without an assignee, **When** assigned to a Designer, **Then** `DesignerId` is filled and the
  event is published.

**Estimate:** Low.

---

## BRF-028 — Use case: Set deadline and priority

**Title:** Implement `SetDeadlinePriorityCommand` (RF018, RF019).

**Dependencies:** BRF-023, BRF-025.

**Acceptance criteria:**
- **Given** a deadline in the past, **When** set, **Then** it returns a validation failure.

**Estimate:** Low.

---

## BRF-029 — Use case: Update task status

**Title:** Implement `UpdateTaskStatusCommand` (RF020).

**Rules:** Only the assigned Designer (or Manager) can update; reuses the BRF-023 state machine.

**Dependencies:** BRF-023, BRF-025.

**Acceptance criteria:**
- **Given** a Designer who is not the assignee, **When** they try to update the status, **Then** it returns
  `403`.

**Estimate:** Medium.

---

## BRF-030 — Use case: Register comment

**Title:** Implement `RegisterCommentCommand` (RF021).

**Dependencies:** BRF-024, BRF-025.

**Acceptance criteria:**
- **Given** a comment with empty text, **When** registered, **Then** it returns a validation failure.

**Estimate:** Low.

---

## BRF-031 — Use case: Upload task delivery

**Title:** Implement `SubmitTaskDeliveryCommand` (RF022), reusing `IFileStorageService` (BRF-011).

**Rules:** Each delivery submission generates a new `FileVersion` (integrates with the Versioning module —
BRF-075).

**Dependencies:** BRF-011, BRF-023, BRF-025. Versioning integration depends on BRF-074.

**Acceptance criteria:**
- **Given** an uploaded file, **When** persisted, **Then** the attachment is linked to the task and the author
  (RF052).

**Estimate:** Medium.

---

## BRF-032 — REST endpoints: TasksController

**Title:** Expose the Tasks use cases via API.

**Technical details:**
- `POST /briefings/{id}/tasks`, `PUT /tasks/{id}/assign`, `/deadline-priority`, `/status`,
  `POST /tasks/{id}/comments`, `POST /tasks/{id}/deliveries`.

**Dependencies:** BRF-026 to BRF-031.

**Acceptance criteria:**
- **Given** an invalid payload, **When** sent to any endpoint, **Then** it returns `400` with validation
  details.

**Estimate:** Low.

---

## BRF-033 — Blazor: Designer's task board

**Title:** List/kanban screen of tasks assigned to the authenticated Designer, with status updates.

**Dependencies:** BRF-032.

**Acceptance criteria:**
- **Given** a `Blocked` task, **When** listed, **Then** the UI visually highlights the block.

**Estimate:** Medium.

---

## BRF-034 — Blazor: task detail

**Title:** Detail screen with comments, delivery attachments and deadline (RF021, RF022).

**Dependencies:** BRF-032.

**Acceptance criteria:**
- **Given** a submitted delivery, **When** viewed, **Then** it shows the author and date (RF052).

**Estimate:** Medium.

---

# 5. Workflow Module

## BRF-035 — Domain service: briefing progress calculation

**Title:** Implement `BriefingProgressCalculator` (RF023, basis of RF059).

**Rules:** Progress = percentage of tasks with status `Approved` over the total number of tasks in the briefing.

**Technical details:** Pure domain service (no I/O), receives a list of tasks, returns a percentage.

**Dependencies:** BRF-023.

**Acceptance criteria:**
- **Given** 4 tasks, 2 approved, **When** calculated, **Then** it returns 50%.

**Estimate:** Low.

---

## BRF-036 — Use case: Automatic briefing completion

**Title:** Implement a domain event handler that completes the briefing when the last task is approved
(RF024, reinforced by RF058).

**Rules:** Reacts to `TaskApprovedByRequester` (published in the Delivery Approval module, BRF-069);
if all the briefing's tasks are `Approved`, transitions the `Briefing` to `Completed`.

**Dependencies:** BRF-023, BRF-035, BRF-069.

**Acceptance criteria:**
- **Given** the last pending task is approved, **When** the event is processed, **Then** the briefing changes to
  `Completed`.
- **Given** there is still an unapproved task, **When** a task is approved, **Then** the briefing stays
  `InProgress`.

**Estimate:** Medium.

---

## BRF-037 — Use case: Reopen a completed task

**Title:** Implement `ReopenTaskCommand` (RF025).

**Rules:** Design Manager only; if the briefing was `Completed`, it returns to `InProgress`.

**Dependencies:** BRF-023, BRF-025, BRF-036.

**Acceptance criteria:**
- **Given** a `Completed` briefing, **When** a task is reopened, **Then** the briefing goes back to
  `InProgress`.

**Estimate:** Low.

---

## BRF-038 — Use case: Reassign task

**Title:** Implement `ReassignTaskCommand` (RF026).

**Dependencies:** BRF-023, BRF-025, BRF-027.

**Acceptance criteria:**
- **Given** a task assigned to Designer A, **When** reassigned to Designer B, **Then** the `TaskAssigned` event
  is published for B.

**Estimate:** Low.

---

## BRF-039 — Blazor: briefing tracking page

**Title:** Screen showing the overall progress and timeline of the briefing (RF023).

**Dependencies:** BRF-035, BRF-032.

**Acceptance criteria:**
- **Given** a briefing with 50% progress, **When** displayed, **Then** the progress bar reflects the value
  calculated by the backend.

**Estimate:** Low.

---

# 6. SLA Module

## BRF-040 — Data modeling: SlaRule

**Title:** ERD for `SlaRules` (by category and/or priority).

**Technical details:** `SlaRules` table: `Id`, `CategoryId` (FK, nullable = global rule), `Priority`
(nullable), `DeadlineHours`, auditing.

**Dependencies:** BRF-059 (category).

**Acceptance criteria:**
- **Given** the model, **When** reviewed, **Then** it supports a global rule and a rule specific by
  category/priority.

**Estimate:** Low.

---

## BRF-041 — SlaRule entity

**Title:** Implement `SlaRule` + `SlaDeadline` VO.

**Dependencies:** BRF-040.

**Acceptance criteria:**
- **Given** `DeadlineHours <= 0`, **When** created, **Then** it throws `DomainException`.

**Estimate:** Low.

---

## BRF-042 — Use case: Configure SLA rule

**Title:** Implement `ConfigureSlaRuleCommand` (RF027). Administrator only.

**Dependencies:** BRF-041.

**Acceptance criteria:**
- **Given** a Manager (not Admin), **When** they try to configure, **Then** they receive `403`.

**Estimate:** Low.

---

## BRF-043 — Background job: deadline monitoring

**Title:** Implement `SlaMonitorBackgroundService` (RF028), an `IHostedService`/`BackgroundService` in
`Infrastructure`, scheduled (e.g. every N minutes).

**Rules:** Compares the task/briefing deadline (computed from the applicable `SlaRule`) with `DateTime.UtcNow`
and marks/updates the overdue flag.

**Technical details:** Idempotent job; must not reprocess items already marked as overdue.

**Dependencies:** BRF-041, BRF-023, BRF-003.

**Acceptance criteria:**
- **Given** a task with an expired deadline, **When** the job runs, **Then** it is identified as overdue and
  the `DeadlineOverdue` event is published (exactly once).

**Estimate:** High.

---

## BRF-044 — Query: identify overdue tasks

**Title:** Implement `GetOverdueTasksQuery` (RF029).

**Dependencies:** BRF-043.

**Acceptance criteria:**
- **Given** 2 overdue tasks, **When** queried, **Then** it returns the 2, with deadline and assignee.

**Estimate:** Low.

---

## BRF-045 — Query: identify overdue briefings

**Title:** Implement `GetOverdueBriefingsQuery` (RF030).

**Dependencies:** BRF-043.

**Acceptance criteria:**
- **Given** a briefing whose `DesiredDeliveryDate` has passed without completion, **When** queried, **Then** it
  appears in the list.

**Estimate:** Low.

---

## BRF-046 — Blazor: SLA and overdue dashboard

**Title:** Screen for Admin/Manager to view overdue tasks and briefings.

**Dependencies:** BRF-044, BRF-045.

**Acceptance criteria:**
- **Given** overdue items, **When** the dashboard loads, **Then** it shows the count and a detailed list.

**Estimate:** Medium.

---

# 7. Notifications Module

## BRF-047 — Notification abstraction + Notification entity

**Title:** Implement `INotificationService` (Application) and the `Notification` entity (internal inbox).

**Technical details:** `Notification`: `Id`, `UserId`, `Title`, `Message`, `Read`, `CreatedAt`,
`EventType`.

**Dependencies:** None.

**Acceptance criteria:**
- **Given** the contract, **When** reviewed, **Then** it allows in-app delivery; a future email extension does
  not change the interface.

**Estimate:** Low.

---

## BRF-048 — EF Core: NotificationConfiguration and migration

**Title:** Materialize `Notification` via EF Core.

**Dependencies:** BRF-047.

**Acceptance criteria:**
- **Given** the migration, **When** applied, **Then** the `Notifications` table exists with an index on
  `UserId`.

**Estimate:** Low.

---

## BRF-049 — Domain events of the briefing/task flow

**Title:** Consolidate the domain events needed for notifications: `BriefingApproved`,
`BriefingRejected`, `TaskAssigned`, `EntityChanged`, `DeadlineOverdue`.

**Rules:** Events are already emitted in the entities (BRF-015, BRF-027, BRF-043); this task ensures they are
all registered in a single publishing pipeline (e.g. an `IDomainEventDispatcher` on `SaveChanges`).

**Dependencies:** BRF-015, BRF-027, BRF-043.

**Acceptance criteria:**
- **Given** any of the entities is changed, **When** `SaveChanges` is called, **Then** the pending events are
  dispatched after the commit.

**Estimate:** Medium.

---

## BRF-050 — Handler: briefing approval/rejection notification

**Title:** Implement `BriefingApproved`/`BriefingRejected` handlers that notify the requester
(RF031, RF032).

**Dependencies:** BRF-047, BRF-049.

**Acceptance criteria:**
- **Given** an approved briefing, **When** the event is processed, **Then** a `Notification` is created for the
  requester.

**Estimate:** Low.

---

## BRF-051 — Handler: task assignment notification

**Title:** Implement the `TaskAssigned` handler (RF033).

**Dependencies:** BRF-047, BRF-049.

**Acceptance criteria:**
- **Given** an assigned task, **When** processed, **Then** the Designer receives a notification.

**Estimate:** Low.

---

## BRF-052 — Handler: relevant change notification

**Title:** Implement a generic `EntityChanged` handler for the people involved in a briefing/task
(RF034).

**Rules:** "Involved" = requester, assigned Designer(s) and the Manager responsible for approval.

**Dependencies:** BRF-047, BRF-049.

**Acceptance criteria:**
- **Given** a reassigned task, **When** processed, **Then** the requester and Manager are notified of the
  change.

**Estimate:** Medium.

---

## BRF-053 — Handler: overdue notification

**Title:** Implement the `DeadlineOverdue` handler (RF035), integrating with the SLA job (BRF-043).

**Dependencies:** BRF-047, BRF-043.

**Acceptance criteria:**
- **Given** a task marked overdue, **When** the event is processed, **Then** the assignee is notified.

**Estimate:** Low.

---

## BRF-054 — Blazor: notifications component

**Title:** Notification bell with an unread count and list (in-app).

**Technical details:** Evaluate periodic polling vs. SignalR for real-time updates (architecture decision to
validate).

**Dependencies:** BRF-047.

**Acceptance criteria:**
- **Given** unread notifications, **When** the user opens the bell, **Then** they all get marked as read.

**Estimate:** Medium.

---

# 8. History and Auditing Module

## BRF-055 — ChangeHistory entity

**Title:** Implement a generic `ChangeHistory` (RF038): affected entity, user, date/time, operation.

**Technical details:** `Id`, `EntityType`, `EntityId`, `UserId`, `Timestamp`, `Operation`,
`DataBefore`/`DataAfter` (JSON, optional).

**Dependencies:** None.

**Acceptance criteria:**
- **Given** the model, **When** reviewed, **Then** it covers the 4 fields required by RF038.

**Estimate:** Low.

---

## BRF-056 — EF Core: HistoryConfiguration and migration

**Title:** Materialize `ChangeHistory` via EF Core.

**Dependencies:** BRF-055.

**Acceptance criteria:**
- **Given** the migration, **When** applied, **Then** the `ChangeHistory` table exists with a composite index
  `(EntityType, EntityId)`.

**Estimate:** Low.

---

## BRF-057 — Auditing interceptor for Briefing and Task

**Title:** Extend the auditing pattern (USR-002) to write a `ChangeHistory` on each relevant change
in `Briefing` and `Task` (RF036, RF037).

**Rules:** Every status transition and edit of tracked fields generates a history entry.

**Technical details:** A `SaveChangesInterceptor` (or dedicated domain event) that compares previous/new state
via the `ChangeTracker` and writes the record.

**Dependencies:** BRF-055, BRF-003, BRF-023, USR-002.

**Acceptance criteria:**
- **Given** a task that changes status, **When** `SaveChanges` is called, **Then** a `ChangeHistory` is created
  with user, date/time and operation.

**Estimate:** High.

---

## BRF-058 — Blazor: history tab

**Title:** Show the change history in the briefing and task detail.

**Dependencies:** BRF-057.

**Acceptance criteria:**
- **Given** a briefing with 3 changes, **When** the history tab is opened, **Then** the 3 appear in
  chronological order.

**Estimate:** Low.

---

# 9. Briefing Categories Module

## BRF-059 — Data modeling: BriefingCategory and CustomField

**Title:** ERD for `BriefingCategories` and `CustomFields` (RF039, RF041).

**Technical details:**
- `BriefingCategories`: `Id`, `Name`, `Active`, auditing + soft delete.
- `CustomFields`: `Id`, `CategoryId` (FK), `Name`, `FieldType` (enum: text, number, date, boolean,
  list), `Required`.

**Dependencies:** None.

**Acceptance criteria:**
- **Given** the model, **When** reviewed, **Then** it supports N custom fields per category with distinct
  types.

**Estimate:** Low.

---

## BRF-060 — BriefingCategory and CustomField entities

**Title:** Implement the entities + `FieldType` VO.

**Dependencies:** BRF-059.

**Acceptance criteria:**
- **Given** an inactive category, **When** associated with a new briefing, **Then** the business rule (BRF-062)
  rejects the association.

**Estimate:** Low.

---

## BRF-061 — Use case: Category CRUD

**Title:** Implement create/edit/deactivate category commands (RF039). Administrator only.

**Dependencies:** BRF-060.

**Acceptance criteria:**
- **Given** a category in use by existing briefings, **When** deactivated, **Then** existing briefings are not
  affected (only its use in new ones is blocked).

**Estimate:** Medium.

---

## BRF-062 — Domain rule: category mandatory on the briefing

**Title:** Ensure every briefing has an active category (RF040), adjusting `Briefing.Create(...)`
(BRF-003) and `CreateBriefingCommand` (BRF-006).

**Dependencies:** BRF-003, BRF-006, BRF-060.

**Acceptance criteria:**
- **Given** a `CategoryId` of an inactive category, **When** a briefing is created, **Then** it returns a
  `CategoryInactive` failure.

**Estimate:** Low.

---

## BRF-063 — Use case: define custom fields per category

**Title:** Implement `SetCustomFieldsCommand` (RF041). Administrator only.

**Dependencies:** BRF-060, BRF-061.

**Acceptance criteria:**
- **Given** a category, **When** fields are defined, **Then** they become available for use in BRF-089/090.

**Estimate:** Medium.

---

## BRF-064 — Blazor: category and custom fields management

**Title:** Admin screen for categories and their fields (RF039, RF041).

**Dependencies:** BRF-061, BRF-063.

**Acceptance criteria:**
- **Given** a new field of type list, **When** registered, **Then** the UI allows defining the list options.

**Estimate:** Medium.

---

# 10. Delivery Approval Module

## BRF-065 — Domain: technical/requester approval of the task

**Title:** Extend `Task` with `SubmitForTechnicalApproval`, `ApproveTechnically`,
`RejectTechnically`, `SubmitForRequesterApproval`, `ApproveByRequester`,
`RejectByRequester` methods (RF042–RF048).

**Rules:**
- Rejection (technical or final) requires a justification (reuses the `Justification` VO from BRF-015).
- Rejection returns the task to `InProgress` (RF048), assigned to the same Designer.

**Dependencies:** BRF-023, BRF-015.

**Acceptance criteria:**
- **Given** `RejectTechnically("")`, **When** called, **Then** it throws `DomainException`.
- **Given** a task rejected by the requester, **When** processed, **Then** the status returns to `InProgress`.

**Estimate:** Medium.

---

## BRF-066 — Use case: Submit delivery for technical approval

**Title:** Implement `SubmitDeliveryForTechnicalApprovalCommand` (RF042).

**Dependencies:** BRF-065, BRF-025.

**Acceptance criteria:**
- **Given** a task without an attached delivery, **When** submitted for technical approval, **Then** it returns
  a `DeliveryMissing` failure.

**Estimate:** Low.

---

## BRF-067 — Use case: Approve/reject technically

**Title:** Implement `ApproveTechnicallyCommand` / `RejectTechnicallyCommand` (RF043, RF044).

**Dependencies:** BRF-065, BRF-025.

**Acceptance criteria:**
- **Given** technical approval, **When** processed, **Then** the task advances to
  `AwaitingRequesterApproval`.

**Estimate:** Low.

---

## BRF-068 — Use case: Forward to requester approval

**Title:** Implement the automatic transition after technical approval (RF045) — part of the BRF-067 handler, or
a dedicated event handler.

**Dependencies:** BRF-067.

**Acceptance criteria:**
- **Given** technical approval completed, **When** processed, **Then** the requester is notified (integrates
  BRF-052) of the pending final approval.

**Estimate:** Low.

---

## BRF-069 — Use case: Requester final approval

**Title:** Implement `ApproveByRequesterCommand` / `RejectByRequesterCommand` (RF046, RF047).

**Rules:** Final approval publishes `TaskApprovedByRequester` (consumed by BRF-036 — automatic briefing
completion).

**Dependencies:** BRF-065, BRF-025.

**Acceptance criteria:**
- **Given** final approval, **When** processed, **Then** the task changes to `Approved` and the event is
  published.

**Estimate:** Medium.

---

## BRF-070 — Use case: Return for correction

**Title:** Ensure that rejection (technical or final) returns the task to the assigned Designer (RF048) —
already covered by the domain rule in BRF-065; this task covers the associated notification and history
record.

**Dependencies:** BRF-065, BRF-052, BRF-057.

**Acceptance criteria:**
- **Given** a final rejection, **When** processed, **Then** the Designer is notified with the justification.

**Estimate:** Low.

---

## BRF-071 — Delivery approval endpoints

**Title:** Expose BRF-066, BRF-067, BRF-069 via API.

**Technical details:** `POST /tasks/{id}/submit-technical-approval`, `/approve-technical`, `/reject-technical`,
`/approve-final`, `/reject-final`.

**Dependencies:** BRF-066, BRF-067, BRF-069.

**Acceptance criteria:**
- **Given** a requester who does not own the briefing, **When** calling `/approve-final`, **Then** they receive
  `403`.

**Estimate:** Low.

---

## BRF-072 — Blazor: technical and final approval screens

**Title:** UI for the Manager (technical approval) and the Requester (final approval) to review deliveries.

**Dependencies:** BRF-071.

**Acceptance criteria:**
- **Given** a rejection, **When** confirmed without a justification, **Then** the confirm button stays
  disabled.

**Estimate:** Medium.

---

# 11. Versioning Module

## BRF-073 — Data modeling: FileVersion

**Title:** ERD for `FileVersions` (RF049–RF052).

**Technical details:** `Id`, `TaskId` (FK), `VersionNumber`, `StoragePath`, `AuthorId`, `UploadedAt`.

**Dependencies:** BRF-021.

**Acceptance criteria:**
- **Given** the model, **When** reviewed, **Then** it guarantees uniqueness of `(TaskId, VersionNumber)`.

**Estimate:** Low.

---

## BRF-074 — FileVersion entity + increment rule

**Title:** Implement `FileVersion`, with `VersionNumber` incremented automatically per task.

**Dependencies:** BRF-073.

**Acceptance criteria:**
- **Given** 2 existing versions for a task, **When** a new one is created, **Then** it receives
  `VersionNumber = 3`.

**Estimate:** Low.

---

## BRF-075 — Use case: register a new version when submitting a delivery

**Title:** Integrate `SubmitTaskDeliveryCommand` (BRF-031) to create a `FileVersion` on each submission
(RF049, RF052).

**Dependencies:** BRF-031, BRF-074, BRF-011.

**Acceptance criteria:**
- **Given** a new uploaded file, **When** persisted, **Then** a `FileVersion` with author and timestamp is
  created.

**Estimate:** Low.

---

## BRF-076 — Use case: list version history

**Title:** Implement `GetFileVersionsQuery` (RF050).

**Dependencies:** BRF-074.

**Acceptance criteria:**
- **Given** a task with 3 versions, **When** queried, **Then** it returns the 3 in descending order.

**Estimate:** Low.

---

## BRF-077 — Use case: download a previous version

**Title:** Implement `DownloadFileVersionQuery` (RF051), with an authorization check (people involved in the
briefing/task).

**Dependencies:** BRF-074, BRF-011.

**Acceptance criteria:**
- **Given** a user not involved in the briefing, **When** they try to download a version, **Then** they receive
  `403`.

**Estimate:** Medium.

---

## BRF-078 — Blazor: version history/download component

**Title:** Reusable UI to list and download the versions of a delivery.

**Dependencies:** BRF-076, BRF-077.

**Acceptance criteria:**
- **Given** multiple versions, **When** displayed, **Then** the author and date of each are visible (RF052).

**Estimate:** Low.

---

# 12. Corporate Integration Module

## BRF-079 — Infra: corporate authentication adapter

**Title:** Implement a client/adapter for the corporate provider (OIDC/SAML — protocol to confirm with
stakeholders before starting).

**Rules:** Abstracted via an interface in `Application` (`ICorporateAuthProvider`), with the concrete
implementation in `Infrastructure`.

**Dependencies:** USR-025..028 (auth pipeline already existing from module 1).

**Acceptance criteria:**
- **Given** the interface, **When** reviewed, **Then** it does not expose protocol details (OIDC/SAML) to the
  inner layers.

**Estimate:** High.

> **Note:** RF053/RF055 depend on a protocol decision (OIDC vs. SAML) and on the corporate identity provider —
> flag as a gray area to discuss before starting this task.

---

## BRF-080 — Use case: corporate login + user synchronization

**Title:** Implement `CorporateLoginCommand` (RF053, RF054).

**Rules:** On authentication, synchronizes `Name`, `Email`, `Department` of the corporate user with the local
`User` (creates if it does not exist, updates if it does).

**Dependencies:** BRF-079, USR-005 (the `User` entity).

**Acceptance criteria:**
- **Given** a corporate user authenticating for the first time, **When** processed, **Then** a local `User` is
  created with the synchronized data.

**Estimate:** High.

---

## BRF-081 — SSO pipeline configuration

**Title:** Configure the authentication middleware to accept corporate single sign-on (RF055), coexisting with
local login (module 1 JWT).

**Dependencies:** BRF-079, BRF-080, USR-028.

**Acceptance criteria:**
- **Given** both methods configured, **When** a user authenticates via SSO, **Then** they receive the same
  access-token format issued by the local login.

**Estimate:** High.

---

## BRF-082 — Blazor: corporate login flow

**Title:** Corporate login button/redirect on the existing login screen (USR-041).

**Dependencies:** BRF-081.

**Acceptance criteria:**
- **Given** the corporate login button, **When** clicked, **Then** the user is redirected to the provider and
  returns authenticated.

**Estimate:** Medium.

---

# 13. Task and Approval Control Module

## BRF-083 — Domain rule: independent status per task

**Title:** Validate/reinforce that `Task.UpdateStatus` (BRF-023) never depends on the status of other tasks in
the same briefing (RF057).

**Technical details:** Dedicated test coverage (not new logic, it is an architectural guarantee).

**Dependencies:** BRF-023.

**Acceptance criteria:**
- **Given** two tasks in the same briefing, **When** one changes status, **Then** the other stays unchanged.

**Estimate:** Low.

---

## BRF-084 — Use case: approve/reject a task individually

**Title:** Confirm that BRF-067/BRF-069 already operate per individual task, with no side effect on other
tasks in the briefing (RF056).

**Technical details:** Consolidation/validation task, introduces no new code if BRF-067/069 follow the
BRF-083 rule.

**Dependencies:** BRF-067, BRF-069, BRF-083.

**Acceptance criteria:**
- **Given** a briefing with 3 tasks, **When** one is approved individually, **Then** the other 2 keep their
  status.

**Estimate:** Low.

---

## BRF-085 — Domain rule: briefing completion requires all tasks approved

**Title:** Reinforce in BRF-036 that automatic briefing completion (RF058) only happens with 100% of the tasks
in `Approved`.

**Technical details:** A regression test ensuring the BRF-036 rule covers exactly this criterion.

**Dependencies:** BRF-036.

**Acceptance criteria:**
- **Given** 3 of 4 tasks approved, **When** the last is rejected and then re-approved, **Then** the briefing
  only completes on the approval of the 4th.

**Estimate:** Low.

---

## BRF-086 — Query: briefing progress percentage

**Title:** Expose `BriefingProgressCalculator` (BRF-035) via `GetBriefingProgressQuery` (RF059).

**Dependencies:** BRF-035.

**Acceptance criteria:**
- **Given** a queried briefing, **When** the query runs, **Then** it returns the percentage computed in
  real time (not stored in a stale cache).

**Estimate:** Low.

---

# 14. Intelligent Structuring of Briefings Module

## BRF-087 — Data modeling: StructuredField

**Title:** ERD for `StructuredFields` (RF060) — typed key/value capture linked to the briefing.

**Technical details:** `Id`, `BriefingId` (FK), `Key` (e.g. "TargetAudience", "DistributionChannel"), `Value`,
`ValueType`.

**Dependencies:** BRF-001.

**Acceptance criteria:**
- **Given** the model, **When** reviewed, **Then** it supports the RF060 example fields (demand objective,
  target audience, channel, number of pieces, event date, region, campaign type, expected indicators).

**Estimate:** Low.

---

## BRF-088 — StructuredField entity

**Title:** Implement `StructuredField` + `StructuredFieldType` VO.

**Dependencies:** BRF-087.

**Acceptance criteria:**
- **Given** a value incompatible with the declared `ValueType`, **When** created, **Then** it throws
  `DomainException`.

**Estimate:** Low.

---

## BRF-089 — Use case: capture structured fields in the briefing

**Title:** Implement `SetStructuredFieldsCommand` (RF060), integrated into the briefing create/edit flow
(BRF-006/BRF-009).

**Dependencies:** BRF-088, BRF-006, BRF-009.

**Acceptance criteria:**
- **Given** a briefing, **When** structured fields are filled, **Then** they are associated and retrievable
  via `BriefingDto`.

**Estimate:** Medium.

---

## BRF-090 — Use case: validate mandatory fields per category

**Title:** Implement `IChecklistValidator`/a per-category mandatory-field validator (RF061),
consumed by BRF-007 (submit for approval).

**Rules:** Mandatory fields are defined by `CustomField.Required` (BRF-060/063).

**Dependencies:** BRF-063, BRF-089.

**Acceptance criteria:**
- **Given** a category with an unfilled mandatory field, **When** validation runs, **Then** it returns a list
  of pending items.

**Estimate:** Medium.

---

## BRF-091 — Data modeling: ChecklistItem per category

**Title:** ERD for `ChecklistItems` (RF062) and the link to the per-briefing fill state.

**Technical details:** `ChecklistItems`: `Id`, `CategoryId` (FK), `Description`, `Required`.
`BriefingChecklistItems`: `BriefingId`, `ChecklistItemId`, `Checked`.

**Dependencies:** BRF-059.

**Acceptance criteria:**
- **Given** the model, **When** reviewed, **Then** it allows the RF062 examples ("Script attached", "Logo
  attached" etc.) per category.

**Estimate:** Low.

---

## BRF-092 — Use case: configure mandatory checklist per category

**Title:** Implement `ConfigureCategoryChecklistCommand` (RF062). Administrator only.

**Dependencies:** BRF-091, BRF-060.

**Acceptance criteria:**
- **Given** a category, **When** checklist items are configured, **Then** they become available for checking on
  briefings of that category.

**Estimate:** Medium.

---

## BRF-093 — Use case: validate checklist before submitting for approval

**Title:** Implement checklist validation (RF063), integrated with `IChecklistValidator` (BRF-090) and
consumed by `SubmitBriefingForApprovalCommand` (BRF-007).

**Dependencies:** BRF-091, BRF-092, BRF-007.

**Acceptance criteria:**
- **Given** an unchecked mandatory checklist item, **When** submission for approval is attempted, **Then** it
  returns a `ChecklistPending` failure with the list of missing items.

**Estimate:** Medium.

---

## BRF-094 — Blazor: dynamic structured-fields and checklist form

**Title:** UI that renders custom fields (BRF-063), structured fields (BRF-089) and the checklist
(BRF-092) dynamically according to the chosen category.

**Dependencies:** BRF-063, BRF-089, BRF-092, BRF-013.

**Acceptance criteria:**
- **Given** a category change in the form, **When** selected, **Then** the fields and checklist are updated to
  those of the new category.

**Estimate:** High.

---

# 15. Tests

## BRF-095 — Unit tests: entities and domain rules

**Title:** Cover with AAA tests (`.claude/rules/unit-tests.md`) the state machines and invariants of
`Briefing`, `Task`, `BriefingCategory`, `SlaRule`, `FileVersion`, `StructuredField`.

**Dependencies:** All the listed entities.

**Acceptance criteria:**
- **Given** each invalid transition mapped in the modules above, **When** tested, **Then** it throws
  `DomainException` (a dedicated test per scenario, no conditional logic in the test).

**Estimate:** High.

---

## BRF-096 — Unit tests: main use cases per module

**Title:** Cover command/query handlers with mocked repositories and services (`Moq`), one test per relevant
success/failure scenario.

**Dependencies:** All handlers listed in modules 2–14.

**Acceptance criteria:**
- **Given** each handler, **When** tested, **Then** it covers at least one success path and one
  business-rule failure path.

**Estimate:** High.

---

## BRF-097 — Integration test: main system flow

**Title:** Cover the "Main System Flow" described in `requirements-doc.pdf` end to end: briefing creation
→ approval → task creation → assignment → delivery → technical approval → final approval →
briefing completion.

**Technical details:** Integration test with a real database (test SQLite/Postgres), no repository mocks —
validating the full flow across layers.

**Dependencies:** BRF-006, BRF-016, BRF-026, BRF-027, BRF-031, BRF-067, BRF-069, BRF-036.

**Acceptance criteria:**
- **Given** the full flow executed, **When** the last task is approved by the requester, **Then** the briefing
  is `Completed` and all versions/history are recorded.

**Estimate:** High.

---

## BRF-098 — Integration test: SLA and overdue notifications

**Title:** Cover the SLA job (BRF-043) triggering overdue notifications (BRF-053) end to end.

**Dependencies:** BRF-043, BRF-053.

**Acceptance criteria:**
- **Given** a task with an expired deadline in the test database, **When** the job runs, **Then** an overdue
  `Notification` is created for the assignee.

**Estimate:** Medium.

---

## Dependency summary (macro view)

```
[Module 1: USR-001..054]  (prerequisite for everything below)
        │
        ▼
BRF-059..064 (Categories) ─▶ BRF-001..014 (Briefings)
        │                           │
        │                           ▼
        │                   BRF-015..020 (Briefing Approval)
        │                           │
        │                           ▼
        │                   BRF-021..034 (Tasks)
        │                           │
        │        ┌──────────────────┼───────────────────┐
        │        ▼                  ▼                    ▼
        │  BRF-035..039       BRF-065..072          BRF-073..078
        │  (Workflow)         (Delivery Approval)   (Versioning)
        │        │                  │                    │
        │        └────────┬─────────┴────────────────────┘
        │                 ▼
        │         BRF-083..086 (Task and Approval Control)
        │                 │
        ▼                 ▼
BRF-087..094      BRF-040..046 (SLA) ─▶ BRF-047..054 (Notifications)
(Intelligent Structuring)                      │
        │                                      ▼
        │                            BRF-055..058 (History/Auditing)
        │
        ▼
BRF-079..082 (Corporate Integration — parallelizable after module 1)
        │
        ▼
BRF-095..098 (Tests — continuous per wave, consolidated at the end)
```

> **Note:** BRF-079–081 (Corporate Integration) depend on a protocol decision (OIDC/SAML) and an identity
> provider not yet defined in `requirements-doc.pdf` — treat as a gray area to discuss before specifying that
> feature in `.specs/features/`.
