# Kubix UTN 2.0 — Smart Carpool Security

Plataforma multi-tenant de seguridad para carpooling (super_admin → universidades → campus → coordinador / driver / passenger).

| App | Stack | Path |
|---|---|---|
| API | .NET 10 Web API + EF Core + PostgreSQL | `backend/` |
| Admin web | React + TypeScript + Vite + TanStack Query | `web/` |
| Mobile | Flutter (conductores y pasajeros) | `mobile/` |

Ver [PLAN.md](./PLAN.md) y [STATUS.md](./STATUS.md).

## Estado actual

Release v3.0 implementado y desplegado:

- Web/API pública: <https://d2dgmlbp00gdhh.cloudfront.net>
- AWS: CloudFront + CloudFront Function, S3 privado, ECR, ECS Fargate,
  Application Load Balancer, RDS PostgreSQL 16 `db.t4g.micro`, CloudWatch
  Logs, IAM y security groups.
- App Runner permanece como fallback del script; el runtime real de la cuenta
  de prueba es ECS Fargate porque App Runner no estaba habilitado.
- CI: cobertura backend/web/mobile en cada PR/push; workflow independiente para
  APK Android firmado y aplicación iOS Simulator.

Capacidades recientes:

- Registro obligatorio con carrera UTN searchable, cédula, género e imagen de
  perfil; conductores agregan vehículo e imagen obligatoria.
- Solicitudes de viaje solo entre conductor/pasajero del mismo género.
- Cambio passenger↔driver y aprobación de cambios de perfil del pasajero.
- Borrado lógico auditable de usuarios y coordinadores con revocación de
  sesiones.
- Email SMTP opcional para aceptación/denegación, Maps en web/mobile,
  deep-links SPA, branding UTN y manejo seguro de SOS resuelto remotamente.

## Requisitos previos

- Docker Desktop
- Node.js 20+
- .NET SDK 10 (API en el host / `make api`)
- Flutter 3.x (solo mobile)
- Make (`make help`)

```bash
cp .env.example .env
# Edita GOOGLE_MAPS_API_KEY y, si hace falta, MOBILE_API_URL
make sync-env
```

**Una sola fuente de verdad:** el `.env` raíz. No dupliques claves en `web/.env` ni scripts ad‑hoc.

| Variable | Uso |
|---|---|
| `API_URL` / `API_PORT` | Admin web + docs (**fijo** `:8080`) |
| `GOOGLE_MAPS_API_KEY` | Directions (API) + Maps JS (web) + SDK mobile |
| `MOBILE_API_URL` | Override URL Flutter (si vacío, `make mobile` la deduce) |
| `MOBILE_DEVICE` | Device por defecto para `make mobile` |
| `POSTGRES_*` | Docker Postgres |
| `SMTP_*` | Correo opcional de aceptación/denegación |
| `WEB_ORIGIN` / `WEB_PORT` | Vite (**fijo** `:5173`, `strictPort`) |
| `FLUTTER_WEB_PORT` | Flutter Chrome (**fijo** `:5055`) |

`make sync-env` genera: `web/.env` (VITE_*), `mobile/ios/Flutter/MapsSecrets.xcconfig`, `mobile/web/maps_api_key.js`.

## Cómo levantar

```bash
make help

# Todo: Postgres + API (watch) + Web (Vite) — Ctrl+C detiene API/Web
make up

# Individuales
make db          # solo Postgres
make api         # dotnet watch :8080 (Development + CORS)
make web         # Vite :5173 (falla si el puerto está ocupado)
make mobile      # Flutter (DEVICE=chrome|emulator-5554|<udid>)
make mobile-chrome   # → http://localhost:5055
make mobile-android

make down        # para API/Web pids + docker compose down
```

| Servicio | URL fija |
|---|---|
| API + Swagger | http://localhost:8080/swagger |
| Web admin | http://localhost:5173 |
| Flutter web | http://localhost:5055 |
| Postgres | localhost:55432 |

### Docker (sin hot reload / smoke)

```bash
make docker        # imagen Release
make docker-dev    # API con watch dentro del contenedor
make docker-down
```

### Mobile

```bash
make mobile DEVICE=chrome
make mobile DEVICE=emulator-5554
make mobile DEVICE=00008110-000A7D020140401E   # iPhone: usa IP LAN automáticamente
# o fija en .env: MOBILE_API_URL=http://192.168.1.180:8080
```

Detalle de plataformas: [mobile/README.md](./mobile/README.md). Instalación en
simulador, iPhone y TestFlight:
[mobile/IOS_INSTALL.md](./mobile/IOS_INSTALL.md).

### Utilidades

```bash
make tools         # pgAdmin → http://localhost:5050
curl http://localhost:8080/api/v1/health
```

## Credenciales locales

Seed (`SEED_ON_STARTUP=true`) solo crea el super admin:

- `superadmin@kubix.local` / `ChangeMe123!` (`SUPER_ADMIN_EMAIL` / `SUPER_ADMIN_PASSWORD`)

```bash
curl -s http://localhost:8080/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"superadmin@kubix.local","password":"ChangeMe123!"}'
```
