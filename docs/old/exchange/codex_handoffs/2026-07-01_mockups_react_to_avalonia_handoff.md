# MOCKUPS handoff: React/Electron → Avalonia/Suki editor shell

Fecha: 2026-07-01  
Branch actual: `feature/desktop-editor-shell`  
Último commit empujado: `4ba3bbd Fix actor editor selection freeze`

Este documento está escrito para continuar el proyecto en otro hilo de Codex sin depender del contexto conversacional anterior.

## 1. Resumen ejecutivo

MOCKUPS empezó como una app React/Electron de depuración y edición visual para construir pantallas, módulos y renders de una producción tipo chat móvil. Esa app antigua vive principalmente en `src/debug-ui`, con persistencia SQLite en `src/persistence/sqlite`, lógica de dominio en `src/domain`, preview visual en `src/visual`, y render Remotion en `src/remotion`.

El spike actual está migrando/refactorizando el editor desktop hacia C# + Avalonia + SukiUI en `spikes/desktop-editor-shell`. No es una migración literal de React a Avalonia. La intención es conservar la lógica estable de dominio/preview/render, pero rehacer deliberadamente la capa de edición para evitar el crecimiento de controles manuales, CSS específico por editor y rutas legacy que habían hecho muy costosa la UI.

La lógica que se conserva como base estable:

- el modelo conceptual de producción: Project, Episodes, Shots, Apps, Modules, Production Data y System Data;
- la separación entre editor y runtime visual;
- el preview/render web como fuente visual principal;
- el sistema de paleta, temas, fuentes, iconos, componentes y módulos como diseño estructurado;
- el aprendizaje de la app React: dictionary-driven fields, component classes, overrides, palette tokens, typed controls, cards/acordeones, tree cards.

Las partes que se están rediseñando deliberadamente:

- el shell visual del editor;
- los editores de registros;
- la navegación/tree;
- la estructura de cards y grupos;
- el diccionario de tipos y controles;
- el almacenamiento del layout de editores;
- el sistema de commit de campos;
- la eliminación de rutas legacy/manuales cuando se pueda.

Regla mental importante: Avalonia/Suki no debe copiar la app React/Electron campo a campo. La app vieja es fuente de verdad para lógica visual/runtime y para estructuras ya validadas, pero la UI de edición se está rehaciendo con controles encapsulados y tabla de layout.

## 2. Mapa de código antiguo React/Electron

### Rutas principales

- `src/debug-ui/main.tsx`  
  Entrada de la UI React/Vite.

- `src/debug-ui/styles.css`  
  CSS global del editor antiguo. Sirve como referencia visual, no como algo a copiar literalmente.

- `src/debug-ui/panels/LeftPanel.css`  
  Estilos del panel izquierdo/tree antiguo.

- `src/electron/startElectron.ts`, `src/electron/main.cjs`, `src/electron/preload.cjs`  
  Shell Electron antiguo.

- `src/debug-server/server.ts`  
  Servidor de debug que alimenta la app React y el preview.

- `src/persistence/sqlite/schema.sql`  
  Schema SQLite antiguo principal.

- `src/persistence/sqlite/seedDevelopmentDatabase.ts`, `src/persistence/sqlite/seedExampleDataset.ts`  
  Seeds de desarrollo antiguos.

- `src/persistence/sqlite/SQLiteRepository.ts`  
  Repositorio SQLite antiguo.

### Componentes/editores relevantes

- `src/debug-ui/editors/RecordEditorDispatcher.tsx`  
  Dispatcher principal de editores de registros.

- `src/debug-ui/editors/GenericRecordEditor.tsx`  
  Editor genérico antiguo para registros.

- `src/debug-ui/editors/RecordFieldRenderer.tsx`  
  Ruta de render de campos de registros. Fue parte importante del intento de llevar todo al diccionario.

- `src/debug-ui/editors/ActorFields.tsx`  
  Editor antiguo de actores. Referencia para campos de actor, colores light/dark, avatar image, scale, offset, initials.

- `src/debug-ui/editors/ThemeRecordEditor.tsx`, `ThemeEditor.tsx`, `ThemeFields.tsx`  
  Editor antiguo de temas.

- `src/debug-ui/editors/AppRecordEditor.tsx`, `AppEditor.tsx`, `AppMediaFields.tsx`  
  Editor antiguo de apps.

- `src/debug-ui/editors/ModuleInstanceRecordEditor.tsx`, `ModuleInstanceEditor.tsx`  
  Editor antiguo de instancias de módulo.

- `src/debug-ui/editors/ModuleThemeConfigRecordEditor.tsx`, `ModuleThemeConfigEditor.tsx`  
  Editor antiguo de configuración de módulo.

- `src/debug-ui/editors/chat/*`  
  Editores específicos del chat module/content:
  - `ChatMessageFieldsEditor.tsx`
  - `ChatMessageMediaEditor.tsx`
  - `ChatHeaderFieldsEditor.tsx`
  - `ChatDictionaryFieldRow.tsx`
  - `ChatContentGroupEditor.tsx`
  - `ChatContentArrayEditor.tsx`
  - `chatContentModel.ts`

- `src/debug-ui/editor-ui/*`  
  Infraestructura UI antigua:
  - `DictionaryFieldControl.tsx`
  - `ValueKindControlRegistry.ts`
  - `EditorSectionCard.tsx`
  - `EditorSubsectionAccordion.tsx`
  - `EditorSectionButton.tsx`
  - `TypographySelector.tsx`
  - `IconTokenPicker.tsx`
  - `DeferredTextInput.tsx`
  - `DeferredNumberInput.tsx`

