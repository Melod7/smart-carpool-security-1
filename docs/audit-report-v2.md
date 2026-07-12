# Auditoría de implementabilidad del plan v2 — Kubix UTN Smart Carpool Security Platform

Artefacto auditado: `smart-carpool-security/PLAN.md` (v2.0, rediseño multi-tenant)
Diseños de referencia verificados contra el fuente:
- Mobile: `../Alta fidelidad Smart Carpool/src/app/App.tsx` (1.334 líneas, verificado)
- Admin web: `../High-Fidelity  Super Admin Dashboard Wireframe/src/app/App.tsx` (1.413 líneas, verificado)

Puntuación: 10 categorías de rúbrica × 10 pts. Los findings citan secciones del plan (§) y tickets (KBX-n).

---

## 1. Claridad de alcance — 8/10

**Fortalezas.** §1/§11 trazan un límite v1 nítido: EcoTokens/gamificación solo-estáticos (§4 business rules, KBX-16, §11), sin chat (§7 "Ayuda shows support email only"), sin push, sin websockets, sin subida de documentos, sin browsing cross-campus. La regla de visibilidad del passenger en §2 ("destination campus equals assigned campus, v1") es explícita. Las decisiones de mock estático se repiten de forma consistente a nivel de ticket (widget XP KBX-16, KBX-23 "static verification/gamification sections").

**Hallazgos.**
- La tabla de roles de §2 otorga a super_admin "can impersonate read-only views of any tenant" — no hay endpoint (§5), no hay pantalla (§7 la consola super solo tiene universities/detail/stats) y ningún ticket lo implementa. Statement de alcance huérfano. (GAP 5)
- Ambos exports de Figma contienen **return trips con campus como origin** (admin `tripsData` VJ-4818 "Campus Norte → Av. Boyacá", VJ-4816; mobile `PAX_TRIPS` "UTN FRBA → Barrio Norte"). El modelo de dominio (§4 `trips.destination_campus_id`) solo puede representar trips hacia el campus, y §11 no declara los trips de vuelta a casa fuera de alcance. (INCONSISTENCY 2)
- El copy del overlay SOS mobile ("Tu ubicación fue compartida con... tus contactos de emergencia") promete notificación a emergency contacts, pero el flujo SOS del plan (§6.5, KBX-11, KBX-25) solo notifica a admins; los datos de `emergency_contacts` se recogen pero nunca se usan. El alcance debería indicar que los contactos son solo informativos en v1. (GAP 10)

## 2. Completitud del modelo de datos — 6/10

**Fortalezas.** §4 cubre 14 tablas con columnas clave, defaults sensatos en `university_settings`, unique(email, university_id) (QA KBX-2), enums de status por tabla, y un diagrama ER que coincide con la lista de tablas.

**Hallazgos.**
- **Contradicción de columna de tenancy (confirmada).** §2 afirma "Every tenant-scoped table carries `university_id`" y KBX-4 promete "EF global query filters on `university_id` for all tenant tables", pero en el schema de §4 `ride_requests`, `ratings`, `location_pings`, `vehicles` y `emergency_contacts` **no** tienen columna `university_id`. Los filtros como están especificados no se pueden aplicar a estas tablas; el plan debe o bien añadir la columna o especificar filtrado join-through-parent por tabla. (BLOCKER 1)
- **Sin almacenamiento de refresh-token.** KBX-3 menciona "60 min expiry + refresh token" pero §4 no tiene tabla `refresh_tokens` (ni columna token-version en `users`). (BLOCKER 2)
- El schema de `notifications` dice "recipient_role **or** recipient_id" — el diseño real de columnas (¿dos columnas nullable? ¿polimórfico?) está indefinido; KBX-2 no puede crear la entidad a partir de esto. (relacionado con GAP 8)
- `university_settings` carece de columnas para campos que la pantalla Settings (Figma `SettingsScreen`, KBX-20 "General + ... toggles (persisted)") muestra: timezone, support email, toggles de notificación SOS/block/weekly-report, toggle CO₂-tracking. Solo existen `sos_notifications_enabled` y `gamification_enabled`. (GAP 7)
- `location_pings` crece sin límite (una fila por participante cada 10 s por trip activo); no hay política de retención/pruning pese a que KBX-12 anota "history kept in table". (GAP 9)

