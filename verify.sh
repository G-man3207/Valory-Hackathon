#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
shellcheck --severity=style verify.sh lab/lab.sh
python3 -W error -c 'from pathlib import Path; compile(Path("lab/server.py").read_text(), "lab/server.py", "exec")'
node --check dev.mjs
node IncidentCommander/frontend-node/node_modules/eslint/bin/eslint.js \
  --config IncidentCommander/frontend-node/eslint.config.js dev.mjs --max-warnings=0
app=IncidentCommander/app/IncidentCommander/IncidentCommander.csproj
npm --prefix IncidentCommander/frontend-node run lint
npm --prefix IncidentCommander/frontend-node run typecheck
npm --prefix IncidentCommander/frontend-node run build
dotnet build "$app" --configuration Release --warnaserror
dotnet csharpier check IncidentCommander/app/IncidentCommander/*.cs IncidentCommander/checks/*.cs
dotnet run --project IncidentCommander/checks/IncidentCommander.Checks.csproj --configuration Release
