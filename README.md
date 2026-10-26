# Pitstop - Garage Management System

This repository is a fork of [Edwin van Wijk's Pitstop](https://github.com/EdwinVW/pitstop). It exists only to show Graftcode in use: customer and workshop operations are hosted as grafts on Graftcode Gateway and checked with `gc-pitstop-graft-broad-e2e`.

The graft stack does not publish or consume RabbitMQ. Queue workers (`Invoice`, `Notification`, `Auditlog`, `TimeService`, `WorkshopManagementEventHandler`) remain in the tree and are not started by `docker-compose.graft.yml`. Customer and workshop writes go straight to SQL Server. Examples that migrate the RabbitMQ path are planned.

The upstream project is a sample garage-management system for a fictitious repair shop. It demonstrates microservices, CQRS, event-driven design, event sourcing, domain-driven design, and eventual consistency, together with Docker, Kubernetes, Istio, and Linkerd. Background on that design is in the [Pitstop wiki](https://github.com/EdwinVW/pitstop/wiki) and in the [arc42 documentation](docs/arc42/arc42.md) kept in this fork.

![](pitstop-garage.png)

> This is an actual garage somewhere in Dresden Germany. Thanks to Thomas Moerkerken for the picture!

## Exposed public surface

Modules:

- `Pitstop.WorkshopManagementAPI.dll`
- `Pitstop.CustomerManagementAPI.dll` (class library, no `Program.cs`)
- `Pitstop.VehicleManagementAPI.dll` (class library, no `Program.cs`)

Types and methods (names have no `Async` suffix). These are the original operation types, with the HTTP host removed:

- `Pitstop.WorkshopManagementAPI.Controllers.RefDataController`
  - `GetCustomers`, `GetCustomerById`, `GetVehicles`, `GetVehicleByLicenseNumber`
- `Pitstop.WorkshopManagementAPI.Controllers.WorkshopPlanningController`
  - `RegisterPlanning`, `GetPlanningByDate`, `GetMaintenanceJob`
- `Pitstop.WorkshopManagementAPI.CommandHandlers.PlanMaintenanceJobCommandHandler`
  - `PlanMaintenanceJob`
- `Pitstop.WorkshopManagementAPI.CommandHandlers.FinishMaintenanceJobCommandHandler`
  - `FinishMaintenanceJob`
- `Pitstop.Application.CustomerManagementAPI.Controllers.CustomersController`
  - `RegisterCustomer`, `GetCustomers`, `GetCustomerById` (full customer card)
- `Pitstop.VehicleManagement.Controllers.VehiclesController`
  - `RegisterVehicle`

Customer and vehicle ref-data reads stay on `SqlServerRefDataRepository`. Planning and job rows stay on `SqlServerWorkshopPlanningEventSourceRepository`. Job invariants stay on `WorkshopPlanningRules` and `MaintenanceJobRules`. `RegisterCustomer` writes through `CustomerManagementDBContext`. `RegisterVehicle` writes the workshop `Vehicle` table through `VehicleManagementDBContext`, because the event-handler projection is not running. The runtime image is `mcr.microsoft.com/dotnet/runtime:10.0`. The host replaces the platform stub of `Microsoft.Data.SqlClient` with the unix implementation, because GG loads the flat DLL.

This demo starts the stack without `GG_PROJECT_KEY`.

## 1) Start Docker stack with GG

From `ports/gc-pitstop/src`:

```powershell
docker compose -f docker-compose.graft.yml up --build -d
```

Ports:

- `8090` Gateway (`/nuget`, WebSocket)
- `8091` Graft Vision
- `1434` SQL Server (seeded customers, vehicles, and the workshop tables)

WebSocket endpoint for clients:

- `ws://localhost:8090/ws`

Graft Vision:

- `http://localhost:8091`

## 2) Install generated clients

After the container logs `dotnet add package`, open `http://localhost:8090/nuget` and use the package ids printed there.

The ids below are an example from one generation. Without a `GG_PROJECT_KEY` linked to your Graftcode account, every generation gets a new suffix. With that key the id stays stable.

- `graft.nuget.pitstop.workshopmanagementapi_xr2q3u` 1.0.0
- `graft.nuget.pitstop.customermanagementapi_xr2q3u` 1.0.0
- `graft.nuget.pitstop.vehiclemanagementapi_xr2q3u` 1.0.0

NuGet will not replace an existing `1.0.0` when the package id stays the same. When the hash changes, delete the old package folder and restore from the feed printed in the container log. When the hash stays and the host surface changed, delete the cached `1.0.0` and restore from that same feed.

Runtime config:

```powershell
$env:GRAFTCODE_CONFIG_NUGET_PITSTOP_WORKSHOPMANAGEMENTAPI = "name=graft.nuget.Pitstop.WorkshopManagementAPI; runtime=netcore; modules=Pitstop.WorkshopManagementAPI.dll; host=ws://localhost:8090/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_PITSTOP_CUSTOMERMANAGEMENTAPI = "name=graft.nuget.Pitstop.CustomerManagementAPI; runtime=netcore; modules=Pitstop.CustomerManagementAPI.dll; host=ws://localhost:8090/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_PITSTOP_VEHICLEMANAGEMENTAPI = "name=graft.nuget.Pitstop.VehicleManagementAPI; runtime=netcore; modules=Pitstop.VehicleManagementAPI.dll; host=ws://localhost:8090/ws; stateless=true"
```

## 3) Run the broad e2e verifier

```powershell
dotnet run --project C:\Coding\Marketing\gc-pitstop-graft-broad-e2e\gc-pitstop-graft-broad-e2e.csproj -c Release -- `
  --host "ws://localhost:8090/ws" `
  --stateless true `
  --report "C:\Coding\Marketing\gc-pitstop-graft-broad-e2e\reports\pitstop-broad-e2e.json"
```

`--assembly` is ignored. The runner uses the PackageReferences checked into its project file. Those ids are one generation; replace them when `/nuget` prints a new suffix.

Exit codes: `0` all checks passed, `1` an assertion failed, `2` the run was blocked before assertions.

## 4) Stop

```powershell
docker compose -f docker-compose.graft.yml down
```

## Troubleshooting

- `dotnet add package` takes its source from the container log. There is no fixed feed URL. Without a project key, each start prints a new `https://grft.dev/<guid>__free`. If restore fails, restart the host and use the URL from that log.
- If port `8090` is occupied, change the published ports in `src/docker-compose.graft.yml` and point the client host at the new WebSocket URL.
- Restore under `src` uses the repo-local `NuGet.Config` (nuget.org only).