## 3. Claridad del contrato de API — 6/10

**Fortalezas.** §5 agrupa la superficie completa, declara errores RFC 7807 y paginación basada en header; §1 ancla de forma inteligente los shapes de respuesta admin a los mock data del export (`usersData`, `tripsData`, `sosAlerts`, `auditLog`, `initialRequests`, `periodMeta` — todos verificados presentes en el export). Las secciones QA de los tickets fijan status codes concretos (403/404/409/422).

**Hallazgos.**
- **No hay `POST /auth/refresh`** (ni equivalente) en §5 pese al refresh token de KBX-3; la rotación, almacenamiento y semántica de revocación están totalmente sin especificar. (BLOCKER 2)
- **No hay close SOS del lado usuario.** El `SOSOverlay` mobile tiene "Estoy a salvo · Cerrar alerta"; §5 solo tiene el admin `POST /admin/sos/{id}/resolve`. O bien añadir `POST /sos/{id}/close` o especificar que el botón del overlay es solo dismiss de UI. (GAP 3)
- **No hay endpoint mark-read para notificaciones.** El export admin implementa `markRead`/`markAllRead` ("Marcar todo como leído") y `notifications.read` existe en §4, pero §5 solo expone `GET /admin/notifications`. (GAP 4)
- No hay endpoints de impersonación para la capability de super_admin de §2. (GAP 5)
- §5 dice que `GET /admin/dashboard` devuelve "KPI cards + active SOS + **XP mock**", mientras KBX-16 renderiza el widget XP "from static mock (v1)" en el client. Decidir de una vez server-provided vs client-static. (INCONSISTENCY 3)
- Los bodies request/response solo se implican (por shapes de Figma y diagramas de secuencia); aceptable a este nivel de planning pero no existe un apéndice DTO explícito.

## 4. Flujo de datos y renderizado — 8/10

**Fortalezas.** Los intervalos de polling están especificados por superficie y son consistentes entre secciones: SOS/tracking 10 s, notifications 30 s (§3), requests del driver 15 s (§6.3, KBX-24), cola de registros 30 s (§6.2), pings 10 s (§6.4, KBX-26). La matriz de visibilidad de tracking (§6.4) es explícita por rol y se reafirma en QA de KBX-12/KBX-26. Directions se llama server-side una vez por publish (§3, §12). Las elecciones TanStack Query `refetchInterval` y Riverpod/dio mapean limpiamente a los flujos.

**Hallazgos.**
- El intervalo de poll de `GET /trips/available` del passenger (lista PaxHome) nunca se especifica. (menor)
- La QA de KBX-24 dice que aceptar un request "notifies passenger's poll", pero la app mobile no tiene endpoint de notificaciones (§5 `GET /admin/notifications` es solo admin) — el passenger presumiblemente observa vía `GET /trips/mine`; el icono de campana de PaxHome en el diseño no tiene feed de respaldo. Indicar el mecanismo. (GAP 8)
- Qué se renderiza en el mapa del trip del passenger cuando el trip está `scheduled` (antes de que existan pings) vs `in_progress` no se describe; KBX-26 solo cubre el loop in-progress.

## 5. Completitud y ordenabilidad de tickets — 7/10

**Fortalezas.** 30 tickets, cada uno con Title/Description/QA según la restricción content-only. El orden de dependencias es sólido: scaffolding (KBX-1) → schema (KBX-2) → auth (KBX-3) → tenancy (KBX-4) → dominios backend (KBX-5–13) → web (KBX-14–21) → mobile (KBX-22–26) → QA (KBX-27–29) → deploy (KBX-30). Los criterios QA son inusualmente concretos (p. ej. test de concurrencia KBX-8 sobre el último asiento; KBX-4 "404, not 403 leak").