### Servicios/helpers importantes

- `src/domain/value-system/*`  
  Primer sistema fuerte de value registry/dictionary:
  - `ValueRegistry.ts`
  - `FieldDefinition.ts`
  - `SurfaceStyleDefinition.ts`
  - `JsonFieldBinding.ts`

- `src/domain/fields/*`  
  Definiciones de campos por dominio:
  - `recordColumnFields.ts`
  - `actor` estaba parcialmente en editor y en descriptores;
  - `deviceFields.ts`
  - `episodeFields.ts`
  - `shotFields.ts`
  - `themeFields.ts`
  - `chatFields.ts`
  - `moduleThemeConfigFields.ts`
  - `moduleInstanceBehaviorFields.ts`

- `src/debug-ui/field-descriptors/*`  
  Descriptores usados para editores antiguos.

- `src/debug-ui/validation/validateEditorDictionary.ts`  
  Validador antiguo de diccionario/editor.

- `src/persistence/audit/*`  
  Scripts de auditoría/normalización del modelo actual.

### CSS/preview/render que se debe conservar como base

La UI de edición se está reescribiendo, pero el runtime visual sigue siendo base estable:

- `src/debug-ui/preview/*`
  - `PreviewPanel.tsx`
  - `RightPreviewShell.tsx`
  - `RenderSurface.tsx`
  - `PreviewNavigationCard.tsx`
  - `preview.css`

- `src/visual/modules/*`
  - `screens/ChatScreenModule.ts`
  - `atomic/MessageBubbleModule.ts`
  - `atomic/ChatHeaderModule.ts`
  - `atomic/KeyboardModule.ts`
  - `atomic/TextInputBarModule.ts`
  - `atomic/AvatarModule.ts`
  - `atomic/StatusBarModule.ts`
  - `atomic/NavigationBarModule.ts`

- `src/visual/layout/*`
  - `layoutChatScreen.ts`
  - `layoutMessageBubble.ts`
  - `textMeasurement.ts`

- `src/visual/renderable/*`
  - `schema.ts`
  - `types.ts`
  - `helpers.ts`

- `src/remotion/*`
  - `Root.tsx`
  - `ChatScreenPreview.tsx`
  - `buildRenderableForFrame.ts`

### Modelos JSON / estructuras de datos usadas

En la app vieja hay mezcla de columnas SQL y JSON estructurado. Esto no debe copiarse sin criterio, pero sí sirve para saber qué datos existen:

- `themes`: tokens typography/colors/radii/shadows/relief/etc.
- `palette_colors`: token + color primitivo + metadata.
- `production_fonts`: familias aprobadas.
- `icon_themes`: sets de iconos.
- `devices`: `metrics_json` con canvas, screen, viewport, safe area, status bar, dynamic island.
- `actors`: metadata de color/avatar.
- `apps`, `modules`, `module_theme_configs`, `module_instances`: defaults, config y contenido de módulo.
- `component_classes`: componentes como avatar, keyboard, text input, button icon, audio/video, label, style-like groups.
- `episodes`, `shots`, `screen_instances`: estructura temporal/narrativa.

## 3. Mapa de código nuevo Avalonia/Suki

### Rutas principales

- `spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj`  
  Proyecto C# Avalonia/Suki. Usa:
  - `Avalonia` 12.0.5
  - `Avalonia.Controls.ColorPicker` 12.0.3
  - `Microsoft.Data.Sqlite` 10.0.9
  - `SukiUI` 7.0.1

- `spikes/desktop-editor-shell/App.axaml`  
  Inicializa Suki:
  - `RequestedThemeVariant="Dark"`
  - `<suki:SukiTheme ThemeColor="Blue" />`
  - ColorPicker Fluent.

- `spikes/desktop-editor-shell/MainWindow.axaml`  
  Shell principal con tres paneles:
  - navegación;
  - editor;
  - web preview placeholder.

- `spikes/desktop-editor-shell/MainWindow.axaml.cs`  
  Lógica actual del shell, navegación, cards, field binding, apply/update, preview placeholder y avatar preview.

- `spikes/desktop-editor-shell/Data/SpikeDatabase.cs`  
  SQLite local del spike, schema, seed, queries, mutations y editor layouts.

- `spikes/desktop-editor-shell/Data/desktop-editor-spike.sqlite`  
  DB local del spike.

- `spikes/desktop-editor-shell/Data/window-state.json`  
  Persistencia de tamaño de ventana/paneles.

- `spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs`  
  Tipos de campo del spike.

- `spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs`  
  Control genérico de diccionario en Avalonia.

- `spikes/desktop-editor-shell/EditorShell/EditorLayout.cs`  
  Modelo de layout de cards/grupos/campos.

- `spikes/desktop-editor-shell/EditorShell/EditorIcons.cs`  
  Resolución de iconos del editor desde `assets/system/system_icons`, con fallback a path data interno.

### Capas principales

#### Tree/navigation

Actualmente se construye en `SpikeDatabase.LoadTree()` y se renderiza en `MainWindow.axaml.cs`.

Tipos principales:

- `ProjectTreeNodeKind`
- `ProjectTreeNode`
- `BuildNavigationCards`
- `CreateNavigationCard`
- `CreateNavigationHeader`
- `CreateNavigationRow`
- `CreatePaletteNavigationRow`
- `CreateNavigationActions`
- `CreateNavigationSelectButton`
- `CreateNavigationToggleButton`

La navegación ya usa el modelo de cards por niveles, no un TreeView estándar. Se decidió esto porque se parece más al sistema antiguo y permite una metáfora visual uniforme con editor cards.

Estructura objetivo actual:

```text
Project
  Episodes
    Episode
      Shot
  Apps
    App
      Module
  Production Data
    Actors
    Devices
    future production tables
  System Data
    Palette Colors
    future system tables
```

El proyecto sí tiene editor propio. Los grupos como Apps, Episodes, Production Data, System Data son contenedores. Apps contiene rows de apps; cada app contiene modules.

#### Editor cards

Se renderizan en:

- `BuildEditorCards(ProjectTreeNode node)`
- `CreateLayoutCard(ProjectTreeNode node, EditorLayoutCard layoutCard)`
- `AddEditorCard(Expander card)`

El layout viene de DB:

- tabla `editor_layouts`;
- método `SpikeDatabase.LoadEditorLayout(recordClassId)`;
- seed en `SpikeDatabase.SeedEditorLayoutsIfMissing`.

Cada layout contiene:

- cards;
- subtitle;
- icon;
- order;
- visible;
- defaultOpen;
- groups;
- fields.

Los cards son `Expander` nativos con estilos Suki. La intención es que el layout de editores viva en DB y el código no tenga que decidir la estructura visual campo a campo.

#### Diccionario de tipos

Actual en `EditorShell/FieldDefinition.cs`:

```csharp
internal enum ValueKind
{
    StringSingleLine,
    StringReadOnly,
    StringMultiline,
    Integer,
    IntegerPair,
    DirectoryPath,
    ImageFilePath,
    OptionToken,
    HexColor,
    PaletteColorToken,
    PaletteColorPair,
    Boolean,
}
```

`FieldDefinition` declara:

- `Id`
- `Label`
- `ValueKind`
- `IsEditable`
- `DefaultValue`
- `CommitAsDefault`
- `Options`

`FieldValue.IsDefault` compara `Value` con `Definition.DefaultValue`.

#### Controles encapsulados

`DictionaryFieldControl` recibe un `FieldValue` y crea el control según `ValueKind`.

Controles actuales:

- `StringSingleLine`, `StringReadOnly`, `StringMultiline` → `TextBox`;
- `Integer` → por ahora `TextBox`;
- `IntegerPair` → control compuesto X/Y o pair genérico;
- `DirectoryPath`, `ImageFilePath` → `TextBox` + botón Browse;
- `OptionToken`, `PaletteColorToken` → `ComboBox` nativo Suki;
- `PaletteColorPair` → par light/dark con dos ComboBox;
- `HexColor` → swatch + TextBox + `ColorPicker`;
- `Boolean` → `CheckBox`.

Nota importante: aún falta extraer el switch de `DictionaryFieldControl` a un registry/factory de controles. Está documentado como deuda en `docs/architecture/editor_shell_non_negotiables.md`.

#### Persistencia/SQLite

Actual en `SpikeDatabase.cs`.

Tablas existentes del spike:

- `projects`
- `episodes`
- `shots`
- `apps`
- `modules`
- `palette_colors`
- `devices`
- `actors`
- `editor_layouts`

El spike no está usando la DB antigua directamente. Se decidió avanzar con una DB más pequeña y limpia por niveles, basada en la antigua pero sin arrastrar todos los fallbacks/legacy.

Mutaciones principales:

- `AddChild`
- `DuplicateNode`
- `DeleteNode`
- `RenameNode`
- `UpdateProjectField`
- `UpdateEpisodeField`
- `UpdatePaletteColorField`
- `UpdateDeviceField`
- `UpdateActorField`

Seeds:

- `SeedInitialDataIfEmpty`
- `SeedPaletteColorsIfEmpty`
- `SeedDevicesIfEmpty`
- `SeedActorsIfEmpty`
- `SeedEditorLayoutsIfMissing`

#### Preview/avatar/render

El preview del nuevo spike todavía es placeholder visual en `MainWindow.axaml`, no el runtime web real.

Hay un preview específico de avatar de actor en:

- `CreateActorAvatarPreview`
- `WrapAvatarPreview`
- `ResolveLocalPath`
- `RelativePathIfInsideMediaRoot`
- `CurrentMediaRoot`
- `PaletteBrush`
- `ActorInitials`

Este preview de avatar sí refleja:

- imagen;
- crop 640-like viewport simplificado a 160x160;
- scale;
- offset;
- initials;
- actor color;
- actor text color.

El preview/render final que hay que conservar como base sigue estando en React/web:

- `src/visual/*`
- `src/debug-ui/preview/*`
- `src/remotion/*`

## 4. Comparativa implementada React/Electron vs Avalonia/Suki

### Project tree/navigation

Antes:

- React tenía panel izquierdo con estructura tipo card/tree custom.
- La navegación había evolucionado para abrir niveles y seleccionar editor al hacer click sobre filas.
- Había muchas decisiones visuales en CSS propio.

Ahora:

- Avalonia/Suki usa cards por niveles, no TreeView estándar.
- `ProjectTreeNode` modela el árbol.
- Los top-level groups son Project, Apps, Production Data y System Data.
- Episodes cuelga de Project, Apps de Project, data roots también de Project.
- El comportamiento apunta a cards excluyentes por nivel.
- Iconos se resuelven con `EditorIcons`.

Implementado:

- navegación por cards;
- add/duplicate/delete en nodos que lo permiten;
- click de row selecciona editor;
- estructura Project → Episodes → Shots y Apps → Modules;
- Production Data con Actors/Devices;
- System Data con Palette Colors;
- persistencia DB para add/duplicate/delete/rename.

Incompleto:

- pulido final de UI/spacing/iconos;
- validaciones de delete/uso;
- algunos botones/iconos recién añadidos están en assets untracked;
- no está integrado con runtime web real.

