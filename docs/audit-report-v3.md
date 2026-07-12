# Re-auditoría de implementabilidad del plan v3 — Kubix UTN Smart Carpool Security Platform

Artefacto auditado: `smart-carpool-security/PLAN.md` (v2.0, revisado tras la auditoría v2)
Informe previo: `docs/audit-report-v2.md` (puntuación 71/100; 4 blockers, 10 gaps, 7 inconsistencias)
Método: cada finding previo re-verificado contra el plan revisado con citas de sección (§) / ticket (KBX-n); las revisiones se chequearon por contradicciones nuevas introducidas; re-puntado con la misma rúbrica 10×10.

---

## Parte 1 — Resolución de findings previos

### Bloqueadores

| # | Prior finding | Status | Where resolved |
|---|---|---|---|
| B1 | Contradicción de schema de tenancy: cinco tablas (`ride_requests`, `ratings`, `location_pings`, `vehicles`, `emergency_contacts`) carecían de `university_id`, rompiendo el diseño de global-query-filter | **RESOLVED** | Las reglas de tenancy de §2 ahora especifican `university_id` desnormalizado en las cinco tablas hijas al insertar; el schema de §4 añade la columna a cada una de las cinco; §4 reafirma la regla de desnormalización bajo la lista de tablas; KBX-2 migra/seedea ("denormalized `university_id` on all child tables") con QA verificando que está poblado en las cinco; KBX-4 añade un interceptor save-changes que lo estampa al insertar, filtra "ALL tenant tables — including the denormalized child tables", y su QA corre tests negativos cross-tenant "for every tenant table including the five child tables" |
| B2 | Diseño de refresh-token y revocación ausente; QA de KBX-5/KBX-6 (block/suspend surte efecto a mitad de sesión) inimplementable con JWTs stateless | **RESOLVED** | §2 "Auth token model": refresh token opaco hasheado en la nueva tabla §4 `refresh_tokens` (user_id, token_hash, expires_at 14 d, revoked_at), rotado en el nuevo §5 `POST /auth/refresh`, revocado en logout/block/suspend; revocación vía chequeo por request de user-status + university-status (caché 30 s) nombrado explícitamente como lo que hace implementable la QA de block/suspend; mobile refresca de forma proactiva para que los trips largos conserven capacidad de ping (también cierra el modo de fallo previo JWT-60-min-a-mitad-de-trip). KBX-3 reescrito con Description y QA coincidentes (rotación, rechazo de token viejo, 403 dentro de la ventana de caché, lockout por suspend, recuperación 401→refresh); block de KBX-6 "also revokes refresh tokens" |
| B3 | Cascade de cancelación del driver indefinido | **RESOLVED** | Business rules de §4: cancelar un trip `scheduled` mueve requests pending/accepted a `cancelled_by_driver` (status añadido al enum §4 `ride_requests`); los passengers observan vía poll `GET /trips/mine`; cancelar `in_progress` → 409. KBX-9 ahora posee en exclusiva `POST /trips/{id}/cancel` (solapamiento KBX-7 eliminado) con QA para el 409, el cascade, la observación vía poll del passenger y el audit event de late-cancel |
| B4 | Path de fallo de Google Directions indefinido | **RESOLVED** | Nota de §5: en timeout/quota/`ZERO_RESULTS`, `POST /trips` igual tiene éxito con `polyline = null` (columna hecha nullable en §4) + `distance_km` haversine; 422 solo para coordenadas de origin inválidas; las superficies de mapa renderizan una línea recta discontinua cuando polyline es null. La QA de KBX-7 cubre el mock de fallo y el 422; la QA de KBX-21 renderiza la línea discontinua null-polyline en el tracking admin; KBX-26 la renderiza en el mapa mobile del trip |

### Brechas

