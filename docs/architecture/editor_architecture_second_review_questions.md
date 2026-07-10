# Editor architecture — second review questions

Este documento resume las decisiones que queremos adoptar para la arquitectura de editores y plantea dudas concretas de implementación.

No buscamos replantear la arquitectura desde cero. La dirección general ya está bastante clara:

```txt
React + TypeScript
FieldDefinition
ValueKind / dictionary
EditorFieldDescriptor
FieldRow
DictionaryFieldControl
editor-ui común
CSS controlado
Preview/render aislado conceptualmente
```

El objetivo de esta segunda revisión es detectar riesgos prácticos antes de implementar la siguiente fase.

## Contexto del proyecto

Mockups es una herramienta interna de producción.

El sistema permite editar producciones, temas, actores, apps, módulos, instancias de módulos, componentes reutilizables y contenido concreto de shots/screens.

El preview/render está funcionando relativamente bien porque sigue un patrón claro:

```txt
datos resueltos → componente visual → preview/render
```

La zona problemática son los editores, porque todavía conviven dos modelos:

```txt
Modelo histórico:
  JSON path / hints / widget string / input manual / CSS heredado

Modelo nuevo:
  FieldDefinition / ValueKind / Registry / DictionaryFieldControl / editor-ui común
```

Queremos consolidar una única pipeline.

## Arquitectura objetivo

La pipeline deseada para cualquier campo editable es:

```txt
Stored value / JSON path / record column
  ↓
FieldDefinition
  ↓
ValueKind
  ↓
EditorFieldDescriptor
  ↓
FieldRow
  ↓
DictionaryFieldControl
  ↓
editor-ui tokens / CSS común
```

Los editores específicos pueden organizar workflows, cards, accordions y acciones de dominio, pero no deberían inventar inputs/selects/checkboxes/color pickers propios si el concepto ya existe en el diccionario.

## Decisión principal que queremos validar

Queremos introducir `EditorFieldDescriptor` como pieza intermedia común.

Conceptualmente:

```ts
type EditorFieldDescriptor = {
  field: FieldDefinition
  value: unknown
  inheritedValue?: unknown
  resolvedValue?: unknown

  state: "local" | "inherited" | "default" | "invalid"

  canInherit: boolean
  canRestore: boolean
  readonly?: boolean

  write: (value: unknown) => void
  restore?: () => void

  source: {
    kind:
      | "record-column"
      | "json-binding"
      | "component-override"
      | "module-instance-content"
      | "custom"
    path?: string[]
  }

  validation?: {
    valid: boolean
    message?: string
  }
}
```

La idea es que todas las rutas produzcan descriptors:

```txt
RecordFieldRenderer → EditorFieldDescriptor
JsonFieldBinding → EditorFieldDescriptor
TokenOverrideEditor → EditorFieldDescriptor
Component override modal → EditorFieldDescriptor
Editores específicos → EditorFieldDescriptor para sus controles base
```

Después se pintan así:

```tsx
<FieldRow descriptor={descriptor}>
  <DictionaryFieldControl descriptor={descriptor} />
</FieldRow>
```

## Separación propuesta

### `FieldDefinition`

Define qué es el dato.

Responsabilidades:

- id;
- kind;
- default value;
- label;
- min/max/step;
- options;
- relation/table metadata;
- file metadata;
- si permite vacío;
- metadata necesaria para editar el dato.

No debería saber nada de CSS ni layout.

### `EditorFieldDescriptor`

Define qué está pasando con ese dato en este editor concreto.

Responsabilidades:

- valor local;
- valor heredado;
- valor resuelto;
- estado;
- readonly;
- restore;
- validación;
- origen;
- callbacks de escritura.

### `FieldRow`

Define el chrome común de campo.

Responsabilidades:

- label;
- description;
- inherited/default/override state;
- restore button;
- error;
- readonly;
- layout;
- spacing;
- estado visual común.

### `DictionaryFieldControl`

Define el control concreto.

Responsabilidades:

- number input;
- text input;
- checkbox;
- select;
- font selector;
- color token selector;
- palette color selector;
- icon token selector;
- file picker;
- compound controls.

### `VisualStyle`

Define cómo se ve todo eso mediante `editor-ui` común.

