# Kubix UTN 2.0 — Web administrativa

SPA React 19 + TypeScript + Vite 8 para `super_admin` y `coordinador`.

## Rutas

| Scope | Rutas | Funciones |
|---|---|---|
| Super admin | `/super/*` | Universidades, campuses con Google Maps, coordinadores (crear/resetear/eliminar) y estadísticas |
| Coordinador | `/admin/*` | Dashboard, altas/cambios, usuarios (block/unblock/delete), viajes, SOS, tracking, auditoría y configuración |

El login y el shell incluyen branding UTN. Los deep links funcionan mediante
la CloudFront Function del stack de deploy.

## Desarrollo

Desde la raíz:

```bash
cp .env.example .env
make sync-env
make api
make web
```

Web: <http://localhost:5173>. La API se obtiene de `VITE_API_URL`; Maps usa
`VITE_GOOGLE_MAPS_API_KEY`, ambas generadas desde el `.env` raíz.

## Calidad

```bash
cd web
npm ci
npm test
npm run lint
npm run build
```

Vitest/React Testing Library cubre guards, auth y pantallas principales.
`pr-tests.yml` ejecuta el gate web en cada PR y push a `main`.

## Producción

El build estático se sincroniza a S3 privado y se sirve por
<https://d2dgmlbp00gdhh.cloudfront.net>. CloudFront enruta `/api/*` al ALB/ECS
y conserva las rutas `/admin/*` y `/super/*` en la SPA.
