# Kubix UTN — Smart Carpool Security · STATUS

Última actualización: 2026-07-13
Plan: [PLAN.md](PLAN.md) (v2.6 — epic **2 semanas**, **7 tareas** T1–T7 con Título/Prioridad/Fechas/Checklist/Notas/Adjuntos, §10.1) · Auditorías: [docs/audit-report-v2.md](docs/audit-report-v2.md) → [docs/audit-report-v3.md](docs/audit-report-v3.md) → [docs/audit-report-v4-delta.md](docs/audit-report-v4-delta.md); **v2.4** `university_admin` → `coordinador`

## Fase

| Fase | Estado |
|---|---|
| Discovery + análisis de diseño (ambos exports de Figma) | Hecho |
| Plan v1 (single tenant) | Reemplazado |
| Rediseño Plan v2–v2.4 | Hecho |
| Plan v2.5 (Planner: 2 semanas, 7 tareas T1–T7 + Summary/AC) | Hecho |
| Plan v2.6 (campos Planner: prioridad, fechas, checklist, notas, adjuntos) | Hecho |
| Artefactos de stories | No iniciado (bajo demanda) |
| Implementación | **T5 Mobile** hecho (KBX-22–26); siguiente **T6 / KBX-27** |

## Planner (7 tareas · 2 semanas · ancla D1=2026-07-06)

| Tarea | Prioridad | Inicio | Vencimiento | Checklist KBX | Estado |
|---|---|---|---|---|---|
| **T1** Fundación | Urgente | 07-06 | 07-07 | 1–4 | hecho |
| **T2** Backend dominio | Urgente | 07-07 | 07-10 | 5–10 | hecho |
| **T3** Backend ops + Eco | Importante | 07-10 | 07-12 | 31, 11–13 | hecho |
| **T4** Web admin | Importante | 07-11 | 07-15 | 14–21 | hecho |
| **T5** Mobile | Importante | 07-13 | 07-17 | 22–26 | hecho |
| **T6** QA | Importante | 07-15 | 07-18 | 27–29 | **siguiente** |
| **T7** Deploy | Media | 07-17 | 07-19 | 30 | pendiente |

## Tablero KBX (detalle interno)

| Ticket | Título | Estado |
|---|---|---|
| KBX-1 | Scaffolding del monorepo y entorno local | hecho |
| KBX-2 | Esquema de base de datos, migraciones y datos seed | hecho |
| KBX-3 | Auth: login, JWT, refresh tokens, revocación, cambio de contraseña | hecho |
| KBX-4 | Infraestructura de tenancy | hecho |
| KBX-5 | API super admin: universidades, campuses, coordinadores, stats | hecho |
| KBX-6 | API de registro, gestión de usuarios, perfil y contactos de emergencia | hecho |
| KBX-7 | API de vehículos y publicación de viajes | hecho |
| KBX-8 | API de solicitudes de viaje | hecho |
| KBX-9 | API de ciclo de vida, cancelación e historial de viajes | hecho |
| KBX-10 | API de ratings y enforcement de rating mínimo | hecho |
| KBX-11 | API de alertas SOS | hecho |
| KBX-12 | API del servicio de tracking | hecho |
| KBX-13 | APIs operativas de admin | hecho |
| KBX-14 | Scaffolding web, auth y shell de la app | hecho |
| KBX-15 | Web: consola de super admin | hecho |
| KBX-16 | Web: Panel de Control | hecho |
| KBX-17 | Web: Gestión de Usuarios | hecho |
| KBX-18 | Web: Reportes de Viajes | hecho |
| KBX-19 | Web: Auditoría de Seguridad | hecho |
| KBX-20 | Web: Configuración | hecho |
| KBX-21 | Web: mapa de tracking en vivo | hecho |
| KBX-22 | Scaffolding mobile, auth y registro | hecho |
| KBX-23 | Mobile: flujos de pasajero | hecho |
| KBX-24 | Mobile: flujos de conductor | hecho |
| KBX-25 | Mobile: flujo SOS | hecho |
| KBX-26 | Mobile: mapa de viaje en vivo y pings | hecho |
| KBX-27 | QA: suites unitarias y umbrales de cobertura | **siguiente** |
| KBX-28 | QA: colección Postman, pruebas de carga y plan JMeter | pendiente |
| KBX-29 | QA: Selenium, TestLink, SonarCloud, MantisBT, SAST | pendiente |
| KBX-30 | Despliegue: infraestructura AWS y CI/CD | pendiente |
| KBX-31 | Motor EcoTokens y API | hecho |

Orden: T1→T5 hechos · **T6** (27→29) activo · luego T7 Deploy. Commits por KBX-* interno; tarea Planner se cierra al completar su checklist.

## Log de decisiones clave

- Multi-tenant: super_admin → universidades → campuses; los usuarios siempre pertenecen a una universidad; drivers/passengers se asignan a un campus
- Sin dominio de email hardcodeado; `allowed_email_domain` opcional por universidad
- Auth: JWT propio (60 min) + refresh tokens rotados (14 d) + chequeo de status por request (caché 30 s) para revocación
- Solo polling (sin websockets); intervalos fijos en PLAN.md sección 3
- Google Maps: Directions server-side al publicar; fallback null-polyline = línea recta discontinua
- AWS: App Runner + ECR, RDS Postgres t4g.micro, S3 + CloudFront; local-first vía docker-compose; teardown documentado
- Enums/valores en inglés en DB/API; **identificadores C# en español** mapeados a columnas EN (KBX-2)
- Roles: exactamente 4 (super_admin seedeado, coordinador creado por super admin, driver/passenger auto-registro + aprobación); un rol por cuenta; matriz de permisos en PLAN.md sección 2
- EcoTokens FUNCIONALES en v1 (ledger + engine idempotente, KBX-31): driver +8 / passenger +4 por viaje completado, +2 por rating, +10 racha semanal (5 viajes en la semana lun–dom del timezone de la universidad, una vez por semana vía unicidad de week-key ISO), cancelación tardía del driver −min(5, balance) clamped para que la suma del ledger = balance; niveles Bronce 0 / Plata 100 / Oro 500 / Platino 2000 sobre eco_lifetime; canje fuera de alcance; toggle gamification_enabled por universidad (default true, expuesto a mobile vía GET /me/eco); widget admin XP-por-carrera con datos reales (montos positivos de la semana actual)
- Admin ops (KBX-13): `adoptionRate` = % usuarios activos / total (driver+passenger), documentado como `adoptionRateBasis: active_users_over_total`; export CSV/XLSX/PDF vía ClosedXML + QuestPDF (fallo → 500 problem+json)
- Estático en v1: pagos, subida de docs, chat (solo email de soporte), feed de notificaciones mobile, canje de ECT
- Planner v2.5→v2.6: epic en 2 semanas; 7 tareas T1–T7; cada una con Título, Prioridad, Inicio/Vencimiento, Lista de comprobación (KBX+AC), Notas, Datos adjuntos; KBX-* = checklist interna (no 31 tarjetas); ancla D1=2026-07-06