**Hallazgos.**
- **Endpoints huérfanos (confirmados).** `GET/PUT /me/profile` y `GET/POST/DELETE /me/emergency-contacts` (grupo Profile de §5) **no tienen ticket backend**. KBX-7 solo cubre `/me/vehicle`; KBX-23 (mobile) afirma "contacts CRUD round-trips" contra una API que nada construye. Añadirlos a KBX-6 o a un ticket nuevo. (GAP 1)
- Ningún ticket implementa la regla de auto-suspensión `min_driver_rating`, pese a la columna de settings (§4), la fila de la pantalla Settings ("Conductores bajo este promedio son suspendidos") y el mock de notificación del export ("bloqueada por calificación menor a 3.0"). O bien añadir enforcement a KBX-10 o declararlo config estática en v1. (GAP 2)
- Ningún ticket para impersonación (GAP 5) ni mark-read de notificaciones (GAP 4).
- KBX-9 y KBX-7 ambos reclaman `POST /trips/{id}/cancel` (KBX-7: lógica de ventana de cancelación; KBX-9: lifecycle) — ownership solapado; asignar el endpoint a un solo ticket.

## 6. Correctitud de multi-tenancy y seguridad — 6/10

**Fortalezas.** Decisión single-DB/shared-schema con claims JWT (§2), `ITenantContext` + global query filters con bypass explícito de super_admin (KBX-4), QA anti-enumeración 404-over-403, tests negativos de tenancy por endpoint mandatorios (KBX-28, §12), BCrypt, flujo `must_change_password`, password temporal mostrado una vez (KBX-5), y Directions server-side manteniendo esa key fuera de los clients (§3).

**Hallazgos.**
- El mecanismo de query-filter está **roto como está especificado** para las cinco tablas sin `university_id` (ver Data model). El aislamiento de tenancy para ride_requests/ratings/pings/vehicles/emergency_contacts actualmente no se apoya en nada. (BLOCKER 1)
- **Revocación indefinida (confirmada).** La QA de KBX-6 exige "block mid-session → next authed call 403" y KBX-5 exige que suspend-university bloquee a los usuarios, pero los JWT son stateless con expiración de 60 min y no existe diseño de revocación (no se especifica chequeo de status por request, denylist ni claim token-version). Los criterios QA actualmente son inimplementables tal como están escritos. (BLOCKER 2)
- §2 dice que los claims JWT `university_id`, `campus_id` son "null for super_admin", pero §4 también permite `campus_id` null para university_admin — la spec de claims debería coincidir.
- La QA de KBX-21 "map key loaded from env not bundle" es técnicamente incorrecta para la API Maps **JS**: una env var de build-time igual viaja en el bundle del browser. El control real es la restricción HTTP-referrer de la key; §3 protege correctamente solo la key de Directions. Reformular para evitar una asunción de seguridad falsa. (INCONSISTENCY 5)

## 7. Plataforma / config / entrega — 9/10

**Fortalezas.** Local-first respetado (§8: docker-compose Postgres+API+pgAdmin, Vite dev, `--dart-define` para networking del emulador incl. `10.0.2.2`), `.env.example` en raíz y por app, super admin seedeado vía env. La postura AWS test-account encaja con las restricciones: App Runner + ECR, RDS t4g.micro, S3+CloudFront, solo artefacto APK (sin store), pipelines GitHub Actions PR/main, y — notablemente — un **teardown** documentado para evitar cargos (QA KBX-30). §12 mitiga la facturación de Maps.

**Hallazgos.**
- El paso de migración RDS ("with migration step", KBX-30) no dice cómo las migraciones alcanzan un RDS privado en VPC desde CI (¿embebido en el startup de App Runner? ¿runner en VPC?). Una frase lo resolvería.
- No se menciona la configuración CORS entre el origin web CloudFront y la API App Runner (necesaria el día uno).

