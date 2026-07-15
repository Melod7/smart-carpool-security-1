# Kubix mobile

App Flutter para conductores y pasajeros.

## Arranque recomendado (monorepo)

Desde la **raíz** del repo (no desde `mobile/`):

```bash
cp .env.example .env          # una vez
# Edita GOOGLE_MAPS_API_KEY (y MOBILE_API_URL si el auto-detect falla)

make sync-env                 # Maps iOS + Flutter web + web/.env
make up                       # API + web en otra terminal, o ya corriendo
make mobile DEVICE=chrome
make mobile DEVICE=emulator-5554
make mobile DEVICE=<udid-iphone>
```

Variables relevantes en el `.env` **raíz**:

| Variable | Rol |
|---|---|
| `GOOGLE_MAPS_API_KEY` | Maps SDK + dart-define (única clave) |
| `MOBILE_API_URL` | Override; si vacío, se deduce por device |
| `MOBILE_DEVICE` | Default para `make mobile` |
| `API_PORT` | Puerto de la API (default 8080) |

URL automática si `MOBILE_API_URL` está vacío:

| Device | URL |
|---|---|
| Emulador Android (`*emulator*`, `*gphone*`) | `http://10.0.2.2:8080` |
| Chrome / simulador iOS / macOS | `http://127.0.0.1:8080` |
| iPhone/Android físico | `http://<IP-LAN-Mac>:8080` |

## Requisitos

- Flutter 3.x (`flutter doctor`)
- API local (`make api` o `make up`)
- iOS: Xcode + CocoaPods; Android: SDK/emulador
- Deployment target iOS **14.0**

```bash
cd mobile && flutter pub get
```

## Manual (sin Make)

```bash
# desde la raíz
./scripts/sync-env.sh
set -a && source .env && set +a
cd mobile
flutter run -d chrome \
  --dart-define=API_URL=http://127.0.0.1:8080 \
  --dart-define=GOOGLE_MAPS_API_KEY="$GOOGLE_MAPS_API_KEY"
```

`./tool/sync_maps_key_from_env.sh` es un alias de `scripts/sync-env.sh`.

## IDs de dispositivo

```bash
flutter devices
```

> **CORS (Chrome):** Flutter web usa un puerto efímero; en Development la API acepta `localhost` / `127.0.0.1`.
