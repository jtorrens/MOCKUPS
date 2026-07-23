# Editor architecture diagnosis

Este documento resume cómo está organizada actualmente la arquitectura de editores de la app, qué partes funcionan mejor, qué partes nos están dando más problemas y hacia dónde conviene llevar la limpieza.

La intención no es describir cada archivo al detalle, sino fijar el modelo mental que queremos respetar a partir de ahora.

## Modelo general

La arquitectura deseada para cualquier campo editable es:

```txt
Dato en DB / JSON
  ↓
FieldDefinition
  ↓
ValueKind / Dictionary
  ↓
Control estándar
  ↓
Tokens visuales del editor
  ↓
CSS común de editor
  ↓
UI visible
```

La regla de fondo es:

```txt
Un campo no debería decidir manualmente qué control visual usa.
El campo declara qué tipo de dato es, y el diccionario devuelve el control correcto.
```

Por ejemplo:

```txt
decimal          → input numérico estándar
boolean          → control boolean estándar
fontFamily       → selector de fuente aprobado
fontWeight       → selector de peso
fontStyle        → selector de estilo
themeColorToken  → selector de token de color de tema
paletteColor     → selector de color de paleta
iconToken        → selector de icono
```

Esto evita que cada editor invente su propia versión de input, select, checkbox, color picker, etc.

## Capas principales

### 1. DB / schema

La base de datos guarda registros reales de producción:

- productions
- themes
- actors
- apps
- module theme configs
- module instances
- component classes
- icon themes
- palette colors
- production fonts
- etc.

Algunos campos son columnas simples y otros son JSON estructurado.

Los campos JSON son donde aparece gran parte de la complejidad, porque pueden contener:

- tokens de diseño;
- overrides;
- herencias;
- listas;
- objetos compuestos;
- contenido concreto de mensajes;
- configuración de módulos.

### 2. `domain/fields/*`

Esta capa define campos mediante `FieldDefinition`.

Ejemplos:

- `actorFields.ts`
- `themeFields.ts`
- `chat/typographyFields.ts`
- `chat/headerFields.ts`
- `chat/keyboardFields.ts`
- etc.

Cada campo debería declarar:

- `id`
- `kind`
- `defaultValue`, si procede
- metadata UI:
  - label
  - min/max
  - step
  - opciones
  - tabla referenciada
  - si admite vacío
  - grupo semántico

Ejemplo conceptual:

```ts
{
  id: "theme.typography.bodySize",
  kind: "decimal",
  ui: {
    label: "Body size",
    min: 1,
    step: 1,
  },
}
```

Esta capa es clave porque es la fuente de verdad para los editores.

### 3. Value system / dictionary

El diccionario de valores define qué tipos existen y cómo se validan.

Ejemplos de tipos:

- text
- integer
- decimal
- alpha
- boolean
- enum
- recordReference
- relativeFilePath
- fontFamily
- fontWeight
- fontStyle
- paletteColorToken
- themeColorToken
- iconToken
- jsonObject
- jsonArray

El objetivo final es que todo dato editable pase por este sistema, incluso cuando no admita herencia.

Un campo sin herencia sigue siendo un campo tipado.

```txt
No herencia ≠ no diccionario
```

### 4. `ValueKindControlRegistry`

Esta capa traduce tipos de dato a controles de editor.

Conceptualmente:

```txt
FieldDefinition.kind → control estándar
```

Ejemplo:

```txt
decimal       → number control
boolean       → checkbox control
enum          → select control
fontFamily    → typography/font control
themeColor    → theme color token control
```

Esta es una de las piezas más importantes de la limpieza actual.

El objetivo es que los editores no hagan esto manualmente:

```tsx
<input type="number" />
```

sino algo equivalente a:

```tsx
<DictionaryFieldControl field={fieldDefinition} />
```

### 5. React editors

Los editores están implementados en React/TypeScript.

Actualmente hay varias rutas:

#### `RecordFieldRenderer`

Renderiza campos simples de tabla.

Es adecuado para columnas como:

- name
- family
- version
- production_id
- default_theme_id
- etc.

Esta ruta ya está parcialmente conectada al diccionario.

#### `JsonTreeEditor`

Editor genérico para campos JSON.

Puede delegar en:

