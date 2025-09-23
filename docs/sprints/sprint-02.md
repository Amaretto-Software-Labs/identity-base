# Sprint 02 – Identity Core & Registration Metadata

## Focus & Priority
- Implement ASP.NET Core Identity, database schema, and configurable user metadata required for registration flows.
- Priority: **High** for Identity features; supporting docs/tasks medium.

## Streams
- **Identity & Users** – Configure Identity options, user entities, metadata pipeline.
- **Data & Persistence** – Create migrations, JSONB metadata storage, unit-of-work abstraction.
- **Email & Notifications** – Wire MailJet sender for confirmation flows.
- **Documentation & Enablement** – Update guides with registration/metadata instructions.

## Stories

### S2-ID-101: Configure ASP.NET Core Identity with Custom User (Priority: High, Stream: Identity & Users)
**Description**
Set up Identity with `ApplicationUser`/`ApplicationRole` (Guid keys) and align with engineering principles.

**Acceptance Criteria**
- `ApplicationUser` includes base profile fields (Email, DisplayName, CreatedAt, etc.).
- Identity services configured with password/lockout policies and email confirmation required.
- User and role stores registered via `AddIdentityCore` with token providers.

**Tasks**
- [ ] Create `ApplicationUser`/`ApplicationRole` classes inheriting Identity types with Guid keys.
- [ ] Configure Identity options via `builder.Services.Configure<IdentityOptions>` (password, lockout, user settings).
- [ ] Register token providers for email confirmation, password reset, TOTP (security stamp).
- [ ] Add initial data seeding hook for super admin (optional, disabled by default).

**Dependencies**
- Sprint 1 infrastructure completed.

### S2-DATA-102: Implement DbContext & Migrations with Metadata Support (Priority: High, Stream: Data & Persistence)
**Description**
Finalize `AppDbContext` to extend `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` and support metadata storage via JSONB.

**Acceptance Criteria**
- DbContext configured with Identity and OpenIddict entity sets (placeholders) and `OnModelCreating` applying configurations.
- `UserProfileMetadata` value object mapped to JSONB column on `AspNetUsers` table with indexes for search.
- `InitialIdentity` migration created, reviewed, and documented in `Identity.Base/docs/README.md`.

**Tasks**
- [ ] Update `AppDbContext` to inherit `IdentityDbContext` and include DbSet placeholders for OpenIddict.
- [ ] Create `ApplicationUserConfiguration` implementing `IEntityTypeConfiguration` (Guid keys, CreatedAt default, JSONB metadata property with conversion).
- [ ] Implement `UserProfileMetadata` class supporting dictionary-like access with validation helpers.
- [ ] Generate migration `InitialIdentity`, review SQL for uuid/JSONB configuration, document in README.

**Dependencies**
- S2-ID-101.

### S2-API-103: Registration Endpoint with Metadata Validation (Priority: High, Stream: Identity & Users)
**Description**
Deliver `/auth/register` minimal API endpoint supporting configurable metadata fields.

**Acceptance Criteria**
- `RegistrationOptions` bound from `Registration:ProfileFields`; validation errors returned via ProblemDetails.
- Endpoint accepts email, password, metadata payload; persists user and metadata in transaction.
- On success, sends confirmation email and returns 202 with correlation id.

**Tasks**
- [ ] Implement options classes for metadata field definitions (name, type, required, validation rules).
- [ ] Create DTOs + FluentValidation validators referencing metadata configuration.
- [ ] Implement command handler using `UserManager` + UnitOfWork to create user, attach metadata, invoke `IEmailSender`.
- [ ] Add integration test verifying registration persists metadata and produces confirmation email request (mocked MailJet).

**Dependencies**
- S2-DATA-102, S2-EMAIL-104.

### S2-EMAIL-104: MailJet Email Sender Integration (Priority: Medium, Stream: Email & Notifications)
**Description**
Implement MailJet templated email sender for confirmation/reset flows following internal guide.

**Acceptance Criteria**
- `MailJetEmailSender` created per guidance with template-only enforcement.
- Configuration bound from `MailJet` section; secrets pulled from user secrets/environment variables.
- Registration flow uses confirmation template; logs mail send results.

**Tasks**
- [ ] Add `Mailjet.Api` package and implement sender with options binding + logging.
- [ ] Register `IEmailSender`/`ITemplatedEmailSender` in DI and add health check stub for MailJet (optional ping).
- [ ] Extend docs with template configuration instructions and update sample `appsettings.Development.json`.

**Dependencies**
- S2-API-103.

### S2-DOCS-105: Update Documentation for Identity & Metadata (Priority: Medium, Stream: Documentation & Enablement)
**Description**
Ensure docs cover Identity setup, migrations, metadata configuration, and email requirements.

**Acceptance Criteria**
- `Identity.Base/docs/README.md` updated with migration instructions and metadata configuration examples.
- `/docs/getting-started.md` includes prerequisites to configure registration fields.
- Changelog entry describing sprint deliverables created.

**Tasks**
- [ ] Document running `dotnet ef migrations add InitialIdentity` and database update steps.
- [ ] Provide sample `Registration` section in `appsettings` with metadata field definitions (name/company/position).
- [ ] Add changelog entry summarizing new capabilities and dependencies.

**Dependencies**
- Dependent on stories completing implementation details.
