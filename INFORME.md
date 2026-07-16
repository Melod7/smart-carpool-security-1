# Kubix UTN — Smart Carpool Security  
## Informe de factibilidad e ingeniería de software

**Fecha:** julio 2026  
**Fuentes:** `PLAN.md` v2.9 · `STATUS.md` · historial git (≈45 commits, jul 2026)

---

## 1. Qué es el proyecto

Plataforma **multi-tenant** de carpooling seguro para universidades: identidades aprobadas, SOS, GPS en vivo, auditoría y rutas por waypoints.

| Quién | Cliente | Qué hace |
|---|---|---|
| Super admin | Web `/super` | Universidades, campuses, coordinadores |
| Coordinador | Web `/admin` | Aprueba usuarios, SOS, mapa, reportes, auditoría |
| Conductor | Mobile | Publica ruta, acepta pasajeros, inicia/completa viaje |
| Pasajero | Mobile | Pide viaje, punto de espera, califica, SOS |

| Parte | Stack real | Carpeta |
|---|---|---|
| API | .NET 10 + EF Core + PostgreSQL 16 | `backend/` |
| Admin | React + TypeScript + Vite | `web/` |
| App | Flutter 3.x | `mobile/` |
| Mapas | Google Maps (Directions en servidor) | — |

**Estado (commits + STATUS):** T1–T5 + T8 hechos · T6 QA siguiente · T7 Deploy pendiente.

---

## 2. Arquitectura

```
Mobile (Flutter)  ──┐
                    ├── REST + JWT + polling ──► API .NET ──► PostgreSQL
Web admin (React) ──┘                              │
                                                   └── Google Directions
```

- Una sola BD; aislamiento por `university_id` (filtros EF).
- JWT 60 min + refresh 14 días; bloqueo mid-session.
- Sin WebSockets en v1: polling (SOS/tracking ~10 s).
- Capas API: Api / Application / Domain / Infrastructure.

---

## 3. Factibilidad — Framework y roles

### A. Framework ágil → Scrum

El proyecto se gestiona con **Scrum**, materializado en el epic **KBX** y el Planner **T1–T8** (ancla D1 = 2026-07-06).

#### Artefactos

| Artefacto Scrum | En Kubix |
|---|---|
| **Product Backlog** | Tickets KBX-1…33 en `PLAN.md` (auth, trips, SOS, web, mobile, QA, deploy) |
| **Sprint Backlog** | Checklist por tarea Planner (T1 Fundación … T8 Waypoints … T7 Deploy) |
| **Incremento** | API + Web + Mobile que levantan con `make up` |
| **Definition of Done** | Criterios QA del ticket + tenancy OK + demo del rol afectado |
| **Burndown / avance** | `STATUS.md` + commits por KBX |

#### Ceremonias

| Ceremonia | Cómo se aplica |
|---|---|
| **Sprint Planning** | Elegir T* / KBX del Planner; fijar AC del checklist |
| **Daily** | Desbloquear Maps, CORS, `.env`, builds iOS/Android |
| **Review** | Demo: super_admin → coordinador → driver/passenger → SOS |
| **Retrospectiva** | Decisiones en STATUS (polling, waypoints, enums EN, etc.) |
| **Refinement** | Auditorías de plan (`docs/audit-report-v*.md`) y deltas v2.7–v2.9 |

#### Principios y valores

- **Transparencia:** plan y estado públicos en el repo; UI en español, API en inglés.
- **Inspección:** demos por rol; healthcheck; Swagger; auditorías del plan.
- **Adaptación:** T8 (waypoints + espera) añadido sin romper tenancy ni el resto del backlog.
- **Entrega incremental:** commits `feat(backend|web|mobile)` por KBX, no big-bang.
- **Seguridad primero:** aislamiento multi-tenant, SOS auditable, revocación de sesión.

---

### B. Roles

#### Roles Scrum