- `TokenOverrideEditor`
- `JsonObjectEditor`
- `JsonArrayEditor`
- `JsonValueEditor`
- `RawJsonEditor`

Es una de las zonas más delicadas porque debe editar JSON arbitrario, pero también respetar controles semánticos cuando hay bindings.

#### `TokenOverrideEditor`

Editor especializado para JSON con herencia/override.

Se usa para:

- tokens de theme;
- tokens de app;
- tokens de module;
- overrides de screen/module instance;
- algunos grupos con valores heredados.

Es probablemente el punto más problemático de la arquitectura actual, porque mezcla:

- paths JSON;
- inherited values;
- local overrides;
- restore;
- widgets antiguos;
- hints;
- controles compuestos;
- diccionario nuevo.

El objetivo final debería ser:

```txt
JSON path → FieldDefinition binding → DictionaryControl
```

No:

```txt
JSON path → hint → widget string → control manual
```

#### Editores específicos

Algunos editores tienen UI propia porque su dominio lo necesita:

- actors
- themes
- component classes
- chat content
- module behavior
- icon themes
- palette
- production fonts

Esto está bien siempre que los controles base sigan saliendo del diccionario.

La regla debería ser:

```txt
Un editor específico puede organizar la pantalla,
pero no debería inventar controles base nuevos.
```

## CSS actual

Los estilos de editor están divididos en varios CSS:

```txt
EditorSystem.css
EditorContent.css
EditorJson.css
EditorMedia.css
EditorBehavior.css
```

Estos ficheros contienen estilos para:

- cards;
- subcards;
- accordions;
- labels;
- inputs;
- selects;
- override rows;
- inherited values;
- media previews;
- behavior blocks;
- JSON editor;
- token override editor.

El problema es que aún existen reglas históricas bastante amplias:

```css
.record-editor input
.token-override-input input
.json-value-control
.editor-workspace ...
```

Cuando varias reglas compiten por el mismo nodo, puede ocurrir que:

1. React renderice bien la clase.
2. El control tenga la clase correcta.
3. Una regla CSS posterior lo vuelva a pintar como antes.

Esto fue exactamente el tipo de problema que vimos durante la auditoría de diccionario.

## Lo que funciona bien

La parte de preview/render y componentes visuales está funcionando bastante mejor que la parte de editores.

Ejemplos:

- keyboard
- text input bar
- bubbles
- avatar
- label
- audio message
- video message
- status bar
- navigation bar
- icon button

Estas piezas funcionan mejor porque tienen una estructura más cerrada:

```txt
props resueltas → componente visual → preview/render
```

Cada componente tiene entradas relativamente claras y no depende tanto de CSS global histórico.

Este modelo es el que deberíamos imitar en los editores:

```txt
entrada clara
responsabilidad clara
salida clara
pocas rutas alternativas
```

## Lo que da más problemas

### 1. JSON editors + hints + dictionary bindings

Es el foco principal.

Un campo puede parecer que está en el editor correcto, pero si no tiene binding a `FieldDefinition`, realmente sigue siendo legacy.

Ejemplo reciente:

```txt
Theme > tokens.typography
```

El grupo existía y visualmente parecía parte del sistema moderno, pero sus controles no podían marcarse como dictionary controls porque faltaban bindings en `THEME_TOKEN_BINDINGS`.

La solución correcta no era más CSS, sino crear bindings de diccionario.

### 2. CSS global / cascada histórica

Aunque React esté bien, CSS puede pisarlo.

Esto pasa sobre todo con:

- inherited values;
- override values;
- token override rows;
- json-value-control;
- inputs dentro de wrappers antiguos.

El riesgo aumenta cuando usamos `!important`.

### 3. Controles compuestos

Los controles compuestos necesitan tratamiento especial:

- typography: family + weight + style;
- color + alpha;
- x/y;
- width/height;
- file picker + preview;
- component override modal;
- icon token multi-select.

Estos no son simples inputs.

Por eso deben estar modelados explícitamente como controles del diccionario, no como combinaciones improvisadas en cada editor.

### 4. Muchas rutas para editar campos

Ahora mismo un campo editable puede venir de:

- `RecordFieldRenderer`
- `JsonValueEditor`
- `TokenOverrideEditor`
- `ThemeFields`
- `ActorFields`
- `ComponentClassRecordEditor`
- editores específicos de chat

