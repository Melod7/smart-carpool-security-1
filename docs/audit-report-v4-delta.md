# Auditoría de implementabilidad del plan v4 — DELTA (v2.1 → v2.2)

Artefacto auditado: `smart-carpool-security/PLAN.md` (v2.2)
Baseline: `docs/audit-report-v3.md` — 93/100 en v2.1, cero blockers, seis ítems menores.
Alcance de esta auditoría: SOLO el delta v2.2 y su integración — (a) definiciones de roles §2 + matriz de permisos, (b) reglas funcionales de EcoTokens §4 + KBX-31 + las edits en cascada a KBX-2/9/10/13/16/20/23/24 y §5/§6.4/§7/§11. El contenido v2.1 sin cambios se re-leyó por contradicciones pero no se re-puntuó.

---

## Parte 0 — Chequeo de carry-over del baseline

La línea de status de v2.2 afirma que los seis ítems menores del auditor de v3 están incluidos. Verificado — los seis están genuinamente corregidos:

| v3 item | Where fixed in v2.2 |
|---|---|
| `GET /public/universities` no tenía ticket propietario | KBX-6: "Also owns `GET /public/universities` (registration picker)" |
| Semántica de `/trips/mine` para requests pending | §4 última business rule: pending requests incluidos, "next trip" card = first accepted, poll trigger definido |
| Comportamiento mobile ante fallo de refresh-token | QA KBX-22: "refresh-token failure (expired/revoked) forces re-login with a friendly message" |
| Flag `read` compartido con múltiples admins | §4 `notifications`: "share one read flag across a university's admins — accepted v1 simplification" |
| NEW-1 convención enum vs `severity`/`period` | §4: severity ahora `high/medium/low` (renderizado alta/media/baja); `period` documentado como la única excepción en español |
| NEW-2 ventana de caché QA KBX-6 | QA KBX-6: "403 within ≤30 s (KBX-3 status-check cache window)" |

---

## Parte 1 — Findings por área

### Área A — Definiciones de roles y matriz de permisos (§2)

**Consistente.** La tabla de cuatro roles, paths de creación de cuentas y la regla un-rol-por-cuenta coinciden con §5 (los grupos de rutas coinciden con la columna Client), §6.1/6.2 (flujos de provisioning y aprobación), §11 (ambos-roles-en-una-cuenta y rol security-guard listados fuera de alcance), KBX-2 (logins seedeados "for all 4 roles"), KBX-3 (claims por rol) y KBX-5 (password temporal + cambio forzado). Las filas de la matriz cross-chequean limpiamente contra el plan: las celdas de rating codifican la ventana asimétrica exactamente como §4/KBX-10 ("Yes (passengers, ≤12 h)" / "Yes (driver)"); el split SOS (resolve = UA, close-own = mobile) coincide con KBX-11; el browse del passenger "(own campus)" coincide con la regla de tenancy §2 y KBX-8; publish/accept solo-driver coincide con KBX-7/8; "Earn EcoTokens: driver/passenger only" coincide con §4.

**Defectos encontrados:** la celda de live-tracking de super_admin otorga una capability que nada implementa (INC-1); la matriz afirma que KBX-4 la consume, pero las policies y QA de KBX-4 no se actualizaron para enforcear o verificar el split driver-vs-passenger (GAP-7); el "No" de SA en audit log codifica un huérfano preexistente para audit events a nivel plataforma (INC-3).

### Área B — Completitud e implementabilidad de las reglas EcoTokens (§4, KBX-31)

**Núcleo sólido.** Diseño ledger-first con balance desnormalizado actualizado en la misma transacción; montos de earning, umbrales de level y límites QA de KBX-31 (100/500/2000) son mutuamente consistentes; el canje está limpiamente excluido (§4, §7, KBX-23, §11 todos dicen "próximamente"); la idempotencia vía unique(user_id, type, source_id) funciona para los dos tipos de trip-completion (source = trip_id) y el tipo rating (source = rating_id, doblemente protegido por el 409 de KBX-10 en doble rating).