No debería haber CSS específico por feature para conceptos ya existentes como input, select, card, row, restore, modal, glyph, etc.

## Plan de implementación pensado

### Fase 1 — Contrato base

Crear:

```txt
EditorFieldDescriptor
FieldRow común
helpers para crear descriptors desde columnas normales
```

Mantener:

```txt
ValueRegistry
ValueKindControlRegistry
DictionaryFieldControl
```

Añadir validaciones:

```txt
cada ValueKind tiene control registrado
cada control registrado apunta a ValueKind válido
```

### Fase 2 — Adaptar `RecordFieldRenderer`

Es la ruta más simple.

Objetivo:

```txt
RecordFieldRenderer
  → createRecordFieldDescriptor
  → FieldRow
  → DictionaryFieldControl
```

Esta fase nos daría el patrón correcto antes de tocar JSON.

### Fase 3 — JSON bindings

Crear:

```txt
createJsonFieldDescriptor
```

Responsabilidades:

- leer valor local;
- leer inherited/resolved;
- validar con `FieldDefinition`;
- escribir en `outputPath`;
- restaurar;
- devolver descriptor.

### Fase 4 — Vaciar `TokenOverrideEditor`

Convertirlo en adapter de estado:

```txt
local JSON + inherited JSON + bindings
  → descriptors
  → FieldRow + DictionaryFieldControl
```

Debería dejar de decidir manualmente:

- widgets;
- inputs/selects;
- CSS;
- restore button visual;
- row visual.

### Fase 5 — Controles compuestos

Elevar a ciudadanos de primera clase:

```txt
typography
color token + alpha
x/y
width/height
file picker + preview
icon token picker
component override modal
```

El criterio sería:

```txt
Agrupación visual compuesta.
Fields internos independientes.
```

Es decir, no queremos un objeto opaco que destruya herencia por subcampo, pero tampoco queremos que cada editor agrupe subcampos a mano.

### Fase 6 — Limpieza CSS legacy

Una vez que los campos pasen por `FieldRow` y `DictionaryFieldControl`, eliminar o encapsular reglas amplias:

```css
.record-editor input
.record-editor select
.token-override-input input
.token-override-input select
.json-value-control
.editor-workspace input
.editor-workspace select
```

La regla sería:

```txt
No arreglar legacy CSS con más especificidad.
Mover la intención visual a editor-ui.
Eliminar la regla antigua cuando deje de usarse.
```

## Dudas concretas para revisar

### 1. ¿`EditorFieldDescriptor` debería contener callbacks?

Propuesta actual:

```ts
write: (value: unknown) => void
restore?: () => void
```

Duda:

¿Es correcto que el descriptor contenga callbacks, o sería mejor que fuera un objeto puramente descriptivo y que las acciones vivan en un controller aparte?

Opción A:

```txt
Descriptor con callbacks.
Más práctico para React.
Más fácil de pasar a FieldRow/control.
```

Opción B:

```txt
Descriptor puro + controller.
Más limpio conceptualmente.
Más boilerplate.
```

### 2. ¿Cómo modelar herencia en el descriptor?

Propuesta:

```ts
value
inheritedValue
resolvedValue
state
canRestore
```

Duda:

¿Conviene distinguir mejor entre:

```txt
localValue
parentValue
defaultValue
resolvedValue
```

para no mezclar inherited con default?

### 3. ¿`FieldRow` debe recibir descriptor entero o props derivadas?

Opción A:

```tsx
<FieldRow descriptor={descriptor}>
```

Ventaja:

```txt
menos props;
contrato único.
```

Riesgo:

```txt
FieldRow conoce demasiado del modelo.
```

Opción B:

```tsx
<FieldRow
  label={descriptor.field.ui.label}
  state={descriptor.state}
  error={descriptor.validation?.message}
  onRestore={descriptor.restore}
>
```

Ventaja:

```txt
FieldRow más tonto.
```

Riesgo:

```txt
cada editor vuelve a mapear props manualmente.
```

### 4. ¿`DictionaryFieldControl` debe recibir `FieldDefinition` o `EditorFieldDescriptor`?

Opción A:

```tsx
<DictionaryFieldControl field={field} value={value} onChange={...} />
```

