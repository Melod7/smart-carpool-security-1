#!/usr/bin/env bash
# flutter run con API_URL + GOOGLE_MAPS_API_KEY desde .env raíz.
# Uso:
#   scripts/mobile-run.sh              # device por defecto / MOBILE_DEVICE
#   scripts/mobile-run.sh chrome
#   scripts/mobile-run.sh emulator-5554
#   DEVICE=iphone scripts/mobile-run.sh
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
# Compat Android gradle / código que aún lee MAPS_API_KEY
ARGS+=(--dart-define="MAPS_API_KEY=${KEY}")

echo "→ flutter ${ARGS[*]}"
echo "   API_URL=${API}"
echo "   GOOGLE_MAPS_API_KEY len=${#KEY}"
exec flutter "${ARGS[@]}"
