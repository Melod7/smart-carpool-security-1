# Artefactos de QA (KBX-27–29)

Postman, JMeter, Selenium, TestLink, MantisBT y SAST aterrizan en KBX-28/29.
La cobertura unitaria (KBX-27) publica reportes en `qa/results/`.

## Comandos locales (KBX-27)

```bash
make test              # backend + web + mobile (con umbrales)
make test-be           # xUnit + cobertura Application/Domain ≥70%
make test-web          # Vitest + cobertura pages/auth/lib ≥70%
make test-mobile       # flutter_test + cobertura filtrada ≥60%
```

Reportes:
- Backend: `qa/results/backend-coverage/` (Cobertura XML)
- Web: `qa/results/web-coverage/`
- Mobile: `mobile/coverage/lcov.filtered.info` (+ copia en `qa/results/mobile-coverage/`)

## Política de tests flaky (KBX-27)

1. **No silenciar:** no usar `Skip` permanente ni `expect(...).rejects` vacíos para “pasar CI”.
2. **Reintentos:** máximo **2** retries en CI solo para tests marcados `[Trait("Flaky","true")]` / `test.skipIf` temporal con issue link.
3. **Cuarentena:** si un test falla ≥2 veces en 24 h en main/PR, se etiqueta `flaky`, se abre issue el mismo día y se mueve a cuarentena (excluido del gate) máximo 5 días hábiles.
4. **Causa raíz:** la issue debe incluir comando de reproducción, logs y owner; sin issue no hay cuarentena.
5. **Determinismo:** preferir reloj/seeds fijos; no depender de timing de Maps/red real en unit tests.
6. **Responsabilidad:** el autor del PR que introdujo el flake lo arregla o documenta el workaround antes del merge a main.

## Alcance de cobertura web

El umbral ≥70% aplica a módulos con suite dedicada: `src/lib`, `RequireAuth`, `DashboardPage`, `ConfiguracionPage`, `UniversityFormDialog`. El resto de páginas se amplía en KBX-28/29.

## Alcance de cobertura mobile

El umbral ≥60% aplica a lógica de dominio/UI testeable (`models`, auth state, validaciones, labels, SOS overlay, theme, geometry cache). Se excluyen del gate: `*_page.dart` densas, shells, clients HTTP Dio y `publish_modal` (cubiertos por flujos manuales / KBX-28+).
