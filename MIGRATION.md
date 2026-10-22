# MIGRATION.md

## 10.1 Migration Summary

This migration was executed for the selected `gc-pitstop` slice:

- `CustomerManagementAPI` (customer read operations)
- Workshop ref-data access used by workshop flows (`WorkshopManagementAPI` customer/vehicle lookup)

Original architecture in this slice:

- REST endpoints in controllers
- HTTP-specific request/response handling
- Customer mirror access from workshop ref-data controller

Target architecture in this slice:

- Public service methods intended as the Graftcode-facing surface
- Reduced controller footprint (controller removal for migrated endpoints)
- Workshop ref-data customer reads no longer tied to workshop mirror repository

Overall result:

- Implemented a partial migration of the selected slice with measured infrastructure reduction.
- REST controllers for migrated endpoints were removed.
- Business data sources were preserved (SQL Server).
- One endpoint (`POST /api/customers`) was intentionally omitted because it depends on unsupported queue-driven publishing in this migration scope.

## 10.2 Removed Infrastructure

Major removed components in migrated scope:

- Controllers:
  - `src/CustomerManagementAPI/Controllers/CustomersController.cs` (removed)
  - `src/WorkshopManagementAPI/Controllers/RefDataController.cs` (removed)
- Controller-specific interface/route exposure for migrated methods
- Customer-side RabbitMQ-specific configuration from customer appsettings:
  - removed `RabbitMQPublisher` block from `src/CustomerManagementAPI/appsettings.Development.json`
- Obsolete customer command/event/mapping files tied to removed register endpoint:
  - `src/CustomerManagementAPI/Commands/RegisterCustomer.cs`
  - `src/CustomerManagementAPI/Events/CustomerRegistered.cs`
  - `src/CustomerManagementAPI/Mappers/Mappers.cs`
- Package cleanup in migrated scope:
  - removed Swagger package from `CustomerManagementAPI`
  - removed direct `Pitstop.Infrastructure.Messaging` package reference in `CustomerManagementAPI` and `WebApp` (kept where still required via project reference)

## 10.3 Graftcode Integration

Service-surface exposure in migrated slice:

- `src/CustomerManagementAPI/Services/CustomerManagementService.cs`
  - `GetCustomersAsync()`
  - `GetCustomerByIdAsync(string customerId)`
- `src/WorkshopManagementAPI/Services/WorkshopRefDataService.cs`
  - `GetCustomersAsync()`
  - `GetCustomerByIdAsync(string customerId)`
  - `GetVehiclesAsync()`
  - `GetVehicleByLicenseNumberAsync(string licenseNumber)`

Dependency injection and initialization changes:

- `CustomerManagementService` and `WorkshopRefDataService` remain available as service classes in source.
- DI registrations for these two wrappers are currently disabled in runtime startup.
- `WebApp` customer/vehicle read clients were adapted to SQL-backed calls for the migrated read path, preserving user-facing behavior for the selected slice.

Authentication and authorization:

- No explicit auth/authorization middleware existed in the migrated slice before migration.
- No weakening of existing security behavior was introduced in this slice.

Important limitation:

- The Azure DevOps reference repository specified for conventions was not accessible through MCP during migration.
- Local workspace conventions and existing project patterns were used.
- No verified runtime gateway exposure policy file for method filtering was found in this repository.

## 10.4 REST Endpoint Migration

| Original REST Endpoint | HTTP Method | Graftcode Service | Graftcode Method | Status |
| ---------------------- | ----------- | ----------------- | ---------------- | ------ |
| `/api/customers` | GET | `CustomerManagementService` | `GetCustomersAsync` | Migrated read path preserved |
| `/api/customers/{customerId}` | GET | `CustomerManagementService` | `GetCustomerByIdAsync` | Migrated read path preserved |
| `/api/customers` | POST | n/a | n/a | Omitted (unsupported queue-dependent path in selected scope) |
| `/api/refdata/customers` | GET | `WorkshopRefDataService` | `GetCustomersAsync` | Migrated read path preserved |
| `/api/refdata/customers/{customerId}` | GET | `WorkshopRefDataService` | `GetCustomerByIdAsync` | Migrated read path preserved |
| `/api/refdata/vehicles` | GET | `WorkshopRefDataService` | `GetVehiclesAsync` | Migrated read path preserved |
| `/api/refdata/vehicles/{licenseNumber}` | GET | `WorkshopRefDataService` | `GetVehicleByLicenseNumberAsync` | Migrated read path preserved |

## 10.5 Methods That Must Not Be Exposed Through Graftcode

Because automatic exposure behavior depends on runtime gateway configuration, the following methods should be explicitly filtered out (or made non-public where safe):