Más desacoplado.

Opción B:

```tsx
<DictionaryFieldControl descriptor={descriptor} />
```

Más directo, pero acopla el control al estado de editor.

Nuestra intuición actual:

```txt
FieldRow probablemente debe recibir descriptor.
DictionaryFieldControl podría recibir descriptor o una selección explícita derivada.
```

### 5. ¿Cómo introducir controles compuestos sin romper herencia por subcampo?

Caso:

```txt
typography = family + weight + style + size + lineHeight
```

Queremos:

```txt
un solo bloque visual coherente
pero subcampos independientes en storage/herencia
```

Duda:

¿Conviene que el descriptor pueda representar grupos?

Ejemplo:

```ts
type EditorFieldGroupDescriptor = {
  kind: "group"
  id: "typography"
  fields: EditorFieldDescriptor[]
  control: "typography"
}
```

¿O es mejor mantener solo descriptors individuales y que el control compuesto reciba varios descriptors?

### 6. ¿Cómo clasificar JSON concreto de contenido?

Tenemos JSON de contenido, por ejemplo mensajes de chat.

No todo eso debe tener herencia ni token override, pero sí debería usar controles base para campos como:

```txt
message.text
message.type
message.delayAfterPreviousFrames
message.media.filePath
message.status.deliveryStatus
```

Duda:

¿Tiene sentido que también pasen por `EditorFieldDescriptor`, aunque `canInherit = false`?

Nuestra intuición:

```txt
Sí. Campo sin herencia sigue siendo campo tipado.
```

### 7. ¿Dónde poner validación de layout/visual?

`FieldDefinition` puede validar tipo:

```txt
decimal, boolean, enum...
```

Pero hay reglas más contextuales:

```txt
width > 0
offset puede ser negativo
alpha 0..1
font must exist in production fonts
theme token must exist in theme
palette token must exist in production palette
```

Duda:

¿Debe el descriptor traer validación contextual ya resuelta?

O:

```txt
FieldDefinition valida forma básica.
Descriptor/controller valida contexto.
Resolver valida dominio final.
```

### 8. ¿Cómo migrar sin crear otra ruta paralela permanente?

Riesgo:

```txt
Durante transición convivirán:
legacy hints/widgets
DictionaryFieldControl
EditorFieldDescriptor
controles manuales
```

Duda:

¿Qué estrategia recomienda para que la transición no cree otra capa legacy?

Idea actual:

```txt
1. Descriptor primero en RecordFieldRenderer.
2. Una vez estable, JSON bindings.
3. Luego TokenOverrideEditor.
4. Cada fase elimina una ruta antigua, no solo añade nueva.
```

### 9. ¿CSS Modules / scoped CSS merece la pena ya?

Actualmente usamos CSS global dividido por editor:

```txt
EditorSystem.css
EditorContent.css
EditorJson.css
EditorMedia.css
EditorBehavior.css
```

Duda:

¿Conviene migrar `editor-ui` a CSS Modules / scoped CSS ahora, o esperar a que `FieldRow + DictionaryFieldControl` estén consolidados?

Nuestra intuición:

```txt
Primero consolidar componentes.
Luego reducir CSS legacy.
Después decidir si migrar a CSS Modules/scoped CSS.
```

### 10. ¿Qué sería un buen Definition of Done para Fase 1?

Propuesta:

```txt
1. Existe EditorFieldDescriptor.
2. Existe FieldRow común.
3. RecordFieldRenderer usa descriptors para columnas simples.
4. DictionaryFieldControl recibe datos desde descriptor o desde helper derivado.
5. No se rompe UI actual.
6. Typecheck OK.
7. Validación ValueKind ↔ control registry OK.
8. Queda documentada la regla para nuevos campos.
```

¿Falta algo esencial?

## Pregunta final

¿Esta secuencia parece razonable para una herramienta interna de producción?

```txt
1. EditorFieldDescriptor
2. FieldRow
3. RecordFieldRenderer
4. JSON bindings
5. TokenOverrideEditor como adapter
6. Controles compuestos
7. Limpieza CSS legacy
```

¿O hay alguna decisión aquí que convendría cambiar antes de escribir más código?