| # | Prior finding | Status | Where resolved |
|---|---|---|---|
| G1 | `GET/PUT /me/profile`, `GET/POST/DELETE /me/emergency-contacts` no tenían ticket backend | **RESOLVED** | La Description de KBX-6 ahora incluye ambos grupos de endpoints (máx. 3 contactos, validación de teléfono); la QA cubre round-trip CRUD, tests de tenancy y 422 del 4.º contacto — coincidiendo con la QA consumidora de KBX-23 |
| G2 | `min_driver_rating` almacenado pero nunca enforceado | **RESOLVED** | Business rule de §4: en el recompute de rating, driver bajo el umbral de la universidad con ≥5 ratings se auto-setea `blocked` + audit event + notificación admin; implementado en KBX-10 con QA positiva y negativa (<5 ratings) |
| G3 | Sin close SOS del lado usuario para "Estoy a salvo" | **RESOLVED** | §5 `POST /sos/{id}/close` (solo owner; resolved con resolver = owner, permanece en el historial admin); KBX-11 lo implementa con QA (close de la alerta de otro → 404); KBX-25 cablea el botón del overlay a él |
| G4 | Sin endpoint mark-read de notificaciones | **RESOLVED** | §5 `PUT /admin/notifications/{id}/read` y `PUT /admin/notifications/read-all`; KBX-13 implementa con QA "mark-read decrements the unread badge"; la QA de KBX-14 cubre el poll del badge |
| G5 | Impersonación read-only de super-admin huérfana en §2 | **RESOLVED** | Declarada fuera de alcance en §2 (nota al final de la sección de tenancy) y listada en §11; la frase de capability huérfana se eliminó de la tabla de roles de §2 |
| G6 | Restitución de asiento en cancel del passenger sin especificar | **RESOLVED** | Business rule de §4: cancel de request accepted restaura `seats_available +1`, requests auto-rechazados NO se reactivan; Description y QA de KBX-8 (asiento restaurado, trip reaparece en `/trips/available`) |
| G7 | `university_settings` sin columnas de la pantalla Settings | **RESOLVED** | El set de columnas de §4 ahora incluye timezone, support_email, co2_tracking_enabled, notify_sos, notify_block, notify_weekly_report (más las columnas existentes de gamificación/dominio/límites); KBX-2 migra el set completo; KBX-13 `GET/PUT /admin/settings` lo persiste; KBX-20 cablea la pantalla |
| G8 | Canal de notificación mobile indefinido (icono campana, "notifies passenger's poll") | **RESOLVED** | Nota de §5: mobile no tiene feed de notificaciones en v1; el estado se observa exclusivamente vía polls existentes; icono de campana omitido (reafirmado en el inventario mobile de §7 y fuera de alcance de §11); QA de KBX-24 reformulada ("the passenger's `/trips/mine` poll shows the confirmed trip — no push/notification feed on mobile") |
| G9 | Retención de `location_pings` sin abordar | **RESOLVED** | Política de retención de §4: job diario en background borra pings 7 días después de que el trip alcanza status terminal, conservando el último ping de cada usuario para auditoría; job asignado a KBX-12 con QA (borra viejos, conserva last-per-user) |
| G10 | El copy del overlay SOS promete notificación a emergency contacts que el sistema no hace | **RESOLVED** | §4: emergency contacts son solo informativos en v1, con la instrucción explícita de copy ("seguridad del campus notificada"); KBX-25 ajusta el copy; §11 lista la notificación automática de contactos como fuera de alcance |

### Inconsistencias

| # | Prior finding | Status | Where resolved |
|---|---|---|---|
| I1 | §2 "every tenant table carries `university_id`" vs schema de §4 | **RESOLVED** | Mismo fix que B1 — §2, §4, KBX-2 y KBX-4 ahora coinciden (columna desnormalizada en todas las tablas hijas) |
| I2 | Return trips en ambos datasets de Figma pero no en el modelo de dominio | **RESOLVED** | §11 declara explícitamente "return trips originating from campus" fuera de alcance, reconociendo que ambos datasets de Figma los muestran; KBX-2 manda que los seeds contengan solo trips hacia el campus |
| I3 | Ownership del XP mock (server vs client) | **RESOLVED** | Decidido client-static de una vez: nota de §5 ("`GET /admin/dashboard` does not return XP data"), descripción del endpoint §5 ("KPI cards + active SOS list" — "+ XP mock" eliminado), KBX-13 y KBX-16 ambos dicen client-static |
| I4 | Export Figma titulado "Super Admin" pero pantallas mapeadas a university_admin, sin comentar | **RESOLVED** | La nota de §2 explica el re-mapeo deliberado y que la consola verdadera `/super/*` es nueva y no está en Figma |
| I5 | Asunción de seguridad falsa de KBX-21 "map key from env not bundle" | **RESOLVED** | QA de KBX-21 reformulada: "Maps JS key injected via env at build time and protected by HTTP-referrer restriction (client keys are always bundle-visible)" — ahora consistente con la afirmación Directions-key-server-side de §3 |
| I6 | Valores del diseño en español vs enums ingleses de §4, convención sin documentar | **RESOLVED (con un nuevo matiz)** | Convención de enums de §4: enums en inglés en DB y API, labels en español en clients — documentada como se recomendó. Sin embargo el wording blanket ahora conflictúa con dos valores en español que sobreviven (ver NEW-1 abajo) |
| I7 | Ventana de rating del passenger enunciada solo en KBX-10 | **RESOLVED** | Business rule de §4: "passenger rates the driver any time after completion (no window); driver rates passengers within 12 h" — coincide exactamente con KBX-10 |

