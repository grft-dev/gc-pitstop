# gc-pitstop hosted by GG (Docker)

This runbook exposes existing business service classes through a GG-backed Docker stack and validates them end-to-end from a separate .NET app.

## Exposed public surface

Modules served by GG:

- `Pitstop.WorkshopManagementAPI.dll`

Business class targeted by broad flow:

- `Pitstop.WorkshopManagementAPI.Services.WorkshopRefDataService`
  - `GetCustomersAsync`
  - `GetCustomerByIdAsync`
  - `GetVehiclesAsync`
  - `GetVehicleByLicenseNumberAsync`

## 1) Start Docker stack with GG

From `ports/gc-pitstop/src`:

```powershell
docker compose -f docker-compose.graft.yml up --build -d
```

Ports:

- `8090` Gateway/API (`/GV`, `/nuget`)
- `8091` Vision/diagnostics
- `1434` SQL Server (seeded with Customers/Vehicles)

WebSocket endpoint for clients:

- `ws://localhost:8090/ws`

Graft Vision:

- `http://localhost:8091`

## 2) Install generated client

After the container starts, open:

- `http://localhost:8090/nuget`

Copy the install command for the generated NuGet package and install it in your consumer app.

The stack intentionally runs **without** `GG_PROJECT_KEY`.

## Why old Docker CMD looked strange

The old `Pitstop.GraftHost/Dockerfile` command used:

- an `if` branch around `GG_PROJECT_KEY`
- the same GG method allow-list in both branches

That branch only toggled `--projectKey` for hosted publication. Local gateway runtime behavior for `WorkshopRefDataService` methods stayed the same. The current image removes this branch and always starts GG in the local mode used by our e2e flow.

## Troubleshooting

- If `dotnet add package ... --source <grft-url>` fails with `500 (Build failed: Determining projects to restore...)`, restart the host and use the newest URL printed in container logs.
- If port `8090` is occupied, change published ports in `src/docker-compose.graft.yml` and use the updated `ws://localhost:<port>/ws` endpoint in e2e config.
- Ensure restore in `src` uses repo-local `NuGet.Config` (only `nuget.org`) to avoid unstable global feeds.

## 3) Run broad e2e verifier

Use the dedicated root app:

```powershell
$env:GRAFTCODE_CONFIG_NUGET_PITSTOP_WORKSHOPMANAGEMENTAPI_<HASH_UPPER> = "name=graft.nuget.Pitstop.WorkshopManagementAPI_<hash>; runtime=netcore; modules=Pitstop.WorkshopManagementAPI.dll; host=ws://localhost:8090/ws; stateless=true"

dotnet run --project C:\Coding\Marketing\gc-pitstop-graft-broad-e2e\gc-pitstop-graft-broad-e2e.csproj -c Release -- `
  --assembly "C:\Users\<user>\.nuget\packages\graft.nuget.pitstop.workshopmanagementapi_<hash>\1.0.0\lib\net8.0\graft.nuget.Pitstop.WorkshopManagementAPI_<hash>.dll" `
  --host "ws://localhost:8090/ws" `
  --stateless true `
  --report "C:\Coding\Marketing\gc-pitstop-graft-broad-e2e\reports\pitstop-broad-e2e.json"
```

Important: do not pass business API DLLs as `--assembly`; use only GG-generated client assemblies.

## 4) Stop

```powershell
docker compose -f docker-compose.graft.yml down
```
