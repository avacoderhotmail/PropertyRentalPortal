# Apex Apartments - Property Rental Management Portal

A full-stack ASP.NET Core MVC web application built with .NET 10, Entity Framework Core, ASP.NET Identity, and Razor for property managers and apartment applicants.

## Table of Contents

- [Overview](#overview)
- [Prerequisites and Non-Standard Setup](#prerequisites-and-non-standard-setup)
- [Architecture and Design Decisions](#architecture-and-design-decisions)
- [Features and Technical Requirements](#features-and-technical-requirements)
- [Demo Credentials and Quick Switching](#demo-credentials-and-quick-switching)
- [Getting Started and Running](#getting-started-and-running)
- [Running the Unit Tests](#running-the-unit-tests)
- [Bonus Capabilities](#bonus-capabilities)

## Overview

Apex Apartments is a property management portal facilitating apartment listings, tenant applications, and lease generation. The application enforces two roles:

- **Property Manager**: Maintains properties and units via modal dialogues, manages active/inactive unit type lookups, monitors the review queue, and reviews incoming applications to issue 12-month leases.
- **Applicant**: Discovers available apartments, completes a multi-step single-page rental application wizard, maintains prior residence records via modals, and tracks/resubmits applications.

## Prerequisites and Non-Standard Setup

The application employs specific platform dependencies and forward-looking packages that require attention:

### 1 .NET 10 SDK Requirement

This application explicitly targets `net10.0`. Building or running on older runtimes (such as .NET 8 or .NET 9) will result in target framework resolution errors.

- Verify your installed SDK version:

```powershell
dotnet --version
```

- If the output does not start with `10.`, install the .NET 10 SDK from Microsoft's download portal before proceeding.

### 2 SQL Server LocalDB Configuration and Fallback

The application is pre-configured for Windows SQL Server LocalDB via the connection string:
`Server=(localdb)\mssqllocaldb;Database=PropertyRentalPortalDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`

- **Windows LocalDB Verification**: If the default `mssqllocaldb` instance has never been initialized on your machine, initialize and start it:

```powershell
sqllocaldb create mssqllocaldb
sqllocaldb start mssqllocaldb
```

- **Alternative SQL Server / Docker Container**: If running on non-Windows systems, full SQL Server, or a Docker container (`mcr.microsoft.com/mssql/server`), override the connection string via the `ConnectionStrings__DefaultConnection` environment variable or in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=PropertyRentalPortalDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
}
```

### 3 Automated Database Creation and Seeding

Unlike standard ASP.NET Core setups that require executing manual migrations (`dotnet ef database update`) or running separate SQL seeding scripts:

- **Zero-Touch Startup Migration**: On initial launch, `Program.cs` automatically invokes `await context.Database.MigrateAsync()`. If the database does not exist, it is created automatically.
- **Idempotent Bogus Seeding**: Seeding runs automatically on startup via `DatabaseSeeder.SeedAsync()`. It populates all lookups, active and inactive unit types, sample properties, units, and applications in **every status** (`Draft`, `Submitted`, `UnderReview`, `Returned`, `Approved`, `Denied`, `Withdrawn`) without requiring any manual database setup.

### 4 Scalar OpenAPI Endpoint Location

Rather than the traditional Swashbuckle Swagger UI (`/swagger/index.html`), this .NET 10 project integrates the modern **Scalar API Reference** engine powered by Microsoft's native `Microsoft.AspNetCore.OpenApi`:

- Interactive API Documentation UI: `http://localhost:5141/scalar/v1`
- Raw OpenAPI v1 JSON Schema: `http://localhost:5141/openapi/v1.json`

## Architecture and Design Decisions

The solution is configured for .NET 10 LTS with `global.json` pinning and includes:

- `PropertyRentalPortal.sln` & `PropertyRentalPortal.slnx`: Provides both classic and modern solution files referencing all projects.
- `PropertyRentalPortal.Web`: The main ASP.NET Core 10 LTS MVC application containing:
  - Controllers for accounts, properties, applications, prior residences, application review, and an OpenAPI-documented REST grid endpoint.
  - Domain entities and `ApplicationDbContext` with code-first migrations.
  - Business service layer (`IApplicationService`, `IUnitService`) isolating core validation, lease checks, and review workflows.
  - Reusable Razor Partial Views for modal forms and wizard sections.
  - Razor View Components (`StatusBadge`, `ApplicationTimeline`, `PropertyManagerNotes`, `ReusableGrid`).
  - Idempotent database seeder using Bogus for realistic tenant data.
- `PropertyRentalPortal.Tests`: Comprehensive xUnit test suite containing 21 unit tests validating all business logic, lease conflicts, lookup enforcement, and review outcomes.

### Design Highlights

- **Single-Page Wizard Pattern**: Driven by a single view model (`ApplicationWizardViewModel`) and one action (`ProcessStep`). The action evaluates which button was clicked (`Back`, `Continue`, `Submit`), validating and persisting the active section before moving forward.
- **Server-Side Section Security**: The same partial view dynamically renders as editable or read-only based on the application's lifecycle status. Editing is only permitted for applicants when in `Draft` or `Returned` status. Posts attempting unauthorized modifications are rejected on the server.
- **Double-Lease Protection**: Unit availability requires that no active lease covers the current date. Active lease checks are enforced at unit browsing, at application submission, and again at approval to prevent race conditions.
- **Modal Re-rendering via Partial Views**: Controller actions return partial views for modals. When model validation fails, the same partial view is returned with validation tags, allowing immediate in-place feedback without closing the modal.
- **Unit Type Lookup Enforcement**: When creating or editing units, inactive unit types cannot be selected. Units already assigned an inactive unit type display it with an inactive label, but other units cannot adopt it.

## Features and Technical Requirements

### 1 Application Lifecycle and Statuses

Applications progress through clearly defined statuses:

- `Draft`: Initial state created by an applicant; editable.
- `Submitted`: Both sections validated and submitted by applicant.
- `UnderReview`: Claimed by a property manager in the review queue.
- `Returned`: Returned by a property manager for corrections with mandatory comments; editable by applicant for resubmission.
- `Approved`: Terminal state; automatically creates and issues a 12-month lease for the unit.
- `Denied`: Terminal state; denied by a property manager with mandatory comments.
- `Withdrawn`: Terminal state; withdrawn by applicant.

### 2 View Components and Partial Views

- **View Components**:
  - `StatusBadgeViewComponent`: Renders role-aware status badges with semantic styling and iconography.
  - `ApplicationTimelineViewComponent`: Displays chronological audit trails of status transitions, review outcomes, authors, timestamps, and feedback comments.
  - `PropertyManagerNotesViewComponent`: Renders confidential internal management notes; strictly omitted for applicants.
  - `ReusableGridViewComponent`: Renders database-paged, sortable application listings with queue actions.
- **Partial Views**:
  - `_ApplicantInfoPartial.cshtml`: Renders Section 1 in editable or read-only mode.
  - `_ResidenceHistoryPartial.cshtml`: Renders Section 2 in editable or read-only mode with modal triggers.
  - `_ApplicationSummaryPartial.cshtml`: Read-only review with readiness validation.
  - `_ResidenceModalPartial.cshtml`: Modal dialog for adding/editing prior residences.
  - `_PropertyModalPartial.cshtml`: Modal dialog for creating/updating properties.
  - `_UnitModalPartial.cshtml`: Modal dialog for creating/updating units with lookup validation.
  - `_ReviewModalPartial.cshtml`: Modal dialog for approving, returning, or denying applications.

## Demo Credentials and Quick Switching

The database seeds default accounts with Bogus-generated applications:

- **Property Manager**:
  - Email: `manager@rental.com`
  - Password: `Password123!`
- **Applicant**:
  - Email: `applicant@rental.com`
  - Password: `Password123!`

A **Demo Switch** dropdown in the navigation bar enables 1-click role toggling during review.

## Getting Started and Running

1 Ensure SQL Server LocalDB is running:

```powershell
sqllocaldb start mssqllocaldb
```

2 Run the web application:

```powershell
cd C:\<your repo directory>\PropertyRentalPortal
dotnet run --project PropertyRentalPortal.Web/PropertyRentalPortal.Web.csproj
```

3 Open your browser to `http://localhost:5141` or `https://localhost:7156`.

4 Explore the interactive OpenAPI documentation at `http://localhost:5141/scalar/v1`.

## Running the Unit Tests

Execute the xUnit test suite from the root folder:

```powershell
dotnet test
```

All 21 unit tests validate:

- UnitType active/inactive server enforcement.
- Lease term availability covering current dates.
- Double-lease prevention during both submission and approval.
- Single-page wizard validation step requirements.
- Review outcome comment mandates for Return and Deny.
- Review queue claim and release transitions.

## Bonus Capabilities

- **Bonus 1 - Reusable Grid & OpenAPI**: Database-level paging, sorting, and filtering exposed via `GET /api/applicationsapi` and documented using OpenAPI with Scalar UI at `/scalar/v1`.
- **Bonus 2 - Review Queue**: Property managers can claim submitted applications into `UnderReview` status and release them back to the general queue.
- **Bonus 3 - Property Manager Notes**: Staff-only notes stored separately and never rendered or returned to applicants.