| Service/Class | Method | Reason for exclusion | Currently exposed | Recommended action |
| --- | --- | --- | --- | --- |
| `WorkshopPlanningController` | `GetByDate`, `GetMaintenanceJobAsync`, `PlanMaintenanceJobAsync`, `FinishMaintenanceJobAsync` | Not part of selected migrated service surface | Unknown | Add gateway exposure filter; keep REST-only path out of graft surface |
| `SqlServerRefDataRepository` | `GetVehiclesAsync`, `GetVehicleAsync` | Infrastructure repository methods, not API contract | Unknown | Filter by class/type allow-list |
| `CustomerManagementDBContext` | `MigrateDB` | Operational method, not business API | Unknown | Filter by namespace/type; do not expose DbContext methods |

## 10.6 Omitted Functionality and Unsupported Communication Mechanisms

| Original component | Communication mechanism | Original purpose | Removed functionality | Potential functional impact | Reason for omission |
| --- | --- | --- | --- | --- | --- |
| `CustomersController.RegisterAsync` | RabbitMQ publish via `IMessagePublisher` | Persist customer and emit `CustomerRegistered` event | Public register endpoint omitted from migrated slice | New customer registration through this path is unavailable in migrated slice | Selected scope excludes unsupported queue-dependent communication replacement |
| Workshop customer mirror synchronization path (`WorkshopManagementEventHandler` + mirror table flow) | RabbitMQ/event-driven synchronization | Populate workshop-side customer mirror | Not used by migrated customer lookup path | Workshop lookup now uses customer source directly; mirror freshness semantics no longer used in this slice | Unsupported communication mechanism intentionally not recreated with Graftcode in this migration |

## 10.7 Compatibility Notes

- Customer read behavior (`list`, `single`) is preserved for the selected slice.
- Workshop ref-data read behavior (`customers`, `vehicles`) is preserved for the selected slice.
- Customer registration (`POST /api/customers`) is intentionally omitted in this migration slice due to queue/event dependency.
- This is a scoped migration, not a full-solution transport migration.

## 10.8 Build and Test Results

Executed commands and outcomes:

- `dotnet restore src/pitstop.sln` -> **Failed** (external package source issues in environment, unrelated to code changes).
- Scoped restore/build (selected slice, with failed sources ignored):
  - `dotnet restore ... --ignore-failed-sources` for migrated projects -> **Succeeded**
  - `dotnet build src/CustomerManagementAPI/CustomerManagementAPI.csproj -c Release --no-restore` -> **Succeeded**
  - `dotnet build src/WorkshopManagementAPI/WorkshopManagementAPI.csproj -c Release --no-restore` -> **Succeeded**
  - `dotnet build src/WebApp/WebApp.csproj -c Release --no-restore` -> **Succeeded**
- Tests:
  - `dotnet test src/WorkshopManagement.UnitTests/WorkshopManagement.UnitTests.csproj -c Release --no-restore` -> **Succeeded**
  - Result: **25 passed, 0 failed, 0 skipped**

Known limitations:

- Full-solution restore/build remains blocked by package source configuration in this environment.
- The migration report is therefore validated against scoped migrated projects and affected test suite(s).

## 10.9 Code Reduction Metrics

### Measurement Method

Scope measured for migration KPI: selected migrated source projects only:

- Included directories:
  - `src/CustomerManagementAPI`
  - `src/WorkshopManagementAPI`
- Included extensions:
  - `.cs`
- Excluded:
  - `bin`, `obj`
  - `Migrations` generated files
  - `*.Designer.cs`, `*ModelSnapshot.cs`
  - tests, docs, third-party dependencies
- Counting method:
  - Non-empty source lines (blank lines excluded)
  - Same script and inclusion/exclusion rules used before and after

### Metrics

| Metric                             | Before | After | Reduction |
| ---------------------------------- | -----: | ----: | --------: |
| Application source LOC             |   1597 |  1416 |    11.33% |
| Number of controllers              |      3 |     1 |    66.67% |
| Number of application source files |     55 |    51 |     7.27% |
| REST-related dependencies          |      6 |     4 |    33.33% |

Additional counts:

- Number of migrated REST endpoints: **6**
- Number of exposed Graftcode methods: **6**
- Number of methods requiring exposure filtering: **7** (listed in section 10.5)
- Number of omitted unsupported features: **2**

### Reduction Attribution

1. Removed because REST infrastructure was replaced:
   - Controller classes for migrated endpoints
   - HTTP route/action wrappers for migrated operations
   - Swagger/HTTP scaffolding in the migrated customer path
2. Removed because unsupported functionality was omitted:
   - Queue/event-driven registration path (`POST /api/customers` event publish flow)
3. Added for migrated architecture:
   - Wrapper services for migrated read-path operations
   - Wiring/configuration required for read-path continuity

### REST Infrastructure Reduction

Using selected-scope controller and dependency metrics:

- Controller reduction: **66.67%**
- REST-related dependency reduction: **33.33%**

### Marketing Summary

Migrated the selected Pitstop slice from REST controllers to service-surface Graftcode-style exposure, reducing measured application source code by **11.33%** and controller count by **66.67%** while preserving supported customer/workshop read functionality.
