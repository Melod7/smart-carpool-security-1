#!/usr/bin/env bash
# flutter run con API_URL + GOOGLE_MAPS_API_KEY desde .env raíz.
# Flutter web usa puerto FIJO (FLUTTER_WEB_PORT, default 5055) para CORS estable.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/env.sh
source "${SCRIPT_DIR}/lib/env.sh"

ROOT="$(kubix_root)"
kubix_load_env
"${SCRIPT_DIR}/sync-env.sh"

DEVICE="${1:-${MOBILE_DEVICE:-${DEVICE:-}}}"
API="$(kubix_mobile_api_url "$DEVICE")"
KEY="${GOOGLE_MAPS_API_KEY:-}"
WEB_PORT_FIXED="${FLUTTER_WEB_PORT:-5055}"

cd "${ROOT}/mobile"

if [[ ! -d .dart_tool ]]; then
  flutter pub get
fi

ARGS=(run)
if [[ -n "$DEVICE" ]]; then
  ARGS+=(-d "$DEVICE")
fi
ARGS+=(--dart-define="API_URL=${API}")
ARGS+=(--dart-define="GOOGLE_MAPS_API_KEY=${KEY}")
ARGS+=(--dart-define="MAPS_API_KEY=${KEY}")

# Flutter web / Chrome: hostname + puerto fijos (evita CORS por puerto efímero).
lower="$(echo "${DEVICE:-}" | tr '[:upper:]' '[:lower:]')"
if [[ -z "$DEVICE" || "$lower" == chrome || "$lower" == *web* ]]; then
  ARGS+=(--web-hostname=localhost)
  ARGS+=(--web-port="${WEB_PORT_FIXED}")
fi

echo "→ flutter ${ARGS[*]}"
echo "   API_URL=${API}"
echo "   GOOGLE_MAPS_API_KEY len=${#KEY}"
if [[ -z "$DEVICE" || "$lower" == chrome || "$lower" == *web* ]]; then
  echo "   Flutter web → http://localhost:${WEB_PORT_FIXED}"
fi
exec flutter "${ARGS[@]}"
