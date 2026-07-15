#!/usr/bin/env bash
# Genera archivos derivados desde el .env raíz (única fuente de verdad).
#   - web/.env                          (Vite: VITE_* desde API_URL / GOOGLE_MAPS_API_KEY)
#   - mobile/ios/Flutter/MapsSecrets.xcconfig
#   - mobile/web/maps_api_key.js
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/env.sh
source "${SCRIPT_DIR}/lib/env.sh"

ROOT="$(kubix_root)"
kubix_load_env

WEB_ENV="${ROOT}/web/.env"
IOS_OUT="${ROOT}/mobile/ios/Flutter/MapsSecrets.xcconfig"
FLUTTER_WEB_OUT="${ROOT}/mobile/web/maps_api_key.js"

mkdir -p "$(dirname "$IOS_OUT")" "$(dirname "$FLUTTER_WEB_OUT")"

# --- Admin web (Vite solo lee VITE_*) ---
cat >"$WEB_ENV" <<EOF
# Generado por scripts/sync-env.sh — no editar a mano.
# Fuente: ${ROOT}/.env  →  make sync-env
VITE_API_URL=${API_URL}
VITE_GOOGLE_MAPS_API_KEY=${GOOGLE_MAPS_API_KEY:-}
EOF
echo "OK → ${WEB_ENV}"

# --- Maps mobile (iOS nativo + Flutter web JS) ---
KEY="${GOOGLE_MAPS_API_KEY:-}"
if [[ -z "$KEY" ]]; then
  echo "AVISO: GOOGLE_MAPS_API_KEY vacío — mapas / Directions no funcionarán." >&2
  # Igual escribimos stubs para no romper includes.
  cat >"$IOS_OUT" <<EOF
// Generado por scripts/sync-env.sh — no commitear.
MAPS_API_KEY=
EOF
  cat >"$FLUTTER_WEB_OUT" <<EOF
// Generado por scripts/sync-env.sh — no commitear.
window.__KUBIX_MAPS_API_KEY__ = '';
EOF
else
  KEY_JS="${KEY//\\/\\\\}"
  KEY_JS="${KEY_JS//\'/\\\'}"
  cat >"$IOS_OUT" <<EOF
// Generado por scripts/sync-env.sh — no commitear.
MAPS_API_KEY=${KEY}
EOF
  cat >"$FLUTTER_WEB_OUT" <<EOF
// Generado por scripts/sync-env.sh — no commitear.
window.__KUBIX_MAPS_API_KEY__ = '${KEY_JS}';
EOF
  echo "OK → ${IOS_OUT} (len=${#KEY})"
  echo "OK → ${FLUTTER_WEB_OUT}"
fi
