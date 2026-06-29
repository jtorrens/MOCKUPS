# Icon theme generator implementation plan

Este documento define cómo incorporar en MOCKUPS una herramienta para añadir o sincronizar tokens de iconos en todos los sets de una producción.

Complementa a [`icon_theme_set_script_contract.md`](./icon_theme_set_script_contract.md), que define el formato final esperado en disco.

## Decisión de arquitectura

La funcionalidad vive dentro de MOCKUPS, en `System Data > Icon themes`, pero separada por capas:

```text
IconThemeRecordEditor
  UI y modales

debug-server API
  endpoints transaccionales de aplicación

IconThemeSetService
  lógica de set, validación, escritura y refresh

IconProvider adapters / script runner
  lógica específica de Lucide, Material, etc.
```

Regla importante:

- React no sabe descargar iconos.
- El renderer/preview no sabe nada de proveedores.
- `mapping_json` no es una base de datos de proveedores.
- La fuente de verdad del set final son los SVG presentes en todos los directorios hermanos.

## Objetivo funcional

Desde el editor de `Icon themes`, poder:

1. Crear o actualizar un token lógico.
2. Obtener/generar el SVG correspondiente para todos los sets hermanos.
3. Validar que todos los sets tienen el mismo inventario de tokens.
4. Actualizar `mapping_json` mediante el mismo criterio que `Refresh sets`.
5. Fallar de forma segura si algún set no puede completarse.

## Lo que ya existe

Ya tenemos:

- Tabla `icon_themes`.
- Campo `asset_root`.
- `mapping_json.tokens`.
- Preview de iconos en el editor.
- Botón `Refresh sets`.
- Botón `Delete`, que borra el token de todos los sets.
- API:
  - `POST /api/app/icon-theme/refresh`
  - `POST /api/app/icon-theme/delete-token`
- Contrato de estructura:

```text
<mediaRoot>/icon-themes/
  lucide-basic/
    chat_send.svg
  lucide-semibold/
    chat_send.svg
  material-rounded-basic/
    chat_send.svg
```

## Nuevo caso de uso actualizado

No pedimos al usuario que conozca el nombre interno del icono en cada repositorio.

El flujo correcto es:

1. Buscar una palabra, por ejemplo `telephone`.
2. MOCKUPS consulta/escanea Lucide y Material.
3. La UI muestra candidatos en dos columnas:
   - Lucide
   - Material
4. El usuario selecciona el candidato correcto de cada repositorio.
5. El usuario define el token lógico MOCKUPS, por ejemplo `phone_call`.
6. MOCKUPS genera ese token para todos los sets definidos en la producción.
7. MOCKUPS refresca todos los icon themes afectados.

El script batch original queda como base histórica para crear listas iniciales, pero la nueva rutina debe pedir/generar un icono concreto.

Añadir botón:

```text
Icon themes > Icon tokens > Search / Add token
```

Abre un modal propio, no `prompt/confirm`.

### Paso 1: Search

Campos:

| Campo | Tipo | Nota |
|---|---|---|
| Search | text | palabra libre, ej. `telephone` |

Resultado:

```text
Lucide candidates             Material candidates
phone                         call
phone-call                    phone_in_talk
phone-off                     call_end
...
```

Cada candidato debe mostrar:

- preview SVG si está disponible;
- nombre interno del repositorio;
- provider.

### Paso 2: Define token

Campos base:

| Campo | Tipo | Nota |
|---|---|---|
| Token | text | `snake_case`, obligatorio |
| Category | text/select | por defecto primer segmento del token |
| Description | text multiline | opcional pero recomendado |

Campos seleccionados por provider:

| Campo | Tipo | Nota |
|---|---|---|
| Lucide source | readonly/text | nombre elegido en búsqueda |
| Material source | readonly/text | nombre elegido en búsqueda |

Los parámetros por set no se escriben manualmente en este modal. Se leen desde la definición del set.

Ejemplo:

```text
lucide-basic            provider lucide, stroke 2
lucide-semibold         provider lucide, stroke 2.5
material-rounded-basic  provider material, style rounded, weight 400
material-outlined-basic provider material, style outlined, weight 400
```