Mejora:

- se abandona TreeView estándar porque limitaba la metáfora visual;
- se simplifica la navegación a componentes compartidos;
- no hay que recrear un árbol distinto por editor.

### Apps

Antes:

- Apps y modules existían en la DB antigua y en el panel izquierdo.
- El modelo mezclaba apps/module instances/screen instances.

Ahora:

- `apps` es tabla explícita del spike.
- `modules` cuelga de cada row de app.
- `App` y `Module` aparecen en navegación.
- Record classes seeded: `app.generic`, `app.core.chat`, `module.generic`, `module.core.chat`.

Implementado:

- tabla `apps`;
- tabla `modules`;
- add/duplicate/delete;
- módulos cuelgan de apps;
- seed de `Chat` + `Chat Module`.

Incompleto:

- editores específicos de app/module;
- contenido de chat module;
- apps/module defaults reales;
- relación con screens de shot.

Mejora:

- app/module se separan como clases/prototipos, no como hacks de screen instance.

### Production Data

Antes:

- Production Data agrupaba actores, devices, production fonts, themes, etc.

Ahora:

- Production Data existe como grupo visual bajo Project.
- Contiene Actors y Devices.

Implementado:

- `ActorsRoot`;
- `DevicesRoot`;
- editores iniciales de actors/devices.

Incompleto:

- production fonts;
- production themes;
- otros datos de producción.

Mejora:

- se está reintroduciendo tabla por tabla, no importando todo legacy de golpe.

### System Data

Antes:

- System Data agrupaba icon themes, status bars, navigation bars, palette, render presets, animation presets, etc.

Ahora:

- System Data existe como grupo visual bajo Project.
- Contiene Palette Colors.

Implementado:

- `PaletteRoot`;
- `palette_colors`.

Incompleto:

- icon themes;
- status/navigation bars;
- render presets;
- component classes;
- animation presets;
- fonts/icons import workflow.

Mejora:

- System Data no se puebla con filas provisionales hasta que cada editor esté realmente modelado.

### Palettes

Antes:

- Paleta era tabla de colores primitivos por producción.
- Se hizo limpieza de neutros (`gray_010`, etc.), pasteles para actores, `red` de debug.
- Pickers debían elegir tokens de paleta, no colores literales.

Ahora:

- Tabla `palette_colors` en spike.
- Campos:
  - `token`
  - `value_hex`
  - `metadata_json`
  - `is_neutral`
- Navigation row muestra swatch y marker de usado/no usado.
- Editor usa:
  - `palette.token`
  - `palette.valueHex`
  - `palette.isNeutral`
  - `palette.source`
  - `palette.protected`
  - `palette.hiddenFromPickers`
  - `palette.note`

Implementado:

- seed con básicos y neutros;
- add/delete/duplicate;
- ColorPicker del sistema para `HexColor`;
- swatch en editor/nav;
- `FieldOption.ToString()` para ComboBox nativo.

Incompleto:

- referencia real de “used” contra themes/componentes aún no existe porque esas tablas no están migradas;
- picker modal estilo antiguo de paleta aún no está reconstruido;
- restricciones de borrar/renombrar si usado.

Mejora:

- paleta ya está más limpia y separada de theme tokens;
- editor usa controles de diccionario.

### Devices

Antes:

- Devices incluía métricas complejas: design space, render size, canvas, screen, viewport, safe area, status bar, dynamic island.
- Algunos pares X/Y W/H estaban en dos campos o layouts manuales.

Ahora:

- Tabla `devices`.
- Campo JSON `metrics_json`.
- Editor expone pares como `IntegerPair`.
- Layout de device vive en `editor_layouts`.

Implementado:

- seeds de iPhone/Samsung/Pixel;
- editor de device;
- pairs en una línea;
- datos guardan en `metrics_json`;
- icono `editor_device.svg` está previsto en `EditorIcons`.

Incompleto:

- validación numérica real;
- control numérico no es aún `NumericUpDown`, sigue siendo TextBox;
- posible ajuste fino de pair labels/spacing.

Mejora:

- pares conceptuales están modelados como un campo del diccionario, no como dos controles sueltos.

### Actors

Antes:

- Actor tenía display/short name, default device/theme, colores light/dark, avatar image, scale, offset, initials, initials padding.
- Avatar preview/crop existía en React y también afectaba preview real.

Ahora:

- Tabla `actors`.
- Metadata JSON para modes/avatar.
- Editor actor con cards:
  - General;
  - Colors;
  - Avatar.
- Default device usa `OptionToken` con opciones desde `devices`.
- Default theme usa `OptionToken`, pero todavía no hay tabla de themes nueva; por ahora muestra opción placeholder.
- Colores actor/avatar text son `PaletteColorPair`.
- Avatar image es `ImageFilePath`.
- Avatar offset es `IntegerPair`.
- Preview local de avatar se pinta en el card Avatar.

Implementado:

- editor de Actor;
- add/duplicate/delete;
- default device dropdown desde devices;
- default theme control preparado;
- palette pair controls;
- avatar image browse;
- rutas relativas al media root cuando aplica;
- preview de avatar se actualiza y resuelve ruta;
- bug de freeze al seleccionar actor arreglado en `4ba3bbd`.

Incompleto:

- default theme necesita tabla `themes`;
- commit de campos aún es por cambio local, no por salida de campo;
- browse/preview debe seguir siendo revisado al conectar runtime real.

Mejora:

- la selección de actor ya no debería congelar por rebuild loop;
- se eliminó render manual custom de opciones de ComboBox para dejar Suki nativo.