### Findings previos no numerados/menores (también re-chequeados)

| Prior remark | Status | Where |
|---|---|---|
| Intervalo de poll de `GET /trips/available` del passenger sin especificar | Resolved | §3: available trips 30 s; trip status 15 s while upcoming/active |
| Renderizado del mapa de trip scheduled sin describir | Resolved | KBX-26: route + pickup point only, no pings, with QA |
| Solapamiento de ownership de cancel KBX-7/KBX-9 | Resolved | KBX-9 owns cancellation; KBX-7 no longer mentions it |
| Desajuste de nullability del claim JWT `campus_id` | Resolved | §2 claims: "null for super_admin and university_admin" — matches §4 `users` |
| Diseño de columnas recipient de `notifications` indefinido | Resolved | §4: `recipient_user_id` (nullable) + `recipient_role` (nullable), exactly one set |
| Path de migración RDS desde CI poco claro | Resolved | §8: `Database.Migrate()` at container startup gated by env flag; no CI VPC access needed |
| CORS sin mencionar | Resolved | §5 note: API allows localhost:5173 + CloudFront domain via configuration |
| Flake Selenium vs pantalla de tracking Maps | Resolved | §9 + KBX-29: map screen explicitly excluded, covered by manual TestLink cases |
| Pregunta sobre instancia MantisBT | Resolved | §9: local instance via `qa/mantis/docker-compose.yml` |
| JWT 60-min vs trips largos | Resolved | §2: proactive mobile refresh |
| Guía offline/poll-failure mobile | Resolved | KBX-26: failed pings dropped (not queued), airplane-mode QA |
| Fallo de generación de export de reportes | Resolved | KBX-13: 500 problem+json surfaced as toast |

**Los 21 findings previos numerados están resueltos. 4/4 blockers, 10/10 gaps, 7/7 inconsistencias.**

---

## Parte 2 — Nuevos findings introducidos por las revisiones

- **NEW-1 (inconsistency, menor).** La nueva convención de enums (§4: "all statuses/enums are **English in DB and API**") se contradice con dos valores que el propio plan mantiene en español: §4 `audit_events.severity (alta/media/baja)` y el `GET /admin/reports?period=diario|semanal|mensual|trimestral|anual` de KBX-13. O bien acotar la convención ("trip/request/user statuses and audit types") o convertir severity a `high/medium/low` y el param period a inglés. Fix de una línea; sin impacto en buildability ya que cada set de valores es individualmente inequívoco.
- **NEW-2 (wording, menor).** El chequeo de revocación de KBX-3 tiene caché de 30 s (y su propia QA dice correctamente "within 30 s cache window"), pero la QA de KBX-6 aún dice "block mid-session → next authed call 403" — literalmente, la siguiente call misma puede servirse desde la caché hasta 30 s. Añadir "within ≤30 s" a la QA de KBX-6 para coincidir con el mecanismo al que delega.

No se introdujeron otras contradicciones; las nuevas notas de §5, business rules de §4 y tickets reescritos son mutuamente consistentes (ownership XP, cascade de cancel, política Directions, decisión del canal mobile e intervalos de polling se cross-chequearon across §3/§5/§6/§7 y KBX-7/8/9/13/16/21/24/26).

---

## Parte 3 — Re-puntuación de la rúbrica

### 1. Claridad de alcance — 10/10
Los tres findings previos cerrados: la impersonación ahora está explícitamente fuera de alcance (nota §2 + §11), los return trips se declaran fuera de alcance con una restricción de seed que reconoce la discrepancia de Figma (§11, KBX-2), y los emergency contacts se declaran solo informativos con copy SOS corregido (§4, KBX-25, §11). §11 es ahora un límite v1 completo y auto-consistente; cada decisión de mock estático y omisión se reafirma en el ticket consumidor. Sin findings.

### 2. Completitud del modelo de datos — 9/10
`university_id` presente en las 15 tablas con la regla de desnormalización enunciada (§2, §4, KBX-2/4); `refresh_tokens` añadido; diseño recipient de `notifications` definido (dos columnas nullable, exactly-one-set); `university_settings` cubre cada campo de la pantalla Settings; retención de `location_pings` especificada con ticket propietario.
**Hallazgos (menores):** NEW-1 — `severity (alta/media/baja)` contradice la propia convención English-enum del plan. Las notificaciones dirigidas por rol llevan un solo flag `read`; si una universidad alguna vez tiene más de un admin (el modelo lo permite — `POST /super/universities/{id}/admins` es plural), el estado read se comparte entre admins. Aceptable para v1, vale una línea aclaratoria.