| Rol | Responsabilidad en este proyecto |
|---|---|
| **SM (Scrum Master)** | Facilita flujo; remueve impedimentos (Docker, keys Maps, CORS, sync env) |
| **PO (Product Owner)** | Prioriza KBX; acepta DoD; define v1 vs fuera de alcance (pagos, chat, WebSockets…) |
| **TS (Team Scrum)** | Entrega el incremento de software |

#### Especialidades del Team Scrum

| Especialidad | Qué entrega en Kubix | Evidencia |
|---|---|---|
| **BDD / Datos** | Schema Postgres, migraciones EF, seed solo super_admin, query filters | KBX-2, KBX-4 |
| **DEV** | Backend .NET, Web React, Mobile Flutter | Commits T1–T5 + T8 |
| **UX/UI** | Flujos Figma → pantallas admin (5) + mobile (4 tabs + SOS + mapa ruta) | KBX-14…26, 33 |

#### Roles de producto (usuarios)

| Rol | Scope | Alta |
|---|---|---|
| `super_admin` | Global | Seed env (uno en v1) |
| `coordinador` | Una universidad | Creado por super_admin (password temporal) |
| `driver` | Universidad + campus | Auto-registro + vehículo → aprobación |
| `passenger` | Universidad + campus | Auto-registro → aprobación |

No hay rol de guardia en v1: el **coordinador** atiende el SOS.

---

## 4. Factibilidad técnica, operativa, económica e inversión

### C. Factibilidad técnica

#### Disponibilidad tech (cloud)

| Necesidad | Tecnología disponible | Uso en Kubix |
|---|---|---|
| API contenedorizada | .NET 10 + Docker / App Runner | `backend/` · plan T7 |
| Base de datos | PostgreSQL 16 (Docker local / RDS) | Persistencia multi-tenant |
| Front admin | React estático → S3 + CloudFront | `web/` |
| App móvil | Flutter (Android / iOS / Chrome) | `mobile/` |
| Mapas y rutas | Google Maps Platform | Directions server-side + SDK/JS clients |
| Auth | JWT propio (sin IdP externo en v1) | Login, refresh, logout, change-password |
| CI/CD | GitHub Actions (planificado) | T7 / KBX-30 |

**Evidencia:** el stack ya corre en local (`make up`). Cloud es el siguiente paso, no un bloqueo técnico.

#### Skills y competencias tech

| Competencia | Nivel requerido | Estado del equipo/proyecto |
|---|---|---|
| C# / EF Core / REST | Alto | Cubierto (T1–T3, T8) |
| Multi-tenancy y auth | Alto | Cubierto (KBX-3, KBX-4) |
| React + TanStack Query | Medio-alto | Cubierto (T4) |
| Flutter + mapas + GPS | Medio-alto | Cubierto (T5, T8) |
| Modelado Postgres / migraciones | Medio | Cubierto |
| QA automatizado (Postman, JMeter, Selenium, SAST) | Medio | **Pendiente T6** |
| AWS (App Runner, RDS, S3, CloudFront) | Medio | **Pendiente T7** |

**Conclusión skills:** el núcleo productivo está cubierto; faltan competencias de cierre (QA + cloud) explícitas en el Planner.

#### Escalabilidad y mantenimiento · adaptarse a nuevas necesidades

| Aspecto | Diseño actual |
|---|---|
| **Escala de tenants** | Muchas universidades en un schema; filtros por `university_id` |
| **Escala de tiempo real** | Polling acotado (no WebSockets); índices en pings; JMeter en T6 |
| **Mantenimiento de código** | Capas Api/Application/Domain/Infrastructure; tickets pequeños por feature |
| **Mantenimiento de datos** | Migraciones EF; purga de `location_pings` a 7 días; audit_events retenidos |
| **Adaptarse a nuevas necesidades** | Settings por universidad (dominio email, límites, CO₂, gamificación); waypoints v2.7 sin reescribir el dominio |
| **Límites v1 aceptados** | Sin chat, push, pagos ni canje ECT — evita scope creep |

**Riesgos técnicos y mitigación**