## 8. Manejo de fallos — 4/10

**Fortalezas.** RFC 7807 en todas partes (§5); paths negativos por ticket: wrong-domain 422 (KBX-6), transiciones inválidas 409 (KBX-9), request duplicado 409 y race de concurrencia de asientos (KBX-8), non-participant 403 / ping en trip no active 409 (KBX-12), fallback GPS-permission-denied y protección double-tap (KBX-25), estados loading/error en tests RTL (KBX-16), empty state en el mapa de tracking (KBX-21).

**Hallazgos.**
- **Fallo de Google Directions (confirmado ausente).** KBX-7 solo mockea Directions en tests. No hay comportamiento especificado para agotamiento de quota, timeout, `ZERO_RESULTS` u origin inválido al publicar: ¿`POST /trips` falla (qué status?), reintenta, o publica sin polyline (y entonces qué renderizan las tres superficies de mapa)? El "static map fallback documented" de §12 aborda facturación, no esto. (BLOCKER 4)
- **Cancelación del driver con passengers accepted (confirmado ausente).** `POST /trips/{id}/cancel` existe (KBX-7/KBX-9) y el diseño tiene "Cancelar ruta publicada", pero nada especifica el cascade: qué pasa con los ride_requests `accepted` (¿cambio de status?), cómo se enteran los passengers (sin canal de notificación mobile), y si cancelar un trip `in_progress` es legal. (BLOCKER 3)
- Cancel del passenger `POST /requests/{id}/cancel` tras acceptance: la restitución de asiento (`seats_available +1`) no está especificada — KBX-8 solo especifica el decremento en accept. (GAP 6)
- JWT de 60 min vs trips largos: un driver a mitad de trip cuyo token expira pierde capacidad de ping; sin el diseño de refresh (BLOCKER 2) esto es un modo de fallo en vivo.
- Sin guía offline/poll-failure para mobile (pérdida de red durante trip in_progress: ¿encolar pings? ¿descartar?), y sin path de error para fallos de generación de export de reportes (KBX-13/18).

## 9. Estrategia de testing vs herramientas mandatorias — 9/10

**Fortalezas.** Cada herramienta mandatoria se mapea a un artefacto concreto y hook de CI (§9, KBX-27–29): colección **Postman** + Newman en CI + perfil de load Collection-Runner (satisfaciendo el mandato "Postman load test") (`qa/postman/`, KBX-28); **JMeter** `qa/jmeter/kubix.jmx` con thread groups nombrados, 100 threads/10 s ramp, assertions p95 (<500 ms API, <200 ms tracking — coincidiendo con QA KBX-12); flujos **Selenium** Python+pytest enumerados y corridos headless contra docker-compose en CI (KBX-29); **SonarCloud** 3 proyectos con quality gates en PR; **TestLink** XML importable + matriz de trazabilidad ticket↔test-case; doc de workflow **MantisBT** con convenciones de severity/lifecycle/link; **SAST** vía Semgrep + reglas de seguridad Sonar + tres dependency audits en un job `security-scan` que falla en high severity. Los coverage gates son numéricos por stack (70/70/60) con enforcement en CI (KBX-27).

**Hallazgos.**
- Selenium contra una pantalla de tracking con Google Maps (KBX-21) es propenso a flakes e intesteable sin una key en CI; la lista de la suite omite/debería excluir explícitamente la pantalla de mapa o definir un stub.
- MantisBT es solo documentación (workflow doc) — aceptable, pero indicar si se levanta una instancia real para el proyecto.

## 10. Documentación y trazabilidad — 8/10