### Editor cards

Antes:

- React usaba `EditorSectionCard`, `EditorSubsectionAccordion`, CSS propio y muchas variaciones.
- Había tendencia a crear clases/formatos puntuales.

Ahora:

- Avalonia usa `Expander` nativo con Suki.
- Layout de cards sale de DB (`editor_layouts`) por `record_class_id`.
- Cards tienen icon, label, subtitle, order, visible, defaultOpen.
- Grupos y campos se declaran en JSON.

Implementado:

- cards genéricas para editores;
- subtitles;
- iconos por card;
- exclusividad parcial;
- estructura definida en DB.

Incompleto:

- indicator amber de card/subcard por cambios no default;
- subtitulos/íconos finales de todos los editores;
- refactor de MainWindow para sacar componentes.

Mejora:

- el código no debería definir a mano la estructura de cada editor salvo mapping temporal de fields.

### Field/type dictionary

Antes:

- React había llegado a un `ValueRegistry` y `ValueKindControlRegistry`, pero convivía con rutas manuales.
- Mucho tiempo se perdió por labels verdes/controles grises, CSS que sobreescribía controles y editores que pintaban cosas a mano.

Ahora:

- `FieldDefinition`, `ValueKind`, `FieldValue`, `DictionaryFieldControl`.
- Todos los campos actuales de Project/Episode/Palette/Device/Actor pasan por `DictionaryFieldControl`.

Implementado:

- tipos básicos;
- restore button;
- default/changed state;
- Browse;
- ColorPicker;
- ComboBox nativo;
- pairs.

Incompleto:

- registry/factory de controles separado;
- validación real por tipo;
- commit-on-blur/generalized commit;
- herencia real;
- animatable field metadata.

Mejora:

- el spike está evitando la doble ruta manual/dictionary.

### Palette picker

Antes:

- React tenía una idea avanzada de picker tipo swatch grid, ordenado por hue/saturation, con selected token y RGBA/HEX.

Ahora:

- No está reconstruido como modal custom.
- Se usan `ComboBox` para tokens de paleta y `ColorPicker` para editar `HexColor`.

Implementado:

- palette token dropdown básico;
- ColorPicker para valor HEX;
- swatches en navegación/editor.

Incompleto:

- modal/grid de paleta;
- selección por swatches;
- alpha.

Mejora:

- por ahora se apoya en Suki/Avalonia nativo para no reintroducir CSS/control custom prematuro.

### Avatar image/crop/preview

Antes:

- Actor devolvía imagen/avatar considerando scale/offset/initials.
- El preview real de chat usaba estos datos.

Ahora:

- `CreateActorAvatarPreview` crea viewport 160x160 como preview editor.
- Usa ruta relativa si está dentro de `media_root`.
- Aplica `Stretch.UniformToFill`, scale y translate.
- Si falla imagen o `useInitials`, pinta iniciales.

Implementado:

- preview local en Actor/Avatar card;
- scale/offset;
- initials fallback;
- colors desde paleta.

Incompleto:

- no está aún integrado con preview web real;
- no hay clase reusable de avatar compartida con runtime.

Mejora:

- el editor ve un crop útil sin acoplarse todavía al runtime.

### Default device/default theme

Antes:

- Actor tenía defaults ligados a ids existentes.
- Theme era parte de Production Data.

Ahora:

- `actor.defaultDeviceId` usa `GetDeviceOptions(projectId)` y `ValueKind.OptionToken`.
- `actor.defaultThemeId` usa `GetThemeOptions(projectId)`, pero themes aún no están migrados.

Implementado:

- default device funcional;
- default theme control preparado.

Incompleto:

- tabla/editor de themes nuevo;
- filtrar themes por proyecto cuando exista.

Mejora:

- se usa el mismo tipo `OptionToken`, no un dropdown hecho ad hoc.

### Preview/render CSS

Antes:

- Preview/render real estaba en React/CSS/Remotion.
- Chat visual, bubbles, keyboard, header, status, animations, media/video/audio estaban bastante avanzados.

Ahora:

- El panel derecho del spike es placeholder de preview, escrito en XAML.
- No pretende reemplazar el runtime.

Implementado:

- placeholder visual suficiente para probar shell de tres paneles.

Incompleto:

- embebido real del runtime web;
- comunicación editor → resolved/frame model → preview;
- render Remotion desde shell nuevo.

Mejora:

- el spike no duplica todavía toda la complejidad visual; conserva la frontera correcta.

## 5. Decisiones de arquitectura ya tomadas

### Patrones nuevos que deben respetarse

- Todo campo editable debe tener `FieldDefinition`.
- Todo tipo de valor debe pasar por `ValueKind`.
- Todo control de valor debe salir de `DictionaryFieldControl` o, próximamente, del registry/factory de controles del diccionario.
- Un editor puede decidir layout, cards, grupos, visibilidad y orden; no puede inventar controles de valor.
- La estructura de cards debe vivir en `editor_layouts`, no hardcodearse editor por editor.
- Navegación y editor deben usar Suki/native + clases comunes nuestras, no estilos puntuales.
- Preview/render web se conserva como runtime visual.
- La DB del spike crece por niveles limpios, no por migraciones legacy grandes.

### Patrones legacy que NO deben copiarse

- CSS específico por editor/campo;
- dropdowns, color pickers, number inputs o pairs hechos a mano por editor;
- fallback paths para “DBs antiguas” dentro del spike;
- duplicar runtime visual en desktop;
- route paralelo manual + dictionary para el mismo dato;
- guardar valores por cada stroke como comportamiento definitivo.

### Lógica antigua que sí sigue siendo fuente de verdad

