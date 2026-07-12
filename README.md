# Kubix UTN — Smart Carpool Security

Plataforma multi-tenant de seguridad para carpooling (super_admin → universidades → campus → coordinador / driver / passenger).

| App | Stack | Path |
|---|---|---|
| API | .NET 10 Web API + EF Core + PostgreSQL | `backend/` |
| Admin web | React + TypeScript + Vite + TanStack Query | `web/` |
| Mobile | Flutter (conductores y pasajeros) | `mobile/` |

Ver [PLAN.md](./PLAN.md) y [STATUS.md](./STATUS.md).

## Requisitos previos

- Docker Desktop
- Node.js 20+
- .NET SDK 10
- Flutter 3.x (solo mobile — no hace falta para smoke local de API/web)

## Inicio rápido (local)

```bash
cp .env.example .env

# Postgres + API (el puerto host de Postgres por defecto es 55432 para evitar conflictos locales)
docker compose up -d --build

# Health
curl http://localhost:8080/health
# → {"status":"healthy","database":"up",...}

# Swagger
open http://localhost:8080/swagger

# Web (otra terminal)
cd web && npm install && npm run dev
# → http://localhost:5173

# API contra Postgres local desde el host (no en Docker)
# La connection string usa localhost:55432 (ver appsettings / .env)
```

pgAdmin opcional:

```bash
docker compose --profile tools up -d
# → http://localhost:5050  (admin@kubix.local / admin)
```

### Ejecutar la API sin Docker (Postgres sigue por Compose)

```bash
docker compose up -d postgres
cd backend
dotnet run --project src/Kubix.Api
```

### Mobile

Instalar Flutter 3.x y luego ver [mobile/README.md](./mobile/README.md) para la guía completa de iOS/Android (firmado, CocoaPods, deployment target). Inicio rápido:

```bash
cd mobile
flutter pub get

# Id de dispositivo = segunda columna de:
flutter devices
# Ejemplo:  Fernando’s iPhone • 00008110-000A7D020140401E • ios • ...
#           emulator-5554     • emulator-5554              • android-arm64 • ...

# Emulador Android (10.0.2.2 → máquina host)
flutter run -d <android-id> --dart-define=API_URL=http://10.0.2.2:8080

# Simulador iOS
flutter run -d <simulator-id> --dart-define=API_URL=http://127.0.0.1:8080

# Teléfono físico (misma Wi‑Fi): usar IP LAN del Mac — ipconfig getifaddr en0
flutter run -d <device-id> --dart-define=API_URL=http://192.168.x.x:8080
```

Si no aparece emulador/simulador: `flutter emulators` / `flutter emulators --launch <id>`, o instalar un runtime iOS en Xcode → Settings → Platforms y luego `open -a Simulator`.

Notas iOS: el deployment target mínimo es **14.0**; los dispositivos físicos necesitan un Development Team en Xcode; CocoaPods es obligatorio (`brew install cocoapods`).

## Credenciales locales por defecto (desde KBX-2)

Definidas en `.env.example` — seedeadas tras el ticket de schema:

- Super admin: `superadmin@kubix.local` / `ChangeMe123!`

## Alcance de la Fase 1 (KBX-1)

Scaffolding del monorepo, docker-compose, endpoint de health, shell web apuntando a la API, stub del proyecto Flutter. El schema de dominio y auth llegan en KBX-2+.
