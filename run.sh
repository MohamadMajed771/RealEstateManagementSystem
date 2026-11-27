#!/usr/bin/env bash
# Starts the project with one command (macOS / Linux):
#   ./run.sh               both websites in this terminal
#   ./run.sh marketplace   only System 1 (Marketplace)
#   ./run.sh transfer      only System 2 (Transfer Service)
# With one system per terminal you can stop and restart one of them alone
# (the RabbitMQ demo). Start the second terminal after the first says Ready.
# It starts SQL Server + RabbitMQ (Docker), builds, runs the website(s)
# and opens them in the browser. Press Ctrl+C once to stop.
# (Docker keeps running; stop it with: docker compose stop)

set -euo pipefail
cd "$(dirname "$0")"
export PATH="$HOME/.orbstack/bin:/usr/local/bin:/opt/homebrew/bin:$PATH"

say() { printf '\033[1;34m==> %s\033[0m\n' "$*"; }
fail() { printf '\033[1;31mError: %s\033[0m\n' "$*" >&2; exit 1; }

case "${1:-both}" in
  both)        S1=1; S2=1 ;;
  marketplace) S1=1; S2="" ;;
  transfer)    S1="";  S2=1 ;;
  *) fail "Unknown option '$1'. Use: ./run.sh, ./run.sh marketplace or ./run.sh transfer" ;;
esac

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
# docker compose reads MSSQL_SA_PASSWORD from .env too, so read it the same way
if [ -z "${MSSQL_SA_PASSWORD:-}" ] && [ -f .env ]; then
  MSSQL_SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' .env | tail -n 1 | cut -d= -f2- | tr -d "\"'" || true)"
fi
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

# 4. Build one app after the other (building both at the same time can clash on SharedContracts)
say "Building..."
if [ -n "$S1" ]; then dotnet build RealEstateMarketplace -nologo -v q; fi
if [ -n "$S2" ]; then dotnet build PropertyTransferManagement -nologo -v q; fi

# 5. Run both websites; their output is shown with a [Marketplace] / [Transfer] prefix
# Stop only what this script started (never the terminal or a parent script)
stop_children() {
  local runs; runs="$(pgrep -P $$ -f "dotnet run" || true)"
  for r in $runs; do pkill -TERM -P "$r" 2>/dev/null || true; done   # the two websites
  [ -n "$runs" ] && kill -TERM $runs 2>/dev/null || true             # dotnet run (the output filters end by themselves)
}
trap 'echo; say "Stopping..."; stop_children; wait 2>/dev/null || true; exit 0' INT TERM
trap 'stop_children' EXIT

prefix() { awk -v p="$1" '{ print p " " $0; fflush() }'; }

if [ -n "$S1" ]; then
  say "Starting System 1 (Marketplace)..."
  dotnet run --no-build --project RealEstateMarketplace --launch-profile https 2>&1 | prefix $'\033[35m[Marketplace]\033[0m' &
fi
if [ -n "$S2" ]; then
  say "Starting System 2 (Transfer Service)..."
  dotnet run --no-build --project PropertyTransferManagement --launch-profile https 2>&1 | prefix $'\033[36m[Transfer]\033[0m' &
fi

listening() {
  { [ -z "$S1" ] || nc -z localhost 7287 >/dev/null 2>&1; } &&
  { [ -z "$S2" ] || nc -z localhost 7041 >/dev/null 2>&1; }
}

# 6. Open the browser once the website(s) are listening
(
  seen=0
  for i in $(seq 1 120); do
    # Give up once the websites stopped (checked only after they were seen
    # or after a few seconds, so a slow start is not mistaken for a stop)
    if pgrep -P $$ -f "dotnet run" >/dev/null; then seen=1
    elif [ "$seen" = 1 ] || [ "$i" -gt 5 ]; then exit 0; fi
    if listening; then
      printf '\n\033[1;32m==> Ready!\n'
      if [ -n "$S1" ]; then printf '    Marketplace:       https://localhost:7287   (health: /health)\n'; fi
      if [ -n "$S2" ]; then printf '    Transfer Service:  https://localhost:7041   (health: /health)\n'; fi
      printf '    RabbitMQ:          http://localhost:15672 (guest / guest)\n    Email codes appear below in this terminal (search: DEV EMAIL).\n    Press Ctrl+C to stop.\033[0m\n\n'
      if [ -z "${NO_OPEN:-}" ] && command -v open >/dev/null; then
        if [ -n "$S1" ]; then open https://localhost:7287; fi
        if [ -n "$S2" ]; then open https://localhost:7041; fi
      fi
      exit 0
    fi
    sleep 1
  done
) &

wait