### 3. Claridad del contrato de API — 9/10
La superficie ahora está cerrada: `POST /auth/refresh`, `POST /auth/logout`, `GET /public/universities`, `POST /sos/{id}/close`, mark-read/read-all de notificaciones todos añadidos; ownership XP decidido (client-static) y el contrato del dashboard limpio; el canal de notificación mobile es explícitamente "solo polls, campana omitida"; el contrato de fallo de Directions (polyline null, haversine, regla 422) es preciso.
**Hallazgos (menores):** los bodies request/response siguen implicados por shapes de Figma y diagramas de secuencia (sin cambios desde v2, previamente aceptado a este nivel de planning). Los valores en español del param `period` de NEW-1 quedan raros frente a la convención English-API.

### 4. Flujo de datos y renderizado — 9/10
Cada intervalo de poll está ahora especificado (§3 añade available-trips 30 s y `/trips/mine` 15 s); el mecanismo de observación del passenger para accepts y cancels del driver está nombrado (el poll `/trips/mine` — regla §4, KBX-9, KBX-24); el renderizado del mapa scheduled-vs-in-progress está definido (KBX-26); el renderizado null-polyline está definido en las tres superficies de mapa; la política de pings offline está definida (descartados, no encolados).
**Hallazgo (menor):** si `GET /trips/mine` incluye trips donde el request del passenger sigue `pending` no está especificado — la "next trip card" de PaxHome y el trigger del poll "while a trip is upcoming" ambos dependen de la respuesta. Una frase lo resolvería.

### 5. Completitud y ordenabilidad de tickets — 9/10
Huérfanos previos eliminados: profile + emergency contacts (KBX-6), enforcement min-rating (KBX-10), SOS close (KBX-11), mark-read (KBX-13), job de retención (KBX-12); ownership de cancel deduplicado en KBX-9. El orden de dependencias sin cambios y sólido; los criterios QA siguen concretos y testeables (concurrencia, ventana de caché, cascade, restitución).
**Hallazgo (menor):** `GET /public/universities` (grupo Public de §5; consumido por el wizard de registro de KBX-22) no está explícitamente reclamado por ningún ticket backend — trivialmente pequeño, pero asignarlo (KBX-6 es el hogar natural).

### 6. Correctitud de multi-tenancy y seguridad — 9/10
Los dos blockers previos aquí están corregidos estructuralmente: los filtros ahora tienen una columna sobre la que filtrar en cada tabla, con interceptor de stamp al insertar y tests negativos cross-tenant por tabla (KBX-4); la revocación tiene un mecanismo concreto (chequeo de status por request, caché 30 s, revocación de refresh-token en block/suspend) que hace implementables los criterios QA de KBX-5/6. La nullability de claims JWT ahora coincide con §4. La QA de key-handling de KBX-21 es técnicamente correcta (restricción referrer, bundle-visible reconocido); key del SDK mobile restringida por app id.
**Hallazgos (menores):** NEW-2 — el wording de la QA de KBX-6 debería reconocer la caché ≤30 s. La caché de status de 30 s es un trade-off deliberado de availability/consistency; bien, pero la historia de invalidación de caché en el middleware de auth (¿in-memory por instancia?) no está enunciada — irrelevante a escala single-instance App Runner, anotado por completitud.

### 7. Plataforma / config / entrega — 10/10
Ambos findings previos cerrados: las migraciones corren vía `Database.Migrate()` al arrancar el contenedor detrás de un flag de env (no hace falta path CI-to-private-RDS), y la configuración CORS para localhost + CloudFront está especificada (§5). La postura local-first, convenciones `.env`/dart-define, super admin seedeado, App Runner/ECR/RDS/S3+CloudFront, solo-artefacto-APK y el teardown documentado se mantienen. Sin findings.

