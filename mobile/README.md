# Kubix mobile

App Flutter para conductores y pasajeros.

## Funciones actuales

- Registro obligatorio con universidad/campus, carrera UTN searchable, cédula,
  género e imagen de perfil.
- Conductores registran vehículo e imagen obligatoria.
- Publicación de rutas por waypoints, preview Google Directions, punto de
  espera sugerido, tracking GPS y SOS.
- Matching de solicitudes por mismo género.
- Passenger solicita cambios de perfil al coordinador; puede solicitar modo
  driver con vehículo. Driver puede pasar directamente a modo passenger.
- Imágenes se comprimen y envían como data URL validada (máximo 2 MiB).

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
| `API_PORT` | Puerto de la API (fijo **8080**) |
| `FLUTTER_WEB_PORT` | Flutter Chrome (fijo **5055**) |

URL automática si `MOBILE_API_URL` está vacío:

| Device | URL |
|---|---|
| Emulador Android (`*emulator*`, `*gphone*`) | `http://10.0.2.2:8080` |
| Chrome / simulador iOS / macOS | `http://localhost:8080` (no uses `127.0.0.1`) |
| iPhone/Android físico | `http://<IP-LAN-Mac>:8080` |

## Requisitos

- Flutter 3.x (`flutter doctor`)
- API local (`make api` o `make up`)
- iOS: Xcode + CocoaPods; Android: SDK/emulador
- Deployment target iOS **14.0**

```bash
cd mobile && flutter pub get
```

## iOS

Para desarrollo en el simulador:

```bash
open -a Simulator
flutter devices
make mobile-ios DEVICE="iPhone 16 Pro"
```

La instalación en un iPhone requiere firma de Apple y seleccionar el equipo de
desarrollo en Xcode. La guía completa para simulador, dispositivo físico y
TestFlight está en [Guía de instalación en iOS](IOS_INSTALL.md).

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

## Builds de distribución

El workflow `.github/workflows/mobile-builds.yml` recibe la URL pública de la
API y `GOOGLE_MAPS_API_KEY`:

- `kubix-android-apk`: APK release firmado con keystore estable.
- `kubix-ios-simulator-app`: ZIP ejecutable en iOS Simulator.

La URL desplegada es <https://d2dgmlbp00gdhh.cloudfront.net>. Un iPhone físico
requiere certificado/provisioning Apple o TestFlight; el ZIP de Simulator no se
instala en hardware físico.

Para instalar el ZIP en un simulador, ejecutar desde Xcode en un iPhone o
distribuir mediante TestFlight, consulta la
[Guía de instalación en iOS](IOS_INSTALL.md).