| Riesgo | Mitigación |
|---|---|
| Fuga de datos entre universidades | Query filters + tests KBX-4; cross-tenant → 404 |
| Costo / cuota Google Maps | Directions solo al publicar; keys restringidas; fallback polyline null |
| Carga por polling | Intervalos fijos; pruebas de carga T6 |
| Deuda de QA | T6 bloqueante antes de considerar “cerrado” |

**Veredicto C:** **Factible.** Tecnología disponible, skills del núcleo demostrados en commits, diseño preparado para crecer y adaptarse.

---

### D. Factibilidad operativa tech

#### Usabilidad y adopción (curvas de aprendizaje)

| Actor | Curva | Por qué es manejable |
|---|---|---|
| **Pasajero** | Baja | Wizard de registro → lista de viajes → punto de espera sugerido |
| **Conductor** | Media | Mapa de waypoints (2–8) + cola accept/reject; FAB “Agregar ruta” |
| **Coordinador** | Media | Panel con KPIs, cola de registros, SOS, mapa — flujo administrativo conocido |
| **Super admin** | Baja | CRUD universidades / campuses / coordinadores |

Adopción institucional: el coordinador es el “dueño” operativo del tenant (aprueba quién entra). Sin aprobación no hay uso — eso baja el riesgo de usuarios externos.

Copy SOS: *seguridad del campus notificada* (no se afirma contacto automático a familiares en v1).

#### Integración (compatibilidad) — BDD · DEV · ARQ-SW

| Capa | Compatibilidad |
|---|---|
| **BDD** | Postgres 16; schema único; `university_id` en tablas hijas; enums EN en DB/API |
| **DEV** | Monorepo: `backend/`, `web/`, `mobile/`, `qa/`, `deploy/`; una sola `.env` + `make sync-env` |
| **ARQ-SW** | Clients → `/api/v1` + JWT → API → DB; Maps tiles en clients; Directions solo en servidor |
| **Entornos** | Local Docker Compose; cloud planificado App Runner + RDS + CloudFront |
| **Contratos** | Errores RFC 7807; Swagger; mismos endpoints para web y mobile |

Commits recientes de integración operativa: unificar `.env`, fijar puertos 8080/5173/5055, CORS Flutter web.

#### Disponibilidad y soporte · Seguridad → Backup

| Tema | Enfoque |
|---|---|
| **Disponibilidad local** | `make up`: API, Web, Postgres siempre en los mismos puertos |
| **Disponibilidad cloud (plan)** | App Runner (API) + RDS + CloudFront (web); healthcheck `/health` |
| **Soporte operativo** | Coordinador resuelve SOS; `support_email` en settings; sin rol guardia en v1 |
| **Seguridad de acceso** | Aprobación de registros; JWT + refresh; block/suspend mid-session; policies por rol |
| **Seguridad de datos** | Aislamiento tenant; auditoría (sos/auth/admin/system); rating mínimo → auto-block |
| **Seguridad de mapas** | API key Directions en servidor; keys client restringidas por app/referrer |
| **Backup / retención** | Dumps Postgres / snapshots RDS (T7); last ping por viaje; audit_events para forensics; pings viejos purgados |

**Veredicto D:** **Factible operativamente** para piloto universitario. La operación diaria cae en el coordinador; el soporte cloud y backups formales se cierran en T7.

---

### E. Viabilidad económica

#### Análisis costo-beneficio (costo implementación vs ahorro de tiempo)

| Costos de implementar | Beneficios / ahorro |
|---|---|
| Horas DEV del stack (API + web + mobile) — **ya invertidas** en T1–T5 + T8 | Menos coordinación manual de rides (grupos WhatsApp / hojas) |
| Horas QA + deploy (T6–T7) — **pendientes** | Menos tiempo del coordinador buscando “quién va / quién es confiable” |
| Infra cloud + Google Maps (OPEX) | Identidades verificadas + SOS + auditoría = menos riesgo institucional |
| Capacitación breve a coordinadores | Visibilidad de viajes activos y reportes exportables (CSV/XLSX/PDF) |
| | Incentivo EcoTokens / CO₂ → más adopción del carpool |

