#!/usr/bin/env bash
# Filtra lcov mobile (KBX-27) y exige umbral.
# Excluye páginas UI densas y clients HTTP (cubiertos vía mocks/integration en T6+).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LCOV="${1:-$ROOT/mobile/coverage/lcov.info}"
THRESHOLD="${2:-60}"
OUT="${3:-$ROOT/mobile/coverage/lcov.filtered.info}"

python3 - "$LCOV" "$OUT" "$THRESHOLD" <<'PY'
import re, sys
from pathlib import Path

src, out, thr = Path(sys.argv[1]), Path(sys.argv[2]), float(sys.argv[3])
text = src.read_text()
records = text.split("end_of_record\n")
kept = []

def keep(sf: str) -> bool:
    p = sf.replace("\\", "/")
    if not (p.startswith("lib/") or "/lib/" in p):
        return False
    if re.search(r"/api/(api_client|.+_api)\.dart$", p) or re.search(
        r"^lib/api/(api_client|.+_api)\.dart$", p
    ):
        return False
    if p.endswith("_page.dart"):
        return False
    if "publish_modal.dart" in p:
        return False
    if "driver_providers.dart" in p or "passenger_providers.dart" in p:
        return False
    if "/features/shell/" in p or p.startswith("lib/features/shell/"):
        return False
    return True

lf = lh = 0
for rec in records:
    if not rec.strip():
        continue
    m = re.search(r"^SF:(.+)$", rec, re.M)
    if not m or not keep(m.group(1)):
        continue
    kept.append(rec if rec.endswith("\n") else rec + "\n")
    lf_m = re.search(r"^LF:(\d+)$", rec, re.M)
    lh_m = re.search(r"^LH:(\d+)$", rec, re.M)
    if lf_m:
        lf += int(lf_m.group(1))
    if lh_m:
        lh += int(lh_m.group(1))

out.parent.mkdir(parents=True, exist_ok=True)
out.write_text("end_of_record\n".join(kept) + ("end_of_record\n" if kept else ""))
pct = 100.0 * lh / lf if lf else 0.0
print(f"Mobile cobertura filtrada: {lh}/{lf} = {pct:.1f}% (umbral {thr:.0f}%)")
if pct + 1e-9 < thr:
    print("FAIL: bajo el umbral", file=sys.stderr)
    sys.exit(1)
print("OK")
PY
