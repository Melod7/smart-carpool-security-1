#!/usr/bin/env bash
# Helpers para cargar el .env raíz de Kubix.
# Uso: source "$(dirname "$0")/lib/env.sh"  ó  source scripts/lib/env.sh

# Directorio de este archivo (robusto en bash; make siempre usa bash).
_kubix_lib_dir() {
  local src="${BASH_SOURCE[0]:-}"
  if [[ -z "$src" ]]; then
    src="$0"
  fi
  cd "$(dirname "$src")" && pwd
}

kubix_root() {
  # scripts/lib → raíz del monorepo
  local lib
  lib="$(_kubix_lib_dir)"
  cd "${lib}/../.." && pwd
}

# Carga KEY=VALUE del .env raíz (ignora comentarios / vacías). Exporta a entorno.
kubix_load_env() {
  local root env_file
  root="$(kubix_root)"
  env_file="${KUBIX_ENV_FILE:-$root/.env}"

  if [[ ! -f "$env_file" ]]; then
    echo "No existe ${env_file}. Corre: cp .env.example .env" >&2
    return 1
  fi

  set -a
  # shellcheck disable=SC2046
  eval "$(grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$env_file" | sed 's/\r$//')"
  set +a

  # Defaults
  : "${POSTGRES_DB:=kubix}"
  : "${POSTGRES_USER:=kubix}"
  : "${POSTGRES_PASSWORD:=kubix}"
  : "${POSTGRES_PORT:=55432}"
  : "${API_PORT:=8080}"
  : "${API_URL:=http://localhost:${API_PORT}}"
  : "${WEB_PORT:=5173}"
  : "${WEB_ORIGIN:=http://localhost:${WEB_PORT}}"
  : "${FLUTTER_WEB_PORT:=5055}"
  : "${FLUTTER_WEB_ORIGIN:=http://localhost:${FLUTTER_WEB_PORT}}"
  : "${MIGRATE_ON_STARTUP:=true}"
  : "${SEED_ON_STARTUP:=true}"

  # Compat: nombres viejos → canónicos
  if [[ -z "${GOOGLE_MAPS_API_KEY:-}" && -n "${MAPS_API_KEY:-}" ]]; then
    GOOGLE_MAPS_API_KEY="$MAPS_API_KEY"
  fi
  if [[ -z "${GOOGLE_MAPS_API_KEY:-}" && -n "${VITE_GOOGLE_MAPS_API_KEY:-}" ]]; then
    GOOGLE_MAPS_API_KEY="$VITE_GOOGLE_MAPS_API_KEY"
  fi
  if [[ -z "${API_URL:-}" && -n "${VITE_API_URL:-}" ]]; then
    API_URL="$VITE_API_URL"
  fi

  export POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD POSTGRES_PORT
  export API_PORT API_URL WEB_PORT WEB_ORIGIN
  export FLUTTER_WEB_PORT FLUTTER_WEB_ORIGIN
  export MIGRATE_ON_STARTUP SEED_ON_STARTUP
  export GOOGLE_MAPS_API_KEY
  export MOBILE_API_URL MOBILE_DEVICE
  export SUPER_ADMIN_EMAIL SUPER_ADMIN_PASSWORD
  export PGADMIN_EMAIL PGADMIN_PASSWORD PGADMIN_PORT
  export WEB_ORIGIN
  export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
}

# Connection string host → Postgres Docker publicado.
kubix_connection_string() {
  printf 'Host=localhost;Port=%s;Database=%s;Username=%s;Password=%s' \
    "${POSTGRES_PORT}" "${POSTGRES_DB}" "${POSTGRES_USER}" "${POSTGRES_PASSWORD}"
}

# IP LAN del Mac (en0/en1) o vacío.
kubix_lan_ip() {
  ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || true
}

# Resuelve URL de la API para un device Flutter.
# Args: device_id_or_name (opcional)
kubix_mobile_api_url() {
  local device="${1:-${MOBILE_DEVICE:-}}"
  if [[ -n "${MOBILE_API_URL:-}" ]]; then
    echo "$MOBILE_API_URL"
    return 0
  fi

  local lower
  lower="$(echo "$device" | tr '[:upper:]' '[:lower:]')"

  # Emulador Android
  if [[ "$lower" == *emulator* || "$lower" == *gphone* ]]; then
    echo "http://10.0.2.2:${API_PORT}"
    return 0
  fi

  # Chrome / desktop / simulador iOS
  if [[ "$lower" == chrome || "$lower" == *web-javascript* || "$lower" == macos || "$lower" == *simulator* ]]; then
    echo "http://127.0.0.1:${API_PORT}"
    return 0
  fi

  # Dispositivo físico (iOS/Android) → IP LAN del Mac
  local lan
  lan="$(kubix_lan_ip)"
  if [[ -n "$lan" ]]; then
    echo "http://${lan}:${API_PORT}"
  else
    echo "http://127.0.0.1:${API_PORT}"
  fi
}