**Beneficio principal (no es solo dinero):** gobernanza y seguridad del carpooling universitario. El ahorro de tiempo del staff de campus y la trazabilidad ante incidentes justifican el piloto aunque no haya suscripción comercial en v1.

#### Ratios financieros y KPI

Marco para evaluar el piloto (el repo no define precio de venta; sí define KPIs de producto):

| Indicador | Cómo se interpreta en Kubix |
|---|---|
| **ROI** | (Valor de riesgo mitigado + horas de gestión evitadas − costo total) / costo total |
| **VAN (NPV)** | Suma descontada de beneficios − costos (Maps + cloud + mantenimiento) a 12–24 meses |
| **TIR (IRR)** | Tasa que hace VAN = 0; útil si la universidad modela “ahorro anual” vs inversión |
| **Rentabilidad** | Costo por viaje seguro / usuario activo vs alternativas (taxi, apps abiertas, gestión manual) |
| **Utilidad** | Adopción, SOS resueltos a tiempo, CO₂ ahorrado, viajes completados, rating promedio |

**KPI de producto ya en el dashboard (implementados):** viajes del día, usuarios bloqueados, CO₂, tasa de adopción, SOS activos, XP/ECT por carrera.

#### Punto de equilibrio

```
Costo fijo ───── (DEV inicial + infra base + keys)
Costo variable ─ (requests Maps + compute + storage)
Valor / ingreso ─ (ahorro tiempo + valor seguridad; o suscripción hipotética)

Costo ────────╲
               ╲
Valor ──────────╲──────► GANANCIA
                 X
            punto de equilibrio
```

El equilibrio se alcanza cuando el **valor institucional acumulado** (tiempo + riesgo) supera costos fijos + Maps. En fase académica, el “ingreso” se mide como equivalencia de horas evitadas y cumplimiento de seguridad campus — no como ticket de venta.

**Veredicto E:** **Viable económicamente como piloto** si se controla Maps y se ejecuta teardown AWS. El núcleo DEV ya está pagado en esfuerzo; el OPEX futuro es el foco.

---

### F. Costo de inversión tech

#### Costo DEV (horas stack DEV + QA)

| Bloque | Contenido | Estado | Peso relativo |
|---|---|---|---|
| T1 Fundación | Monorepo, DB, auth, tenancy | Hecho | Alto (base) |
| T2 Backend dominio | Universidades, users, trips, ratings | Hecho | Alto |
| T3 Backend ops | EcoTokens, SOS, tracking, admin API | Hecho | Alto |
| T4 Web admin | Super + coordinador (panel…mapa) | Hecho | Alto |
| T5 Mobile | Auth, pax, driver, SOS, mapa | Hecho | Alto |
| T8 Waypoints | Ruta + punto de espera + polish Maps | Hecho | Medio |
| **T6 QA** | Coverage, Postman, JMeter, Selenium, SAST | **Pendiente** | Medio |
| **T7 Deploy** | AWS + CI/CD + teardown | Pendiente | Medio-bajo |

Inversión DEV del producto usable: **ya realizada** (historial `feat(backend|web|mobile)` por KBX). Inversión restante estimada: **~20–30%** (calidad + cloud).

#### Costo infraestructura (SaaS · IaaS · PaaS)

| Modelo | Servicio | Rol en Kubix | Notas de costo |
|---|---|---|---|
| **SaaS** | Google Maps Platform | Directions + tiles | Principal riesgo de billing; restringir keys |
| **PaaS** | AWS App Runner | API contenedor | Free-tier friendly en plan |
| **IaaS / DB managed** | RDS PostgreSQL t4g.micro | Datos | Pequeño instance size en plan |
| **SaaS/CDN** | S3 + CloudFront | Hosting web | Estático, bajo costo |
| **Local (dev)** | Docker Compose | Postgres + opcional API | Sin costo cloud |

Regla de oro del plan: **documentar teardown** para no dejar recursos AWS facturando tras pruebas.

