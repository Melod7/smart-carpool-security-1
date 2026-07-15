#!/usr/bin/env bash
# Copia GOOGLE_MAPS_API_KEY del .env raíz a:
#   - ios/Flutter/MapsSecrets.xcconfig  (SDK nativo iOS)
#   - web/maps_api_key.js               (Maps JavaScript API / Flutter web)
# Usar antes de `flutter run` en iOS o Chrome.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
ENV_FILE="${ROOT}/.env"
IOS_OUT="${ROOT}/mobile/ios/Flutter/MapsSecrets.xcconfig"
WEB_OUT="${ROOT}/mobile/web/maps_api_key.js"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "No existe ${ENV_FILE}. Copia .env.example → .env y pon GOOGLE_MAPS_API_KEY." >&2
  exit 1
fi

# shellcheck disable=SC1090
set -a
# Solo exportamos líneas KEY=VALUE (ignora comentarios / vacías)
# shellcheck disable=SC2046
eval "$(grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$ENV_FILE" | sed 's/\r$//')"
set +a

KEY="${GOOGLE_MAPS_API_KEY:-${MAPS_API_KEY:-}}"
if [[ -z "$KEY" ]]; then
  echo "GOOGLE_MAPS_API_KEY vacío en ${ENV_FILE}" >&2
  exit 1
fi

# Escape mínimo para string JS entre comillas simples.
KEY_JS="${KEY//\\/\\\\}"
KEY_JS="${KEY_JS//\'/\\\'}"

cat >"$IOS_OUT" <<EOF
// Generado por tool/sync_maps_key_from_env.sh — no commitear.
MAPS_API_KEY=${KEY}
EOF

cat >"$WEB_OUT" <<EOF
// Generado por tool/sync_maps_key_from_env.sh — no commitear.
window.__KUBIX_MAPS_API_KEY__ = '${KEY_JS}';
EOF

echo "OK → ${IOS_OUT} (MAPS_API_KEY len=${#KEY})"
echo "OK → ${WEB_OUT}"