- Composición visual y render:
  - `src/visual/*`
  - `src/remotion/*`
  - `src/debug-ui/preview/*`
- Reglas de módulos de chat ya aprendidas:
  - header;
  - bubbles;
  - tails;
  - media;
  - audio/video;
  - keyboard/text input;
  - animation.
- Modelo conceptual de paleta/theme/fonts/icons/components.
- Campos existentes útiles en React, siempre reinterpretados hacia el nuevo modelo.

### Dónde se permite mejorar comportamiento respecto a la app vieja

- Layout de editores desde DB.
- Suki native controls en vez de CSS custom.
- Commit de campos centralizado.
- Eliminación de app instance si no aporta valor.
- Screens/shots pueden rediseñarse según el modelo nuevo:
  - shot contiene screens;
  - screen puede corresponder a app/module;
  - device state/theme pueden moverse a shot/screen según se formalice.

## 6. Estado actual exacto

### Últimos commits relevantes

```text
4ba3bbd Fix actor editor selection freeze
5629583 Refine desktop actor fields
84511d4 Add desktop actor editor
31d564b Restore navigation card subtitles
47e523f Restore top-level navigation groups
01755f5 Group desktop navigation data roots
d6e2f70 Add desktop device editor
a0c3778 Add desktop palette editor controls
```

### Cambios no commiteados

En el momento de este handoff, antes de crear este documento, el branch estaba limpio respecto a código commiteado, pero había muchos archivos no trackeados de iconos/scripts generados por el usuario o por utilidades:

- `assets/FOQN_S2/icon-themes/*/add.svg`
- `assets/icon-themes/*/editor_*.svg`
- `assets/icon-themes/*/system_duplicate.svg`
- `assets/icon-themes/_licenses/*`
- `docs/architecture/editor_icon_theme_script_prompt.md`
- `scripts/icon-themes/_licenses/`
- `scripts/icon-themes/add-editor-material-icons-prompt-weight.cjs`
- `scripts/icon-themes/material-rounded-200/`

No meter estos archivos en commits sin revisar con el usuario. Puede que algunos deban incorporarse después, especialmente iconos del sistema como `system_add.svg`, `system_delete.svg`, `editor_device.svg`, `editor_shot.svg`.

Este handoff crea un nuevo archivo:

- `docs/exchange/codex_handoffs/2026-07-01_mockups_react_to_avalonia_handoff.md`

### Bugs pendientes

- Commit de campos es todavía demasiado inmediato. Muchos controles disparan update en `TextChanged`/`SelectionChanged`. Debe generalizarse un sistema de commit al salir de campo, aceptar picker, toggle booleano o gesto explícito.
- `DictionaryFieldControl` mezcla shell/row host y factory de controles; hay que extraer registry/factory.
- `Integer` todavía usa TextBox; falta validación numérica real.
- `OptionToken`/ComboBox funciona, pero el control final de dropdowns y labels puede necesitar pulido Suki.
- `GetThemeOptions` existe pero no hay tabla themes nueva; actor default theme es placeholder.
- Preview derecho del spike no está conectado al runtime web.
- Tree/cards necesitan pasada de UI final cuando la estructura esté más completa.
- Palette used marker aún no escanea themes/componentes porque esas tablas no existen en spike.

### Bugs recién arreglados

- Freeze al seleccionar Actor: arreglado en `4ba3bbd`.
  - Causa: campos de avatar emitían `ValueChanged` al inicializar, `ApplyFieldValue` actualizaba actor y reconstruía el editor, generando loop.
  - Fix inmediato: same-value guard en actor branch antes de actualizar/reconstruir.
  - Deuda documentada: generalizar commit-on-field-exit.

- ComboBox de opciones: simplificado para usar rendering nativo Suki.
  - Se quitó `ItemTemplate` custom en `DictionaryFieldControl`.
  - `FieldOption.ToString()` devuelve `Label`.

### Archivos modificados recientemente

- `spikes/desktop-editor-shell/MainWindow.axaml.cs`
- `spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs`
- `spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs`
- `docs/architecture/editor_shell_non_negotiables.md`

### Comandos de build/test/run

Para compilar el spike:

```bash
dotnet build spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj
```

Para ejecutar el spike:

```bash
dotnet run --project spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj
```

Atajo ejecutable creado previamente en raíz:

```bash
./run-desktop-editor.command
```

Para la app React/Electron antigua:

```bash
npm run debug
npm run electron
npm run test
npm run validate:editor-dictionary
```

### Resultado del último build

Último build ejecutado:

```text
dotnet build spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj
Compilación correcta.
0 errores.
2 warnings.
```

Warnings conocidos:

- `NU1903`: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 tiene vulnerabilidad alta conocida. No está relacionado con el bloqueo de Actor.

## 7. Guía para continuar editor por editor

### Project

Fuente antigua a leer:

- `src/domain/fields/recordColumnFields.ts`
- `src/debug-ui/editors/GenericRecordEditor.tsx`
- `src/debug-ui/editors/RecordFieldRenderer.tsx`

Destino nuevo:

- `SpikeDatabase.GetProjectSettings`
- `SpikeDatabase.UpdateProjectField`
- `MainWindow.FieldForLayout`
- `editor_layouts` para `record_class_id = "project"`

Datos:

- `projects.name`
- `projects.slug`
- `projects.default_fps`
- `projects.media_root`
- `projects.notes`

Controles:

- `StringSingleLine`
- `Integer`
- `DirectoryPath`
- `StringMultiline`

Riesgos:

- Media root debe ser path real; browse debe resolver directorios.
- No guardar cada stroke como definitivo a largo plazo.

