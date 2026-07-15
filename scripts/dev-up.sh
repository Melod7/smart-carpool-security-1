#!/usr/bin/env bash
# Levanta Postgres (Docker) + API (dotnet watch) + Web (Vite) en primer plano.
# Ctrl+C detiene API y Web; Postgres queda arriba (make down para pararlo).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/env.sh
source "${SCRIPT_DIR}/lib/env.sh"

ROOT="$(kubix_root)"
cd "$ROOT"

kubix_load_env
"${SCRIPT_DIR}/sync-env.sh"

mkdir -p "${ROOT}/.run"
echo $$ >"${ROOT}/.run/dev-up.pid"

cleanup() {
  echo ""
  echo "→ Deteniendo API y Web..."
  if [[ -f "${ROOT}/.run/api.pid" ]]; then
    kill "$(cat "${ROOT}/.run/api.pid")" 2>/dev/null || true
    rm -f "${ROOT}/.run/api.pid"
  fi
  if [[ -f "${ROOT}/.run/web.pid" ]]; then
    kill "$(cat "${ROOT}/.run/web.pid")" 2>/dev/null || true
    rm -f "${ROOT}/.run/web.pid"
  fi
  rm -f "${ROOT}/.run/dev-up.pid"
}
trap cleanup EXIT INT TERM

echo "→ Postgres..."
docker compose up -d postgres

echo "→ Esperando Postgres healthy..."
for _ in $(seq 1 30); do
  if docker compose exec -T postgres pg_isready -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

CS="$(kubix_connection_string)"

echo "→ API (dotnet watch) :${API_PORT}  [ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Development}]"
(
  cd "${ROOT}/backend"
  export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
  export ConnectionStrings__Default="$CS"
  export GoogleMaps__ApiKey="${GOOGLE_MAPS_API_KEY:-}"
  export Cors__WebOrigin="${WEB_ORIGIN}"
  export Cors__AllowedOrigins__0="${WEB_ORIGIN}"
  export Cors__AllowedOrigins__1="http://127.0.0.1:${WEB_PORT}"
  export Cors__AllowedOrigins__2="${FLUTTER_WEB_ORIGIN}"
  export Cors__AllowedOrigins__3="http://127.0.0.1:${FLUTTER_WEB_PORT}"
  export Database__MigrateOnStartup="${MIGRATE_ON_STARTUP}"
  export Database__SeedOnStartup="${SEED_ON_STARTUP}"
  export SUPER_ADMIN_EMAIL SUPER_ADMIN_PASSWORD
  export ASPNETCORE_URLS="http://0.0.0.0:${API_PORT}"
  exec dotnet watch run --project src/Kubix.Api --no-launch-profile
) > >(sed -u 's/^/[api] /') 2>&1 &
echo $! >"${ROOT}/.run/api.pid"

if [[ ! -d "${ROOT}/web/node_modules" ]]; then
  echo "→ npm install (web)..."
  (cd "${ROOT}/web" && npm install)
fi

echo "→ Web (Vite) :${WEB_PORT} (strict)"
(
  cd "${ROOT}/web"
  exec npm run dev -- --port "${WEB_PORT}" --strictPort --host
) > >(sed -u 's/^/[web] /') 2>&1 &
echo $! >"${ROOT}/.run/web.pid"

echo ""
echo "Listo (puertos fijos):"
echo "  API         ${API_URL}  (Swagger ${API_URL}/swagger)"
echo "  Web admin   ${WEB_ORIGIN}"
echo "  Flutter web http://localhost:${FLUTTER_WEB_PORT}  (make mobile-chrome)"
echo "  Maps key    len=${#GOOGLE_MAPS_API_KEY}"
echo ""
echo "Ctrl+C para detener API+Web. Postgres: make down"
echo ""

wait
