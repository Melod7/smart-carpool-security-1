# React + TypeScript + Vite

Esta plantilla ofrece un setup mínimo para hacer funcionar React en Vite con HMR y algunas reglas de Oxlint.

Actualmente hay dos plugins oficiales disponibles:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) usa [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) usa [SWC](https://swc.rs/)

## React Compiler

El React Compiler no está habilitado en esta plantilla por su impacto en el rendimiento de dev y build. Para añadirlo, consulta [esta documentación](https://react.dev/learn/react-compiler/installation).

## Ampliar la configuración de Oxlint

Si estás desarrollando una aplicación de producción, recomendamos habilitar reglas de lint type-aware instalando `oxlint-tsgolint` y editando `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

Consulta la [documentación de reglas de Oxlint](https://oxc.rs/docs/guide/usage/linter/rules) para la lista completa de reglas y categorías.
