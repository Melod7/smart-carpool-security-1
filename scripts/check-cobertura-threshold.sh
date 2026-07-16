#!/usr/bin/env bash
# Verifica umbrales de cobertura Cobertura XML (KBX-27).
# Uso: check-cobertura-threshold.sh <archivo.xml> <umbral%> [filtro_paquete_regex]
set -euo pipefail

FILE="${1:?falta cobertura.xml}"
THRESHOLD="${2:?falta umbral}"
FILTER="${3:-.}"

python3 - "$FILE" "$THRESHOLD" "$FILTER" <<'PY'
import re, sys, xml.etree.ElementTree as ET

path, threshold_s, filt = sys.argv[1], float(sys.argv[2]), sys.argv[3]
root = ET.parse(path).getroot()
pat = re.compile(filt)

covered = valid = 0
for pkg in root.findall(".//package"):
    name = pkg.get("name") or ""
    if not pat.search(name):
        continue
    for cls in pkg.findall(".//class"):
        for line in cls.findall(".//line"):
            valid += 1
            if int(line.get("hits", "0")) > 0:
                covered += 1

if valid == 0:
    print(f"ERROR: no hay líneas para filtro {filt!r} en {path}", file=sys.stderr)
    sys.exit(2)

pct = 100.0 * covered / valid
print(f"Cobertura {filt}: {covered}/{valid} = {pct:.1f}% (umbral {threshold_s:.0f}%)")
if pct + 1e-9 < threshold_s:
    print("FAIL: bajo el umbral", file=sys.stderr)
    sys.exit(1)
print("OK")
PY