**Defectos encontrados:** el enum `type` del ledger no puede representar la penalty −5 (BLOCKER-1); el invariante una-vez-por-semana de la racha no está realmente enforceado por la unique key enunciada y racea bajo concurrencia (BLOCKER-2); el timezone de cada ventana de tiempo no está especificado pese a que existe `university_settings.timezone` (GAP-1); la semántica floor-clamp del monto del ledger indefinida (GAP-2); la fuente de conteo de racha a mitad de semana con gamificación off ambigua (GAP-3); mobile no tiene forma especificada de conocer el toggle (GAP-4); math de progress indefinido en Platino (GAP-5); default de `gamification_enabled` sin enunciar (GAP-8).

### Área C — Barrido de contradicciones cross-plan (referencias stale)

**Limpio.** Se buscó cada referencia "static", "mock", XP y contrato del dashboard:

- No sobrevive texto stale "EcoTokens are static/client-static". La nota de §4 y la nota de §5 ambas dicen ahora que el widget xpByCareer es datos reales y nombran explícitamente la decisión reemplazada; KBX-13 lo obtiene "from KBX-31's ledger"; KBX-16 lo renderiza "from the API's `xpByCareer` data". La frase vieja de v2.1 "`GET /admin/dashboard` does not return XP data" desapareció.
- Las ocurrencias restantes de "static" no están relacionadas y son correctas: hosting estático S3 (§3), la sección "static verification/compliance" de PaxProfile/DrvProfile (verificación de identidad, deliberadamente estática según el pipeline de documentos fuera de alcance de §11), y el fallback de riesgo static-map (§12).
- §6.4 se extendió correctamente (awards ECT en complete, incluido el chequeo de racha); §11 estrecha correctamente la exclusión solo al canje mientras accrual/levels están en alcance; las filas mobile de §7 apuntan a `GET /me/eco`; la QA de KBX-20/23 cubre el toggle de punta a punta (el accrual se detiene + estado disabled del widget + mobile oculta widgets).

**Defectos encontrados:** solo el pliegue de ordenamiento de tickets (INC-2) y la dependencia de seed-data de semana actual de la QA de KBX-16 (GAP-6).

### Área D — Cobertura de tickets del nuevo alcance

**Completa, con los ítems de abajo.** Cada artefacto nuevo tiene propietario: schema + unique key de idempotencia + seeds consistentes (KBX-2), engine + `GET /me/eco` + agregación xpByCareer + slot de ejecución explícito (KBX-31, "after KBX-10, before KBX-13"), hooks de completion/cancel (KBX-9), hook de rating (KBX-10), payload del dashboard (KBX-13), widget (KBX-16), toggle (KBX-20), consumidores mobile (KBX-23/24). Sin endpoints huérfanos: `GET /me/eco` es el único endpoint nuevo y KBX-31 lo posee. Sin reglas huérfanas excepto las de dentro de BLOCKER-1/2 y GAP-1..5.

---

## Parte 2 — Findings numerados

### BLOQUEADORES

1. **El ledger no puede registrar la late-cancel penalty −5.** El `eco_token_transactions.type` de §4 enumera exactamente cuatro valores (`trip_completed_driver/trip_completed_passenger/rating_submitted/weekly_streak`) — no hay tipo para la penalty que la tabla de earning de §4 y KBX-9/KBX-31 mandan, así que la transacción de penalty viola el propio schema del plan y no tiene unique key de idempotencia (un late cancel reintentado podría double-deduct). **Fix:** añadir `late_cancel_penalty` al enum type con `source_id = trip_id`; actualizar la fila de tabla de §4, KBX-2 (migración) y KBX-31 (Description + una línea QA replay-is-no-op para la penalty).

2. **"Max once per user per week" de la racha no está enforceado por el mecanismo enunciado.** §4 define `source_id` como "trip_id or rating_id", así que la unique key de la fila de racha es (user_id, `weekly_streak`, trip_id) — lo que permite múltiples awards de racha en una semana con distintos trip_ids. Concretamente: dos trips que completan casi a la vez pueden cada uno contar 4 completions previas, ambos decidir "esta es la 5.ª", y ambos insertar (source_ids distintos, sin violación de constraint). La afirmación central del plan de que "the unique key makes replays no-ops" es falsa para este tipo de evento. **Fix:** para `weekly_streak`, definir `source_id` como la week key canónica (p. ej. string ISO year-week calculado en el timezone de la universidad — ver GAP-1), de modo que unique(user_id, weekly_streak, week_key) enforcee una-vez-por-semana a nivel de base de datos independientemente de la concurrencia; actualizar el paréntesis de source_id de §4 y KBX-31.