### 8. Manejo de fallos — 9/10
La categoría que puntuó 4/10 queda transformada: política de fallo Directions con renderizado por superficie (B4), cascade de cancel del driver con 409 en in-progress (B3), restitución de asiento (G6), expiración de token a mitad de trip vía refresh proactivo (B2), pings offline descartados con QA airplane-mode (KBX-26), fallo de generación de export → 500 problem+json + toast (KBX-13), fallback GPS-denied y protección double-tap retenidos (KBX-25).
**Hallazgos (menores):** el comportamiento mobile cuando falla el propio `POST /auth/refresh` (refresh token expired/revoked a mitad de sesión) no está enunciado — presumiblemente forzar re-login; una línea en KBX-22. Si el chequeo "invalid origin coordinates" merecedor de 422 es una validación de rango server-side (debe serlo, ya que Directions puede estar caído) está implicado pero no enunciado en KBX-7.

### 9. Estrategia de testing vs herramientas mandatorias — 10/10
Ambos findings previos cerrados: la pantalla de tracking Maps está explícitamente excluida de Selenium y cubierta por casos manuales TestLink (§9, Description y QA de KBX-29), y MantisBT obtiene una instancia local docker-compose (§9, KBX-29). Las siete herramientas mandatorias siguen mapeadas a artefactos concretos con hooks de CI y coverage gates numéricos (70/70/60); el mandato de load Postman satisfecho vía perfil Collection Runner; thread groups JMeter con assertions p95 coinciden con la QA de performance de KBX-12. Sin findings.

### 10. Documentación y trazabilidad — 9/10
El re-mapeo del export "Super Admin" ahora se explica en §2 (I4); la convención de idioma de enums está documentada (I6); la ruptura de trazabilidad pantalla Settings ↔ schema está sanada (G7); los diagramas de secuencia siguen siendo endpoint-accurate, y los nuevos endpoints (`/auth/refresh`, `/sos/{id}/close`, mark-read, `/public/universities`) aparecen todos de forma coherente en §5 y sus flujos/tickets consumidores.
**Hallazgo (menor):** NEW-1 — los valores en español sobrevivientes `severity` y `period` son el único lugar donde la convención documentada y los artefactos documentados discrepan.

---

## Resumen de findings restantes

**Blockers:** ninguno.

**Gaps (todos menores, no bloqueantes):**
1. `GET /public/universities` no tiene ticket propietario — asignar a KBX-6.
2. Semántica de `GET /trips/mine` para requests pending (aún no accepted) sin especificar — afecta la next-trip card de PaxHome y el trigger del poll.
3. Comportamiento mobile ante fallo de refresh-token (forzar re-login) sin enunciar — una línea en KBX-22.
4. Flag `read` compartido en notificaciones dirigidas por rol cuando una universidad tiene múltiples admins — aclarar o aceptar.

**Inconsistencies (todas menores):**
1. (NEW-1) Convención English-enum vs `severity (alta/media/baja)` y `period=diario|…` — acotar la convención o convertir los valores.
2. (NEW-2) QA de KBX-6 "next authed call 403" vs la caché de status de 30 s — añadir "within ≤30 s".

---

## PUNTUACIÓN TOTAL: 93 / 100

| # | Category | v2 | v3 |
|---|---|---|---|
| 1 | Scope clarity | 8 | 10 |
| 2 | Data model completeness | 6 | 9 |
| 3 | API contract clarity | 6 | 9 |
| 4 | Data flow & rendering | 8 | 9 |
| 5 | Ticket completeness & orderability | 7 | 9 |
| 6 | Multi-tenancy & security correctness | 6 | 9 |
| 7 | Platform/config/delivery | 9 | 10 |
| 8 | Failure handling | 4 | 9 |
| 9 | Testing strategy vs mandated tools | 9 | 10 |
| 10 | Documentation & traceability | 8 | 9 |
| | **Total** | **71** | **93** |

## Veredicto

**Listo para ejecución.** Cada uno de los 21 findings previos está resuelto con citas verificables, y los fixes son internamente consistentes — el diseño de tenancy ahora tiene columnas detrás de sus filtros más stamp al insertar y tests negativos por tabla, la historia de auth está completa (refresh, rotación, revocación con un mecanismo de implementabilidad explícito), y los dos paths de fallo runtime de mayor impacto (fallo de Directions al publicar, cascade de cancelación del driver) están especificados de punta a punta hasta qué renderiza cada superficie de mapa y cómo observan el cambio los passengers. Las revisiones introdujeron solo una pequeña auto-contradicción (la convención English-enum blanket vs los valores en español sobrevivientes `severity`/`period`) y dejaron cuatro gaps de aclaración de una línea; ninguno afecta la buildability u ordenamiento de tickets, y todos se pueden corregir durante la implementación sin replanificar. El plan se puede entregar a implementers tal cual, con los seis ítems menores de arriba incorporados en KBX-2/6/13/22 a medida que se tomen.
