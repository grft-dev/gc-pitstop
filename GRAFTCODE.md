# gc-pitstop hosted by GG (Docker)

This runbook publishes `gc-pitstop` as a GG-hosted backend module and validates it end-to-end from a separate .NET app.

## Exposed public surface

Module: `Pitstop.GraftHost.dll`

## 1) Start Docker stack with GG

From `ports/gc-pitstop/src`:

```powershell
# Optional for publishing to hosted GG:
# $env:GG_PROJECT_KEY = "username:token"
docker compose -f docker-compose.graft.yml up --build -d
```

Ports:

- `8090` Gateway/API (`/GV`, `/nuget`)
- `8091` Vision/diagnostics

WebSocket endpoint for clients:

- `ws://localhost:8090/ws`

## 2) Install generated client

After the container starts, open:

- `http://localhost:8090/nuget`

Copy the install command for the generated NuGet package and install it in your consumer app.

If `GG_PROJECT_KEY` is missing, GG still starts in local mode, but hosted package publication is not available.

## Troubleshooting

- If `dotnet add package ... --source <grft-url>` fails with `500 (Build failed: Determining projects to restore...)`, restart the host and use the newest URL printed in container logs.
- If port `8090` is occupied, change published ports in `src/docker-compose.graft.yml` and use the updated `ws://localhost:<port>/ws` endpoint in e2e config.

## 3) Run broad e2e verifier

Use the dedicated root app:

```powershell
dotnet run --project C:\Coding\Marketing\gc-pitstop-graft-broad-e2e\gc-pitstop-graft-broad-e2e.csproj -c Release -- `
  --assembly "C:\path\to\installed\graft-client.dll" `
  --host "ws://localhost:8090/ws" `
  --stateless true `
  --report "C:\Coding\Marketing\gc-pitstop-graft-broad-e2e\reports\pitstop-broad-e2e.json"
```

## 4) Stop

```powershell
docker compose -f docker-compose.graft.yml down
```