### BRECHAS

1. **Timezone de cada ventana de tiempo ECT sin especificar.** La semana de racha lun–dom, la ventana "semana actual" xpByCareer del dashboard y el filtro "ECT hoy" del driver todos necesitan un límite día/semana, y `university_settings.timezone` existe pero nunca se referencia en la sección EcoTokens. También sin enunciar: si un trip se agrupa en una semana por `completed_at` o `departure_at`. **Fix:** una frase en §4 ("all ECT day/week windows use `university_settings.timezone`; trips bucket by `completed_at`") + afirmarlo en la QA de KBX-31.
2. **Semántica floor-clamp del ledger indefinida.** Cuando el balance de un driver es, digamos, 2 en late cancel: ¿el monto del ledger es −5 (entonces la suma del ledger ≠ eco_balance, rompiendo la reconciliación y cualquier repair recompute-from-ledger) o −min(5, balance) = −2? Relacionado: ¿`xpByCareer` suma montos netos (penalties incluidas) o solo earnings positivos ("Puntos … earned" sugiere solo-positivos)? **Fix:** registrar el monto clamped (−min(5, balance)) para que `eco_balance` siempre iguale la suma del ledger, y enunciar que xpByCareer suma solo montos positivos (o net — pero elegir uno) en §4 y KBX-31.
3. **Edge de racha a mitad de semana con gamificación off ambiguo.** Si el toggle está off parte de una semana y se re-habilita, ¿los trips completed mientras estaba off cuentan hacia la racha de 5 trips? Contar desde la tabla `trips` dice sí; contar filas del ledger `trip_completed_*` dice no. **Fix:** enunciar la fuente de conteo — recomendar filas del ledger, consistente con "no accrual while off" y con el diseño week-key de BLOCKER-2.
4. **Mobile no puede conocer `gamification_enabled`.** La QA de KBX-23 exige "gamification-off university hides ECT widgets", pero ni `GET /me` ni `GET /me/eco` están especificados para exponer el flag (los admins lo obtienen vía `/admin/settings`; mobile no tiene endpoint de settings), y el comportamiento de `GET /me/eco` cuando el toggle está off no está enunciado. **Fix:** añadir `gamificationEnabled` a la respuesta de `GET /me/eco` (devolviéndolo con el balance congelado cuando está off), anotado en §5 y KBX-31.
5. **Progress de level indefinido en el nivel superior.** `GET /me/eco` devuelve "progress to next level", pero Platino no tiene siguiente nivel. **Fix:** definirlo (null o 100% fijo en Platino, y la fórmula `(eco_lifetime − floor) / (next_floor − floor)` para los demás) en §4 o KBX-31; su QA ya promete "progress math covered by unit tests" así que el math esperado debe quedar escrito.
6. **La QA de KBX-16 depende de seed data de la semana actual que KBX-2 no manda.** "XP bars match seeded ledger sums" solo es testeable si las transacciones ECT seedeadas caen dentro de la semana actual; fechas de seed estáticas dejan el widget legítimamente vacío. **Fix:** los seeds de KBX-2 deben fechar algunos trips completed + sus filas de ledger relativos a "ahora" (dentro de la semana lun–dom actual).
7. **La matriz de permisos dice que es "consumed by KBX-4", pero KBX-4 no se actualizó.** Su lista de policies (`SuperAdminOnly`, `UniversityAdmin`, `MobileUser`) agrupa driver y passenger mientras la matriz los separa en seis filas, y ninguna QA de ticket barre las celdas negativas de la matriz más allá del "non-drivers get 403" de KBX-7 (p. ej. un driver llamando `GET /trips/available` o `POST /trips/{id}/requests`, un passenger llamando `POST /requests/{id}/accept`, un UA llamando `POST /trips` están todos sin testear). **Fix:** añadir policies `DriverOnly`/`PassengerOnly` a la Description de KBX-4 y un barrido de tests de autorización negativa guiado por la matriz a su QA.
8. **`gamification_enabled` no tiene default enunciado.** Settings hermanos llevan defaults (6 / 3.5 / 0.21); este no, y sin embargo los seeds de KBX-2 y la regla no-op de KBX-31 dependen de él. **Fix:** enunciar `default true` en §4.