**Fortalezas.** Los cinco diagramas de secuencia (§6.1–6.5) se verificaron contra §5: cada endpoint invocado en un diagrama existe en la superficie de API, y los flujos coinciden con la QA de los tickets (p. ej. poll admin 30 s de 6.2 ↔ KBX-17; poll driver 15 s de 6.3 ↔ KBX-24). **Inventario de pantallas verificado contra los exports reales:** el nav admin tiene exactamente las cinco pantallas que lista el plan (Panel de Control / Gestión de Usuarios / Reportes de Viajes / Auditoría de Seguridad / Configuración) y todos los shapes de mock-data citados existen; los nombres de componentes mobile están todos verificados reales (`PaxHome/PaxTrips/PaxProfile/PaxHelp`, `DrvHome/DrvTrips/DrvProfile/DrvHelp`, `PublishModal`, `SOSOverlay`, `FooterNav`); los hex de la paleta coinciden exactamente con los tokens del export (#003087/#C8102E/#2E7D32/#F9A825/#F0F4FA; sidebar #0F172A vía `--sidebar`, primary #1D4ED8, Inter + DM Mono). Las pantallas nuevas (login/register, trip map, `/admin/tracking`, `/super/*`) están correctamente marcadas "(new)". El patrón de filename en KBX-18 coincide con los filenames de `periodMeta`. Matriz de trazabilidad TestLink planificada.

**Hallazgos.**
- El export admin se titula y personifica como **"Super Admin"** (sidebar: "Fernando López · Super Admin"), pero el plan mapea las cinco pantallas a `university_admin` e inventa una consola super separada. Decisión correcta, pero el plan nunca comenta la discrepancia — un implementer que diffee contra Figma se confundirá. Añadir una frase. (INCONSISTENCY 4)
- Los valores de status/type del diseño están en español (`completado/cancelado/programado/en_curso`, audit type `sistema`) mientras los enums de §4 están en inglés (`scheduled/in_progress/...`, type `system`); la convención de mapping/i18n-of-enums no está documentada pese a que §11 mantiene la UI en español. (INCONSISTENCY 6)
- La pantalla Settings documenta campos que el schema no puede persistir (ver Data model / GAP 7) — una ruptura de trazabilidad entre el inventario de pantallas de §7 y §4.

---

## BLOQUEADORES

1. **Contradicción de schema de tenancy (§2 + KBX-4 vs §4).** §2 manda `university_id` en cada tabla con scope de tenant y KBX-4 construye global query filters sobre esa columna, pero `ride_requests`, `ratings`, `location_pings`, `vehicles` y `emergency_contacts` carecen de ella en §4. **Fix:** o bien añadir `university_id` a esas cinco tablas (y a las migraciones/seeds de KBX-2) o reescribir KBX-4 para especificar expresiones de filtro por tabla que joineen a través de `trips`/`users`, y actualizar la QA de KBX-4 para cubrir estas cinco tablas explícitamente.
2. **Diseño de refresh-token y revocación ausente.** KBX-3 dice "60 min expiry + refresh token" pero no hay `POST /auth/refresh` en §5, no hay almacén de tokens en §4, no hay política de rotación/expiración — y la QA de KBX-6 ("block mid-session → next authed call 403") y KBX-5 (suspend university locks users out) son inimplementables con JWTs stateless como están especificados. **Fix:** añadir una tabla `refresh_tokens` (user_id, hash, expires_at, revoked_at), un endpoint `POST /auth/refresh` con rotación, y especificar el mecanismo de revocación para block/suspend (p. ej. chequeo de user-status por request en el middleware de auth, o un claim token-version chequeado contra `users`); actualizar Description/QA de KBX-3 en consecuencia.
3. **Cascade de cancelación del driver indefinido.** `POST /trips/{id}/cancel` (KBX-7/KBX-9) con passengers accepted: especificar que los ride_requests accepted/pending pasan a un status definido (p. ej. `cancelled_by_driver`), cómo lo observan los passengers (vía poll `GET /trips/mine` — indicarlo), si cancelar `in_progress` está permitido (recomendar 409), y las escrituras de audit/notification. Añadir criterios QA a KBX-9.
4. **Path de fallo de Google Directions indefinido.** Especificar el comportamiento de `POST /trips` en timeout/quota/ZERO_RESULTS/origin inválido de Directions: recomendado — fallar con 502/422 problem+json y un flag retryable, o persistir el trip con polyline null + fallback de distancia en línea recta, y definir qué renderizan el mapa del passenger (KBX-26), el mapa del driver y el tracking admin (KBX-21) cuando `polyline` es null. Añadir criterios QA a KBX-7 y KBX-21/26.

## BRECHAS

1. **`GET/PUT /me/profile` y `GET/POST/DELETE /me/emergency-contacts` no tienen ticket backend** (grupo Profile de §5; consumidos por la QA de KBX-23 "contacts CRUD round-trips"). Añadirlos a KBX-6 o crear KBX-6b con QA (tenancy, validación, límite de contactos).
2. **`min_driver_rating` se almacena pero nunca se enforcea.** La pantalla Settings dice que los drivers bajo el umbral "son suspendidos" y el mock de notificaciones del export muestra auto-blocking. Añadir enforcement (p. ej. en el recompute de rating_avg de KBX-10: auto-set `status=blocked` + audit event + notification) o declarar explícitamente la auto-suspensión fuera de alcance en §11.
3. **No hay endpoint de close SOS del lado usuario** para el "Estoy a salvo · Cerrar alerta" del overlay. Añadir `POST /sos/{id}/close` (solo owner, solo active) o indicar en KBX-25 que el botón solo dismisses el overlay mientras la alerta sigue active para admins.
4. **No hay endpoint mark-read de notificaciones** pese a `notifications.read` (§4) y la UI markRead/markAllRead del export. Añadir `PUT /admin/notifications/{id}/read` y `PUT /admin/notifications/read-all` a §5/KBX-13; añadir QA a KBX-14 (el badge decrementa).
5. **La impersonación read-only de super-admin (§2) está sin implementar** — sin endpoint, pantalla ni ticket. O bien borrar la frase de §2 o añadir endpoints (`GET /super/universities/{id}/dashboard` etc.) más una fila de pantalla en §7 y un ticket.
6. **Restitución de asiento en cancel del passenger sin especificar.** KBX-8 define el decremento solo en accept; especificar `seats_available +1` cuando se cancela un request accepted (y si se reactivan requests previamente auto-rechazados — recomendar que no), con un criterio QA.
7. **`university_settings` sin columnas para campos de la pantalla Settings:** timezone, support_email, notify_block, notify_weekly_report, co2_tracking_enabled (secciones de pantalla "Información General" y "Notificaciones y Alertas", KBX-20). Añadir columnas en §4/KBX-2 o recortar el alcance de KBX-20 al subconjunto persistido.
8. **Canal de notificación mobile indefinido.** PaxHome muestra un icono de campana; la QA de KBX-24 dice que accept "notifies passenger's poll" — pero no existe endpoint de notificaciones orientado a mobile. Especificar que mobile deriva el estado solo de polls `GET /trips/mine`/`GET /trips/available` (y quitar la campana), o añadir `GET /me/notifications`.
9. **Retención de `location_pings` sin abordar.** A intervalos de 10 s esta tabla domina el crecimiento. Especificar retención (p. ej. borrar pings N días tras la completion del trip, o conservar solo last-per-user tras la completion) y añadirla a KBX-12.
10. **Los emergency contacts se recogen pero no se usan, mientras el copy del overlay SOS afirma que se notifican.** Adaptar el texto del overlay en KBX-25 (solo "seguridad notificada") o anotar en §11 que la notificación de contactos (SMS/call) está fuera de alcance; actualmente el plan embarca UX de seguridad engañosa.

## INCONSISTENCIAS

1. **§2 "every tenant-scoped table carries `university_id`" vs schema de §4** (cinco tablas sin ella) — misma causa raíz que BLOCKER 1; corregir ambas afirmaciones juntas para que §2, §4 y KBX-4 coincidan.
2. **Los return trips existen en ambos datasets de diseño pero no en el modelo de dominio.** Admin `tripsData` (VJ-4816, VJ-4818) y mobile `PAX_TRIPS` incluyen trips con origin en campus; §4 `trips` solo soporta destinos campus, y §11 omite los trips de vuelta a casa. Añadir "trips originating from campus (return rides)" a §11 y asegurar que los seeds de KBX-2 no contengan trips con origin en campus (o extender el modelo).
3. **Ownership del XP mock:** §5 dice que `GET /admin/dashboard` devuelve "XP mock" (server), KBX-16 lo renderiza "from static mock" (client). Elegir uno — recomendar client-static y borrar "+ XP mock" de §5.
4. **El export Figma admin se titula/personifica "Super Admin"** (persona del sidebar "Fernando López · Super Admin") pero el plan asigna sus cinco pantallas a `university_admin` (§7). Decisión correcta; añadir una frase a §1 o §7 anotando el re-mapeo deliberado para que los implementers no malinterpreten el export.
5. **QA de KBX-21 "map key loaded from env not bundle"** — una env var de Vite se compila en el bundle JS embarcado; la key Maps JS siempre es visible en el client y debe protegerse con restricción HTTP-referrer. Reformular la QA de KBX-21 (p. ej. "key injected via env and restricted by referrer") para coincidir con la afirmación (correcta) de §3, que solo cubre la key de Directions.
6. **Mapping de idioma de enums sin documentar.** Los valores del diseño están en español (`programado/en_curso/completado/cancelado`; audit type `sistema`), los enums de §4 en inglés (`scheduled/in_progress/...`; `system`); como §11 mantiene la UI en español, añadir una convención de una línea (enums en inglés en DB/API, labels en español en clients) a §5.
7. **Ventana de rating del passenger:** KBX-10 deja a los passengers calificar "anytime after completion", pero las business rules de §4 solo listan la ventana de 12 h del driver, dejando la regla del passenger enunciada en un solo lugar. Enunciar la ventana del passenger (o su ausencia) en §4 para que KBX-10 y las reglas de dominio coincidan.

---

## PUNTUACIÓN TOTAL: 71 / 100

| # | Category | Score |
|---|---|---|
| 1 | Scope clarity | 8 |
| 2 | Data model completeness | 6 |
| 3 | API contract clarity | 6 |
| 4 | Data flow & rendering | 8 |
| 5 | Ticket completeness & orderability | 7 |
| 6 | Multi-tenancy & security correctness | 6 |
| 7 | Platform/config/delivery | 9 |
| 8 | Failure handling | 4 |
| 9 | Testing strategy vs mandated tools | 9 |
| 10 | Documentation & traceability | 8 |
| | **Total** | **71** |

## Veredicto

El plan es estructuralmente sólido — el desglose de tickets está bien ordenado con criterios QA genuinamente testeables, la suite de herramientas QA mandatorias está mapeada de forma completa y concreta, el deployment respeta las restricciones local-first/AWS-test-account incluyendo teardown, y el inventario de pantallas y los diagramas de secuencia cuadran contra los exports reales de Figma hasta nombres de componentes, shapes de mock-data y tokens hex. Sin embargo, **no está listo para ejecución** hasta resolver los cuatro blockers: el diseño de tenancy-filter se contradice con su propio schema (cinco tablas no se pueden filtrar como se especifica, que es la garantía de seguridad central de la plataforma), la historia de auth referencia refresh tokens y revocación a mitad de sesión que nada define ni almacena (haciendo varios criterios QA escritos inimplementables), y los dos paths de fallo runtime de mayor impacto — errores de Google Directions al publicar y cancelación del driver con passengers accepted — no tienen comportamiento especificado. Los cuatro son fixes contenidos (columnas de schema o reescrituras de filtro, una sección de auth, dos párrafos de failure-path más líneas QA); con ellos más los diez gaps triados (como mínimo GAPS 1, 6 y 8, que afectan directamente la buildability de tickets), el plan puntuaria en los mid-to-high 80s y sería seguro entregarlo a implementers.