### Episodes

Fuente antigua:

- `src/domain/fields/episodeFields.ts`
- `src/debug-ui/editors/GenericRecordEditor.tsx`

Destino nuevo:

- `SpikeDatabase.GetEpisodeSettings`
- `SpikeDatabase.UpdateEpisodeField`
- layout `record_class_id = "episode"`

Datos:

- `episodes.name`
- `episodes.slug`
- `episodes.sort_order`
- `episodes.notes`

Controles:

- `StringSingleLine`
- `Integer`
- `StringMultiline`

Riesgos:

- `sort_order` debería acabar siendo interno o derivado por orden visual si el usuario decide eso.

### Shots

Fuente antigua:

- `src/domain/fields/shotFields.ts`
- `src/debug-ui/editors/ShotFields.tsx`
- `src/domain/timeline/screenTimeline.ts`

Destino nuevo:

- tabla `shots` ya existe;
- falta editor completo en Avalonia;
- layout `record_class_id = "shot"` existe de forma básica.

Datos:

- `shots.name`
- `shots.notes`
- `shots.fps`
- `shots.duration_frames`
- futuro: device state, theme, screen ordering.

Controles:

- `StringSingleLine`
- `Integer`
- futuros OptionToken para device/theme/owner actor.

Riesgos:

- Modelo temporal fue discutido: screens deben tener duración; start se deriva por orden; transiciones se definen por duración antes del corte.
- No copiar app instance antigua si se decide eliminarla.

### Apps

Fuente antigua:

- `src/debug-ui/editors/AppRecordEditor.tsx`
- `src/debug-ui/editors/AppEditor.tsx`
- `src/debug-ui/editors/AppMediaFields.tsx`
- `src/domain/schemas/app.ts`

Destino nuevo:

- tabla `apps`;
- layout `app.generic`, `app.core.chat`;
- `SpikeDatabase.AddChild`, `DuplicateNode`, `DeleteNode`.

Datos:

- `apps.name`
- `apps.notes`
- `apps.record_class_id`
- futuros app defaults.

Controles:

- `StringSingleLine`
- `StringMultiline`
- futuros component/theme token controls.

Riesgos:

- App es clase/prototipo, no screen instance.
- Modules deben colgar de app rows.

### Modules / core.chat

Fuente antigua:

- `src/debug-ui/editors/ModuleThemeConfigRecordEditor.tsx`
- `src/debug-ui/editors/ModuleThemeConfigEditor.tsx`
- `src/debug-ui/editors/ModuleInstanceEditor.tsx`
- `src/debug-ui/editors/chat/*`
- `src/domain/fields/chatFields.ts`
- `src/debug-ui/module-editor-hints/coreChatV1.ts`
- `src/debug-ui/field-descriptors/coreChatV1Descriptors.ts`

Destino nuevo:

- tabla `modules`;
- layout `module.generic`, `module.core.chat`;
- futuros records para module class/defaults/content.

Datos:

- module defaults de header/bubbles/status/media/keyboard/text input;
- mensajes/content;
- animation metadata.

Controles:

- `OptionToken`
- `Boolean`
- `Integer`
- `IntegerPair`
- futuros:
  - `themeColorToken`
  - `iconToken`
  - `iconTokenList`
  - `surfaceStyle`
  - `componentOverride`
  - `fontSelector`
  - `animationTrack`

Riesgos:

- No migrar literalmente todos los grupos React.
- Primero crear tipos/controles de diccionario que falten.
- Mantener componentes reusables: avatar, label, keyboard, audio/video, button_icon, style.

### Palette Colors

Fuente antigua:

- `src/domain/schemas/paletteColor.ts`
- `src/debug-ui/editors/paletteUsage.ts`
- `src/debug-ui/editor-ui/DictionaryFieldControl.tsx`

Destino nuevo:

- tabla `palette_colors`;
- `SpikeDatabase.GetPaletteColorSettings`
- `UpdatePaletteColorField`
- `GetPaletteColorOptions`
- layout `palette_color`.

Datos:

- token;
- value_hex;
- is_neutral;
- metadata source/protected/hiddenFromPickers/note.

Controles:

- `StringSingleLine`
- `HexColor`
- `Boolean`
- `StringMultiline`

Riesgos:

- Borrado/rename debe revisar referencias cuando existan themes/componentes.

### Devices

Fuente antigua:

- `src/domain/schemas/device.ts`
- `src/domain/fields/deviceFields.ts`

Destino nuevo:

- tabla `devices`;
- `metrics_json`;
- `SpikeDatabase.GetDeviceSettings`
- `GetDeviceMetricFieldValue`
- `UpdateDeviceField`

Datos:

- manufacturer;
- model;
- osFamily;
- designSpace;
- renderSize;
- scaleToPixels;
- pixelRatio;
- defaultScreenScale;
- canvas/screen/viewport/safe/status/dynamicIsland metrics.

Controles:

- `StringSingleLine`
- `Integer`
- `IntegerPair`

Riesgos:

- IntegerPair debe seguir siendo unidad lógica.
- Más adelante usar controles numéricos reales.

### Actors

Fuente antigua:

- `src/debug-ui/editors/ActorFields.tsx`
- `src/domain/schemas/actor.ts`
- preview usage in `src/visual/modules/atomic/AvatarModule.ts`

Destino nuevo:

- tabla `actors`;
- `metadata_json`;
- `SpikeDatabase.GetActorSettings`
- `GetActorFieldValue`
- `UpdateActorField`
- `CreateActorAvatarPreview`

Datos:

