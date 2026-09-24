#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
app=IncidentCommander/app/IncidentCommander/IncidentCommander.csproj
npm --prefix IncidentCommander/frontend-node run lint
npm --prefix IncidentCommander/frontend-node run typecheck
npm --prefix IncidentCommander/frontend-node run build
dotnet build "$app" --configuration Release --warnaserror
dotnet format "$app" whitespace --verify-no-changes --no-restore
dotnet run --project IncidentCommander/checks/IncidentCommander.Checks.csproj --configuration Release