## Definición explícita de cada set

Cada record `icon_themes` debe tener en `metadata_json` una definición del set.

Ejemplo Lucide:

```json
{
  "schemaVersion": 1,
  "iconSet": {
    "provider": "lucide",
    "setName": "lucide-semibold",
    "package": "lucide-static",
    "stroke": 2.5,
    "fillMode": "stroke"
  }
}
```

Ejemplo Material:

```json
{
  "schemaVersion": 1,
  "iconSet": {
    "provider": "material",
    "setName": "material-rounded-basic",
    "package": "@material-symbols/svg-400",
    "style": "rounded",
    "weight": 400,
    "fillMode": "filled"
  }
}
```

Esta metadata se puede inferir inicialmente desde:

1. `manifest.json` del directorio del set, si existe.
2. Nombre de directorio como fallback:
   - `lucide-basic`
   - `lucide-semibold`
   - `material-rounded-basic`
   - `material-outlined-400`
3. Valores por defecto razonables si no hay datos suficientes.

Después de inferirla, debe quedar guardada explícitamente en `metadata_json`.

## Modelo recomendado de request

```ts
interface SearchIconThemeTokenRequest {
  recordId: string;
  query: string;
}
```

```ts
interface SearchIconThemeTokenResult {
  lucide: Array<{
    provider: "lucide";
    sourceName: string;
    previewUrl?: string;
  }>;
  material: Array<{
    provider: "material";
    sourceName: string;
    previewUrl?: string;
  }>;
}
```

## Modelo recomendado de request de generación

```ts
interface GenerateIconThemeTokenRequest {
  recordId: string;
  token: string;
  category?: string;
  description?: string;
  selectedSources: {
    lucide?: string;
    material?: string;
  };
}
```

El backend combina `selectedSources` con la definición de todos los sets de la producción.

Ejemplo:

```json
{
  "recordId": "icon_theme_lucide_basic",
  "token": "phone_call",
  "category": "phone",
  "description": "Phone call icon",
  "selectedSources": {
    "lucide": "phone-call",
    "material": "call"
  }
}
```

## Modelo de response

```ts
interface GenerateIconThemeTokenResult {
  tableId: "icon_themes";
  record: AppRecord;
  state: AppState;
  token: string;
  writtenFileCount: number;
  setCount: number;
  commonTokenCount: number;
  omittedTokenCount: number;
  warnings: string[];
}
```

## API nueva

```text
POST /api/app/icon-theme/generate-token
```

Responsabilidades:

1. Cargar el record `icon_themes`.
2. Resolver `mediaRoot`.
3. Detectar todos los sets hermanos.
4. Cargar todos los `icon_themes` de la producción.
5. Validar unidad de sets:
   - todos cuelgan de la misma raíz `icon-themes`;
   - todos tienen `metadata_json.iconSet`;
   - todos tienen provider soportado;
   - hay source seleccionado para cada provider usado.
6. Validar request:
   - token válido
   - sources seleccionados existen en búsqueda o se pueden resolver
7. Generar SVGs en un directorio temporal.
8. Validar SVGs:
   - extensión SVG
   - contiene `<svg`
   - tiene `viewBox`
   - preferiblemente usa `currentColor`
9. Escribir todos los SVGs de forma atómica o lo más cercana posible.
10. Ejecutar internamente el mismo refresh en todos los icon themes de la producción.
11. Devolver `AppState` actualizado.

## Política de fallo

Regla estricta:

- Si falla un set, no se actualiza `mapping_json`.
- Si es posible, no se escribe ningún archivo final.
- Si se escribieron archivos y luego falla algo, devolver warning claro.

Implementación práctica inicial:

```text
tmp/
  token/
    lucide-basic/chat_send.svg
    lucide-semibold/chat_send.svg
    ...
```

Solo cuando todos validan:

```text
rename/copy tmp -> final
refresh mapping
```

## Dónde poner el código

Propuesta de estructura:

```text
src/debug-server/icon-themes/
  iconThemeSetService.ts
  iconTokenGenerator.ts
  providers/
    localProvider.ts
    scriptProvider.ts
```