- display_name;
- short_name;
- default_device_id;
- default_theme_id;
- modes.light/dark.color;
- modes.light/dark.avatarTextColor;
- avatar.filePath;
- avatar.scale;
- avatar.offsetX/Y;
- avatar.useInitials;
- avatar.initialsPadding.

Controles:

- `StringSingleLine`
- `OptionToken`
- `PaletteColorPair`
- `ImageFilePath`
- `IntegerPair`
- `Boolean`
- `Integer`

Riesgos:

- No volver a introducir rebuild loops.
- File path debe guardarse relativo a media root si aplica.
- Default theme pendiente de tabla themes.

### Themes

Fuente antigua:

- `src/debug-ui/editors/ThemeRecordEditor.tsx`
- `src/debug-ui/editors/ThemeEditor.tsx`
- `src/debug-ui/editors/ThemeFields.tsx`
- `src/domain/fields/themeFields.ts`
- `src/domain/schemas/theme.ts`
- `docs/architecture/16_theme_editor_dictionary_audit.md`

Destino nuevo:

- aún no creado en spike.
- probablemente Production Data.

Datos:

- typography;
- colors;
- radii;
- shadows;
- relief;
- icons/borders tokens;
- neutral tint hue/saturation.

Controles necesarios:

- typography selector;
- palette token selector;
- numeric sliders;
- hue slider;
- surface style/component override.

Riesgos:

- No reintroducir tokens literales fuera de paleta/theme.
- Theme tokens deben ser por modo donde aplique.

### Component Classes

Fuente antigua:

- `src/debug-ui/editors/ComponentClassRecordEditor.tsx`
- `src/domain/schemas/componentClass.ts`
- `src/domain/value-system/SurfaceStyleDefinition.ts`

Destino nuevo:

- aún no creado en spike.

Datos:

- avatar;
- label;
- keyboard;
- text input;
- button icon;
- audio/video/media;
- style/surface style.

Controles:

- `componentOverride`
- `surfaceStyle`
- `themeColorToken`
- `iconToken`
- `fontSelector`

Riesgos:

- Esta parte fue compleja en React. Hacer primero el tipo/control genérico antes de editar componentes concretos.

### Icon Themes / System Icons

Fuente antigua:

- `src/icon-themes/importDevelopmentIconTheme.ts`
- `src/domain/iconThemes/iconThemeMapping.ts`
- `scripts/icon-themes/*`
- `assets/icon-themes/*`

Destino nuevo:

- `EditorIcons.cs` para iconos del editor;
- `assets/system/system_icons` para iconos internos de UI del editor.

Datos:

- por ahora iconos SVG leídos por path simple.

Controles:

- futuro `iconToken` / `iconTokenList`.

Riesgos:

- Hay muchos iconos untracked; revisar antes de commitear.
- Editor system icons no son lo mismo que production icon themes.

## 8. Próximos pasos recomendados

Orden sugerido:

1. Verificar que seleccionar Actor ya no bloquea.
2. Commit del handoff si el usuario lo quiere versionado.
3. Consolidar navegación:
   - Project / Episodes / Apps / Production Data / System Data con nombres/subtítulos visibles;
   - cards excluyentes;
   - iconos correctos (`system_add`, `system_delete`, `system_duplicate`, `editor_shot`, `editor_device`).
4. Resolver untracked icon assets:
   - decidir qué iconos entran;
   - mover solo `assets/system/system_icons` si son de UI interna;
   - no mezclar con `assets/icon-themes` de producción salvo paso dedicado.
5. Generalizar commit de campos:
   - estado local en `DictionaryFieldControl`;
   - commit en LostFocus/Enter/picker accept/toggle;
   - same-value guard común en shell/persistencia;
   - evitar `TextChanged` como persistencia final.
6. Extraer registry/factory:
   - `ValueKind` → control class;
   - `DictionaryFieldControl` queda como row host.
7. Completar Actors visualmente:
   - palette picker real;
   - default theme cuando exista tabla themes;
   - revisar path relativo/media root.
8. Migrar Themes en spike:
   - tabla limpia;
   - editor layout en `editor_layouts`;
   - typography/color/radii/shadow/relief controls.
9. Migrar Component Classes con cuidado:
   - primero `surfaceStyle`;
   - luego `componentOverride`.
10. Conectar preview web real:
   - mantener React/runtime como fuente visual;
   - no duplicar CSS/render en Avalonia.

Qué validar visualmente:

- abrir/cerrar cards del tree y editor;
- selección de filas con editor;
- add/duplicate/delete por nivel;
- labels/subtitles/iconos;
- Actor selecciona sin freeze;
- avatar preview se actualiza;
- palette dropdowns usan tema Suki;
- dark/light no deja labels negros o controles no themed.

Siguiente corte estable recomendado:

- Si el handoff se guarda sin más cambios funcionales:

```bash
git add docs/exchange/codex_handoffs/2026-07-01_mockups_react_to_avalonia_handoff.md
git commit -m "Add Avalonia migration handoff"
git push
```

- Si se retoma implementación antes de commit:
  - no mezclar handoff con iconos untracked;
  - hacer commit pequeño por área;
  - compilar siempre `dotnet build spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj`.

## Regla final para el próximo Codex

No intentes “terminar la app” migrando React a Avalonia por fuerza bruta. Este branch va mucho mejor cuando avanza en capas pequeñas:

1. tabla/datos;
2. field definitions;
3. value kind/control;
4. editor layout JSON;
5. navegación;
6. preview/runtime.

Si un campo necesita un control nuevo, crea primero el tipo/control en el diccionario. Si una UI empieza a necesitar excepciones por editor, parar y subir la abstracción.
