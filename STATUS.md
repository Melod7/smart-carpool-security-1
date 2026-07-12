# Kubix UTN — Smart Carpool Security · STATUS

Última actualización: 2026-07-12
Plan: [PLAN.md](PLAN.md) (v2.4, multi-tenant) · Auditorías: [docs/audit-report-v2.md](docs/audit-report-v2.md) (71/100) → [docs/audit-report-v3.md](docs/audit-report-v3.md) (93/100 en v2.1) → [docs/audit-report-v4-delta.md](docs/audit-report-v4-delta.md) (delta incorporado en v2.3); **v2.4** renombra `university_admin` → `coordinador`

## Fase

| Fase | Estado |
|---|---|
| Discovery + análisis de diseño (ambos exports de Figma) | Hecho |
| Plan v1 (single tenant) | Reemplazado |
| Rediseño Plan v2 (universidades → campuses, super admin, diagramas de secuencia) | Hecho |
| Auditoría de implementabilidad ronda 1 (71/100, 4 blockers) | Hecho |
| Correcciones del plan (los 21 findings) | Hecho |
| Auditoría de implementabilidad ronda 2 (93/100, 0 blockers) | Hecho |
| Plan v2.1 (incorporados 6 ítems menores restantes de la auditoría) | Hecho |
| Plan v2.2 (matriz de permisos de roles + reglas funcionales de EcoTokens, KBX-31) | Hecho |
| Auditoría delta de v2.2 (2 blockers, 8 gaps, 3 inconsistencias) | Hecho |
| Plan v2.3 (los 13 findings de la auditoría delta incorporados) | Hecho |
| Plan v2.4 (renombre de rol: university_admin → coordinador) | Hecho |
| Artefactos de stories | No iniciado (bajo demanda) |
| Implementación | En curso — Fase 1 (KBX-1) |
| KBX-1 entorno local (compose + health + scaffolds) | Hecho |

## Tablero de tickets (desde PLAN.md sección 10)

| Ticket | Título | Estado |
|---|---|---|
| KBX-1 | Scaffolding del monorepo y entorno local | hecho |
| KBX-2 | Esquema de base de datos, migraciones y datos seed | pendiente |
| KBX-3 | Auth: login, JWT, refresh tokens, revocación, cambio de contraseña | pendiente |
| KBX-4 | Infraestructura de tenancy | pendiente |
| KBX-5 | API super admin: universidades, campuses, coordinadores, stats | pendiente |
| KBX-6 | API de registro, gestión de usuarios, perfil y contactos de emergencia | pendiente |
| KBX-7 | API de vehículos y publicación de viajes | pendiente |
| KBX-8 | API de solicitudes de viaje | pendiente |
| KBX-9 | API de ciclo de vida, cancelación e historial de viajes | pendiente |
| KBX-10 | API de ratings y enforcement de rating mínimo | pendiente |
| KBX-11 | API de alertas SOS | pendiente |
| KBX-12 | API del servicio de tracking | pendiente |
| KBX-13 | APIs operativas de admin (dashboard, reportes, auditoría, notificaciones, settings) | pendiente |
| KBX-14 | Scaffolding web, auth y shell de la app | pendiente |
| KBX-15 | Web: consola de super admin | pendiente |
| KBX-16 | Web: Panel de Control | pendiente |
| KBX-17 | Web: Gestión de Usuarios | pendiente |
| KBX-18 | Web: Reportes de Viajes | pendiente |
| KBX-19 | Web: Auditoría de Seguridad | pendiente |
| KBX-20 | Web: Configuración | pendiente |
| KBX-21 | Web: mapa de tracking en vivo | pendiente |
| KBX-22 | Scaffolding mobile, auth y registro | pendiente |
| KBX-23 | Mobile: flujos de pasajero | pendiente |
| KBX-24 | Mobile: flujos de conductor | pendiente |
| KBX-25 | Mobile: flujo SOS | pendiente |
| KBX-26 | Mobile: mapa de viaje en vivo y pings | pendiente |
| KBX-27 | QA: suites unitarias y umbrales de cobertura | pendiente |
| KBX-28 | QA: colección Postman, pruebas de carga y plan JMeter | pendiente |
| KBX-29 | QA: Selenium, TestLink, SonarCloud, MantisBT, SAST | pendiente |
| KBX-30 | Despliegue: infraestructura AWS y CI/CD | pendiente |
| KBX-31 | Motor EcoTokens y API (track backend: después de KBX-10, antes de KBX-13) | pendiente |

Orden de ejecución sugerido: KBX-1→4 (fundación) → 5→10, 31, 11→13 (backend) → 14→21 (web) → 22→26 (mobile) → 27→29 (QA) → 30 (deploy). Los tracks web y mobile pueden ir en paralelo una vez que aterrice KBX-13.

## Log de decisiones clave

- Multi-tenant: super_admin → universidades → campuses; los usuarios siempre pertenecen a una universidad; drivers/passengers se asignan a un campus
- Sin dominio de email hardcodeado; `allowed_email_domain` opcional por universidad
- Auth: JWT propio (60 min) + refresh tokens rotados (14 d) + chequeo de status por request (caché 30 s) para revocación
- Solo polling (sin websockets); intervalos fijos en PLAN.md sección 3
- Google Maps: Directions server-side al publicar; fallback null-polyline = línea recta discontinua
- AWS: App Runner + ECR, RDS Postgres t4g.micro, S3 + CloudFront; local-first vía docker-compose; teardown documentado
- Enums en inglés en DB/API, labels en español en los clients (excepción: param `period`)
- Roles: exactamente 4 (super_admin seedeado, coordinador creado por super admin, driver/passenger auto-registro + aprobación); un rol por cuenta; matriz de permisos en PLAN.md sección 2
- EcoTokens FUNCIONALES en v1 (ledger + engine idempotente, KBX-31): driver +8 / passenger +4 por viaje completado, +2 por rating, +10 racha semanal (5 viajes en la semana lun–dom del timezone de la universidad, una vez por semana vía unicidad de week-key ISO), cancelación tardía del driver −min(5, balance) clamped para que la suma del ledger = balance; niveles Bronce 0 / Plata 100 / Oro 500 / Platino 2000 sobre eco_lifetime; canje fuera de alcance; toggle gamification_enabled por universidad (default true, expuesto a mobile vía GET /me/eco); widget admin XP-por-carrera con datos reales (montos positivos de la semana actual)
- Estático en v1: pagos, subida de docs, chat (solo email de soporte), feed de notificaciones mobile, canje de ECT
