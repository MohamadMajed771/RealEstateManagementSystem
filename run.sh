#!/usr/bin/env bash
# Starts the whole project with one command (macOS / Linux):
#   ./run.sh
# It starts SQL Server + RabbitMQ (Docker), builds, runs both websites
# and opens them in the browser. Press Ctrl+C once to stop both websites.
# (Docker keeps running; stop it with: docker compose stop)

set -euo pipefail
cd "$(dirname "$0")"
export PATH="$HOME/.orbstack/bin:/usr/local/bin:/opt/homebrew/bin:$PATH"

say() { printf '\033[1;34m==> %s\033[0m\n' "$*"; }
fail() { printf '\033[1;31mError: %s\033[0m\n' "$*" >&2; exit 1; }

command -v dotnet >/dev/null || fail ".NET SDK not found. Install .NET 10 from https://dotnet.microsoft.com/download"
command -v docker >/dev/null || fail "docker not found. Install/open OrbStack (Mac) or Docker Desktop."

# 1. Private settings files (copied from the examples the first time)
for app in RealEstateMarketplace PropertyTransferManagement; do
  if [ ! -f "$app/appsettings.Development.json" ]; then
    cp "$app/appsettings.Development.example.json" "$app/appsettings.Development.json"
    say "Created $app/appsettings.Development.json from the example"
  fi
done

# 2. Docker engine
if ! docker info >/dev/null 2>&1; then
  say "Starting Docker..."
  if [ -d /Applications/OrbStack.app ]; then open -a OrbStack; elif [ -d /Applications/Docker.app ]; then open -a Docker; fi
  for _ in $(seq 1 60); do docker info >/dev/null 2>&1 && break; sleep 2; done
  docker info >/dev/null 2>&1 || fail "Docker did not start. Open OrbStack / Docker Desktop and run ./run.sh again."
fi

# 3. SQL Server + RabbitMQ
say "Starting SQL Server and RabbitMQ..."
docker compose up -d

say "Waiting for SQL Server to accept connections..."
SA_PASSWORD="${MSSQL_SA_PASSWORD:-RealEstate_Dev_2026!}"
ready=0
for _ in $(seq 1 60); do
  if docker exec realestate-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 2
done
[ "$ready" = 1 ] || say "SQL Server is slow to start; continuing anyway (the apps will report it if it is not ready)."

for _ in $(seq 1 30); do nc -z localhost 5672 >/dev/null 2>&1 && break; sleep 1; done

# 4. Build once (building both apps at the same time can clash on SharedContracts)
say "Building..."
dotnet build AdvancedProgrammingProject.slnx -nologo -v q

# 5. Run both websites; their output is shown with a [Marketplace] / [Transfer] prefix
trap 'echo; say "Stopping both websites..."; kill 0 2>/dev/null; exit 0' INT TERM
trap 'kill 0 2>/dev/null' EXIT

prefix() { awk -v p="$1" '{ print p " " $0; fflush() }'; }

say "Starting System 1 (Marketplace) and System 2 (Transfer Service)..."
dotnet run --no-build --project RealEstateMarketplace --launch-profile https 2>&1 | prefix $'\033[35m[Marketplace]\033[0m' &
dotnet run --no-build --project PropertyTransferManagement --launch-profile https 2>&1 | prefix $'\033[36m[Transfer]\033[0m' &

# 6. Open the browser once both are listening
(
  for _ in $(seq 1 120); do
    if nc -z localhost 7287 >/dev/null 2>&1 && nc -z localhost 7041 >/dev/null 2>&1; then
      printf '\n\033[1;32m==> Ready!\n    Marketplace:       https://localhost:7287\n    Transfer Service:  https://localhost:7041\n    RabbitMQ:          http://localhost:15672 (guest / guest)\n    Email codes appear below in this terminal (search: DEV EMAIL).\n    Press Ctrl+C to stop.\033[0m\n\n'
      [ -z "${NO_OPEN:-}" ] && command -v open >/dev/null && { open https://localhost:7287; open https://localhost:7041; }
      exit 0
    fi
    sleep 1
  done
) &

wait