En una primera fase se puede mantener dentro de `debugService.ts` si queremos velocidad, pero la dirección correcta es extraerlo a `iconThemeSetService.ts` porque `debugService.ts` ya está creciendo demasiado.

## Provider strategy

### Fase 1: script provider

La forma más limpia y flexible:

- MOCKUPS genera un JSON request.
- Ejecuta un script local controlado por nosotros.
- El script descarga/genera SVGs.
- MOCKUPS valida y escribe.

Ventaja:

- No metemos APIs específicas de icon repos en MOCKUPS.
- ChatGPT u otro hilo puede mejorar el script sin tocar la app.
- Más portable si luego cambiamos proveedores.

### Fase 2: providers internos opcionales

Solo si merece la pena:

- `lucideProvider`
- `materialProvider`

Pero no lo haría al principio.

## UI propuesta

En `IconThemeRecordEditor`:

```text
Toolbar:
  [Refresh sets] [Add / Sync token]
```

Modal:

```text
Add / Sync icon token

Token          [chat_send]
Category       [chat]
Description    [Send message icon]

Sources
lucide-basic            provider [script] source [send] stroke [2]
lucide-semibold         provider [script] source [send] stroke [2.5]
material-rounded-basic  provider [script] source [send]

[Cancel] [Generate]
```

Después de generar:

```text
Generated chat_send in 4 sets.
Mapping refreshed. 83 common tokens.
```

## Relación con el diccionario/editor architecture

Este editor es un caso especial porque edita una colección/asset set, no campos escalares simples.

Aun así debe cumplir:

- Usar `EditorHeader`, `EditorSectionCard`, `EditorSectionButton`.
- Usar modales propios (`AppModalDialog` o variante específica).
- No usar `window.confirm/prompt`.
- No crear estilos nuevos si una clase existente cubre el concepto.
- Si se añaden campos escalares persistentes, deben pasar por `FieldDefinition`.

## Seguridad y límites

No ejecutar red/network desde UI.

Si el provider script necesita red:

- Se ejecuta desde backend/debug-server.
- Debe estar documentado.
- Debe poder fallar sin dejar la producción en estado parcial.

No borrar archivos fuera de:

```text
<mediaRoot>/icon-themes/<set>/<token>.svg
```

## Fases recomendadas

### Fase A — Preparar servicio interno sin UI nueva

- Extraer helpers actuales de refresh/delete desde `debugService.ts` a `iconThemeSetService.ts`.
- Mantener endpoints existentes.
- Typecheck + probar Refresh/Delete.

Objetivo: tener una base limpia antes de añadir generación.

### Fase B — Inferir y guardar metadata de sets

- Leer `manifest.json` de cada set si existe.
- Inferir provider/style/weight/stroke.
- Guardar `metadata_json.iconSet`.

### Fase C — Añadir search

Endpoint:

```text
POST /api/app/icon-theme/search-sources
```

Devuelve candidatos Lucide/Material para una query.

### Fase D — Añadir endpoint dry-run

Endpoint:

```text
POST /api/app/icon-theme/validate-token-request
```

Valida:

- token
- sets existentes
- sources completos
- paths finales

No escribe archivos.

### Fase E — Añadir generación concreta

Endpoint real:

```text
POST /api/app/icon-theme/generate-token
```

Genera un token concreto para todos los sets de la producción.

### Fase F — UI modal

Añadir modal de Search / Add token.

### Fase G — Auditoría

- Refresh no omite tokens inesperados.
- Delete sigue borrando en todos los sets.
- Generate falla si falta un set.
- `mapping_json` solo contiene tokens comunes.

## Pregunta abierta antes de implementar

La única decisión que conviene cerrar antes de escribir código de generación:

¿Queremos que MOCKUPS ejecute un script local externo, o preferimos implementar providers internos directamente?

Mi recomendación:

1. Primero `script provider`.
2. Más adelante, si lo usamos mucho, providers internos.

Así mantenemos MOCKUPS como herramienta de producción y evitamos acoplarla a APIs/repositorios concretos de iconos.