#### Mantenimiento y update

| Ítem | Qué implica | Frecuencia |
|---|---|---|
| Migraciones de schema | Nuevas reglas / tablas | Por release |
| Dependencias | .NET, npm, Flutter, plugins Maps | Por sprint / CI |
| Secrets y keys | Maps, JWT, DB, CORS origins | Según rotación |
| Job de pings | Borrar pings >7 días post viaje terminal | Diario |
| Settings por universidad | Límites, dominio email, toggles | On-demand (admin) |
| Parches de seguridad | SAST/Sonar (T6) + updates runtime | Continuo tras T6 |
| Updates de producto | Features nuevas (push, canje ECT…) | Fuera de v1; nuevo backlog |

**Veredicto F:** Inversión acotada y mayormente **ya ejecutada** en DEV. Costos recurrentes críticos: **Google Maps** + **RDS/App Runner**. Mantenimiento es sostenible gracias a capas claras y settings por tenant.

---

## 5. Historias de usuario (3C)

**Card** = quién/qué/para qué · **Conversation** = reglas · **Confirmation** = prueba.

### 1. Registrar y ser aprobado
- **Card:** Como estudiante, quiero registrarme (universidad + campus + rol) para usar la app cuando el coordinador me apruebe.
- **Conversation:** Dominio de email opcional; driver incluye vehículo; estado “pendiente” hasta accept.
- **Confirmation:** Registro → cola admin → accept permite login; deny no. *(KBX-6, 17, 22 — hecho)*

### 2. Publicar ruta (conductor)
- **Card:** Como conductor, quiero marcar mi ruta en el mapa hacia el campus para que me encuentren en el camino.
- **Conversation:** 2–8 waypoints; Directions en servidor; si Maps falla, viaje con línea aproximada.
- **Confirmation:** Viaje con waypoints + polyline en “disponibles”. *(KBX-7, 32, 33 — hecho)*

### 3. Pedir viaje y punto de espera (pasajero)
- **Card:** Como pasajero, quiero ver dónde esperarme sobre la ruta del conductor.
- **Conversation:** Solo mi campus; sugerencia del punto más cercano; aviso si >800 m.
- **Confirmation:** `suggestedWait` + pickup visible al conductor. *(KBX-8, 32, 33 — hecho)*

### 4. SOS
- **Card:** Como usuario, quiero enviar SOS con GPS para que el coordinador actúe.
- **Conversation:** Queda en auditoría; contactos de emergencia informativos (sin auto-llamada en v1).
- **Confirmation:** Alerta en panel; resolve / “estoy a salvo”. *(KBX-11, 25, 16 — hecho)*

### 5. Tracking y auditoría (coordinador)
- **Card:** Como coordinador, quiero ver viajes activos, SOS, reportes y log de mi universidad.
- **Conversation:** Solo mi tenant.
- **Confirmation:** Dashboard + mapa + auditoría filtrable. *(KBX-13, 16–21 — hecho)*

### 6. EcoTokens
- **Card:** Como usuario, quiero ganar puntos por viajes compartidos.
- **Conversation:** +8 conductor / +4 pasajero; rachas; canje aún no.
- **Confirmation:** Balance en `GET /me/eco`. *(KBX-31 — hecho)*

---

## 6. Conclusión

| Punto | Veredicto |
|---|---|
| **A Framework** | Scrum + KBX/Planner adecuado y ya usado |
| **B Roles** | SM/PO/TS + BDD/DEV/UX y 4 roles de producto claros |
| **C Técnica** | Factible — stack disponible y núcleo implementado |
| **D Operativa** | Factible — usabilidad por rol, integración monorepo, seguridad/backup definidos |
| **E Económica** | Viable en piloto — beneficio = seguridad + tiempo; cuidar Maps |
| **F Inversión** | DEV hecho; restan QA + cloud; OPEX = Maps + infra |

**Recomendación:** cerrar **T6 (QA)** y **T7 (deploy + teardown)**. El producto ya es demostrable en local con `make up`.
