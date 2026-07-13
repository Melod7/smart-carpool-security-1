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
- .NET SDK 10 (solo si corres la API en el host)
- Flutter 3.x (solo mobile)

```bash
cp .env.example .env
```

## Cómo levantar (elige un modo)

### A) Desarrollo diario — hot reload (recomendado)

Postgres en Docker; API y web en el host con recarga automática al guardar.

```bash
# 1) Solo base de datos
docker compose up -d postgres

# 2) API con hot reload (.NET)
cd backend
dotnet watch run --project src/Kubix.Api
# → http://localhost:8080  ·  Swagger: /swagger

# 3) Web con hot reload (otra terminal)
cd web
npm install
npm run dev
# → http://localhost:5173
```

| Servicio | Hot reload |
|---|---|
| Web (`npm run dev`) | Sí |
| API (`dotnet watch`) | Sí |
| API imagen Release de Docker | No |

Connection string local: `localhost:55432` (ya en `appsettings.Development.json`).

### B) Todo en Docker con hot reload de la API

Monta `./backend` en el contenedor y corre `dotnet watch` (útil si no quieres SDK en el host):

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
# → API http://localhost:8080 (recarga al editar .cs en ./backend)
```

Web sigue aparte:

```bash
cd web && npm run dev
```

### C) Todo en Docker (sin hot reload — smoke / CI local)

Imagen Release publicada. **Hay que rebuild** tras cada cambio de código:

```bash
docker compose up -d --build
curl http://localhost:8080/health
```

Si también tenías `dotnet run` / `dotnet watch` en el host, detenlo antes (`Ctrl+C`): el puerto `8080` no puede compartirse.

### Utilidades

```bash
# Health
curl http://localhost:8080/health

# Swagger
open http://localhost:8080/swagger

# pgAdmin (opcional)
docker compose --profile tools up -d
# → http://localhost:5050  (admin@kubix.local / admin)

# Parar API Docker / stack
docker compose down
# (postgres+datos se conservan en el volume kubix_pgdata)
```

Tracking en vivo (`/admin/tracking`) requiere `VITE_GOOGLE_MAPS_API_KEY` en `web/.env` (ver `web/.env.example`).

### Mobile

Instalar Flutter 3.x y luego ver [mobile/README.md](./mobile/README.md). Inicio rápido:

```bash
cd mobile
flutter pub get
flutter devices

# Emulador Android (10.0.2.2 → máquina host)
flutter run -d <android-id> --dart-define=API_URL=http://10.0.2.2:8080

# Simulador iOS
flutter run -d <simulator-id> --dart-define=API_URL=http://127.0.0.1:8080

# Teléfono físico (misma Wi‑Fi): IP LAN del Mac — ipconfig getifaddr en0
flutter run -d <device-id> --dart-define=API_URL=http://192.168.x.x:8080
```

Notas iOS: deployment target mínimo **14.0**; dispositivos físicos necesitan Development Team en Xcode; CocoaPods (`brew install cocoapods`).

## Credenciales locales por defecto

El seed de arranque (`SEED_ON_STARTUP=true`) **solo** crea el super admin (sin universidades ni usuarios demo):

- Super admin: `superadmin@kubix.local` / `ChangeMe123!` (configurable vía `SUPER_ADMIN_EMAIL` / `SUPER_ADMIN_PASSWORD`)

El resto (universidades, campuses, coordinadores, drivers/passengers) se crea por API.

### Auth

```bash
curl -s http://localhost:8080/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"superadmin@kubix.local","password":"ChangeMe123!"}'
```

Endpoints: `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `POST /auth/change-password`, `GET /me` (Bearer JWT).
