#!/usr/bin/env bash
# Compat: redirige al sync unificado del monorepo.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
exec "${ROOT}/scripts/sync-env.sh"