### INCONSISTENCIAS

1. **La matriz otorga a super_admin una capability de live-tracking que nada implementa.** "Live tracking map (all trips): super_admin — Yes (all tenants)" (y el "super_admin sees all tenants" de §6.4) no tiene endpoint (`GET /admin/tracking/active` es policy UA, con scope de tenant por un JWT `university_id` que es null para SA), no tiene pantalla (el inventario `/super/*` de §7 es solo universities/detail/stats) y no tiene ticket (la QA de KBX-12 testa passenger/driver/admin; KBX-21 es la pantalla UA). **Fix (recomendado):** cambiar la celda de la matriz a "No (v1)" y recortar la matriz de visibilidad de §6.4 — coherente con que la impersonación de tenants está fuera de alcance; alternativamente añadir un endpoint `/super/tracking` + pantalla + ticket, que es alcance nuevo.
2. **KBX-9/KBX-10 "invoke the KBX-31 engine" que se construye después de ellos.** KBX-31 ejecuta explícitamente "after KBX-10, before KBX-13", así que en el momento de build de KBX-9/10 el engine no existe; sus Descriptions tal como están escritas son inimplementables en secuencia (su QA está correctamente libre de ECT, así que solo el wording engaña). **Fix:** una línea en KBX-31 — "retrofits the accrual/penalty hooks into the KBX-9/10 endpoints" — o hacer que KBX-9/10 llamen a un stub no-op `IEcoTokenEngine` que KBX-31 reemplaza.
3. **(preexistente, ahora codificado por la matriz)** "Dashboard, reports + export, audit log, settings: super_admin — No" hace que los audit events a nivel plataforma (`audit_events.university_id = null`, §4) no sean legibles por nadie: la vista audit-log de UA está filtrada por tenant y `/super/*` no tiene endpoint de audit. No introducido por v2.2, pero la nueva matriz convierte una omisión en un "No" explícito. **Fix:** o bien enunciar que los audit events null-tenant son solo escritura en v1 (aceptado) o eliminar el caso null-tenant.

---

## Parte 3 — Veredicto del delta

**DEGRADA — el plan cae por debajo del baseline 93/100 hasta que se incorporen los findings de arriba; estimado 90/100 tal como está escrito.**

Deltas de rúbrica (v2.1 → v2.2, solo categorías que toca el delta):

| Category | v3 | v4 | Why |
|---|---|---|---|
| Data model completeness | 9 | 8 | BLOCKER-1 (el enum no puede contener la penalty), BLOCKER-2 (la unique key no entrega la garantía una-vez-por-semana afirmada), GAP-8 |
| API contract clarity | 9 | 9 | `GET /me/eco` bien ubicado; GAP-4/5 son pequeños pero el baseline ya cargaba ítems de peso equivalente ahora corregidos |
| Data flow & rendering | 9 | 8 | GAP-1 (timezone para semana/"hoy"), GAP-2 (reconciliación), GAP-3 (toggle a mitad de semana) |
| Ticket completeness & orderability | 9 | 9 | La cobertura está completa (slot KBX-31 explícito); GAP-6/7 e INC-2 son a nivel wording |
| Multi-tenancy & security | 9 | 9 | La matriz es una ganancia neta; GAP-7/INC-1 son issues de verificación de enforcement y documentación, no defectos de aislamiento |
| Documentation & traceability | 9 | 9 | El barrido de referencias stale está limpio; INC-1/INC-3 no restan más allá de las categorías de arriba |

Las dos adiciones son direccionalmente buenas — la matriz de permisos cierra un agujero real de documentación y el rediseño de EcoTokens es la arquitectura correcta (ledger + agregados desnormalizados + idempotencia a nivel DB). Pero v2.2 introduce las primeras auto-contradicciones genuinas desde v2.0: el schema del ledger no puede representar una transacción que la misma sección manda, y la afirmación central de idempotencia del plan es falsa para el evento de racha. Ambos son fixes de una línea sin ripple (añadir un valor de enum; redefinir un source_id), y los ocho gaps son cada uno una frase. Con BLOCKER-1/2 y GAP-1/2/3 aplicados, el plan vuelve a ≥93; los ítems restantes se pueden incorporar en KBX-2/4/31 a medida que se tomen.