Esto no es malo por sí mismo.

El problema aparece si cada ruta decide controles por su cuenta.

### 5. Compatibilidad legacy / fallbacks

Los fallbacks reducen roturas a corto plazo, pero aumentan confusión.

Conviene evitar rutas paralelas del tipo:

```txt
si existe diccionario usa esto,
si no usa hints,
si no usa heurística,
si no usa widget antiguo,
si no usa input genérico
```

Durante migración puede ser necesario, pero debe ser temporal.

## Regla estricta propuesta para nuevos editores

### Regla 1

No crear un input/select/checkbox/color picker manual si ya existe un tipo de diccionario para ese concepto.

### Regla 2

Si no existe tipo de diccionario, primero se crea o se amplía el diccionario.

### Regla 3

Un editor puede organizar layout, agrupaciones, accordions y previews, pero no debe inventar controles base.

### Regla 4

Cada campo visible debe poder responder:

```txt
¿Cuál es mi FieldDefinition?
¿Cuál es mi ValueKind?
¿Qué control devuelve el registry para mí?
¿Tengo herencia?
¿Tengo restore?
¿Tengo estado override/default?
```

### Regla 5

Si un campo no tiene herencia, igualmente debe estar tipado.

```txt
Campo sin herencia = FieldDefinition sin inherited behavior
```

No debe caer por eso a input manual.

### Regla 6

No añadir clases CSS nuevas para conceptos ya existentes sin validarlo antes.

Conceptos ya existentes:

- card;
- subcard;
- editor header;
- field row;
- label;
- control;
- inherited value;
- override value;
- restore button;
- glyph/icon;
- action button;
- accordion chevron.

## Auditoría recomendada por editor

Cada vez que limpiemos un editor o card:

1. Listar campos visibles.
2. Para cada campo, comprobar si tiene `FieldDefinition`.
3. Si no lo tiene, decidir:
   - crear field definition;
   - ocultarlo por ser interno;
   - dejarlo fuera por ser contenido concreto no tipado.
4. Comprobar que el control sale del registry.
5. Comprobar que no hay input/select manual.
6. Comprobar inherited/override/restore si aplica.
7. Comprobar que no se usan tokens de paleta donde debería haber token semántico de tema.
8. Comprobar preview/render si el campo afecta a visual.
9. Pasar typecheck.
10. Pasar diff check.

## Auditoría automática deseada

Conviene crear scripts que detecten:

- usos de `<input>` manuales en editores;
- usos de `<select>` manuales en editores;
- campos JSON visibles sin binding;
- campos con `dictionary-field` pero sin `dictionary-control`;
- clases legacy usadas fuera de CSS;
- tokens de color hardcodeados;
- tokens de paleta usados directamente donde debería haber theme token;
- fallbacks legacy no usados;
- campos internos visibles por accidente.

## Mejoras futuras del renderer visual

### Bubble shape como SVG único

Cuando el sistema esté más estable, conviene sustituir la construcción actual del bubble con varias piezas (`body`, `tail`, extensión de unión) por una silueta vectorial única.

Modelo deseado:

```txt
BubbleShapeRenderer
  → buildBubblePath({ width, height, radius, tailStyle, tailSide, tailPosition, tailSize })
  → <svg><path /></svg>
```

Motivo:

- el borde sería un `stroke` real sobre la silueta compuesta;
- la sombra se aplicaría al path completo;
- el relief/contorno no dependería de aproximaciones con `drop-shadow`;
- tail y bubble serían geométricamente una sola pieza;
- preview y render compartirían una forma más robusta.

Regla para retomarlo:

- mantener el layout actual;
- cambiar sólo la representación visual del shape;
- no mover texto, media, avatar, status ni label en esa fase.

## Diagnóstico final

La arquitectura va en buena dirección.

El preview/render y las clases visuales están evolucionando de forma limpia porque tienen contratos claros.

La zona de editores todavía está en transición entre:

```txt
editor manual histórico
```

y:

```txt
editor guiado por diccionario
```

Los problemas que estamos viendo no indican que React/TypeScript sean una mala elección. Indican que necesitamos terminar de consolidar el contrato:

```txt
FieldDefinition → ValueKindControlRegistry → DictionaryFieldControl
```

Cuando esa cadena sea obligatoria, los editores deberían volverse mucho más predecibles.
