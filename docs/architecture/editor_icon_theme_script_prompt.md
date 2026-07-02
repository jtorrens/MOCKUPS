# Encargo para ChatGPT/Codex: script para añadir iconos de editor a icon themes

Necesito un script externo para añadir un conjunto concreto de iconos SVG a los sets de iconos de MOCKUPS.

El script debe respetar el contrato general descrito en:

```text
docs/architecture/icon_theme_set_script_contract.md
```

## Objetivo

Dado un directorio raíz de icon themes, descargar/generar los SVG necesarios para estos tokens y guardarlos en todos los sets Material que existan bajo ese directorio.

El objetivo inmediato es que el nuevo editor desktop Avalonia/Suki pueda usar los mismos tokens de icono que el resto del sistema, sin iconos hardcodeados provisionales.

## Directorio de entrada

El script debe recibir como argumento:

```text
<iconThemesRoot>
```

Ejemplo:

```text
/Volumes/SD_02/PROYECTOS/MOCKUPS/assets/icon-themes
```

o:

```text
/Volumes/SD_02/PROYECTOS/MOCKUPS/assets/FOQN_S2/icon-themes
```

Dentro estarán los sets:

```text
material-rounded-basic/
material-rounded-600/
material-outlined-basic/
material-outlined-600/
lucide-basic/
...
```

Para esta primera pasada, prioriza Material. Lucide puede dejarse para una segunda pasada si no hay candidatos buenos.

## Sets objetivo

Actualizar, si existen:

```text
material-rounded-basic
material-rounded-600
material-outlined-basic
material-outlined-600
```

No crear sets nuevos salvo que se pida explícitamente.

## Reglas estrictas

- El nombre del archivo debe ser exactamente `<token>.svg`.
- Los tokens deben ser `snake_case`.
- Los SVG deben ser monocromos y compatibles con `currentColor`.
- Los SVG deben tener `viewBox`.
- El script debe fallar si no puede completar un token en todos los sets Material objetivo.
- No debe borrar iconos existentes.
- No debe reescribir iconos existentes salvo con flag explícito `--overwrite`.
- Debe imprimir un resumen final:
  - tokens añadidos,
  - tokens omitidos porque ya existían,
  - errores,
  - sets actualizados.

## Tokens requeridos

```text
system_duplicate

editor_general
editor_style
editor_behavior
editor_content
editor_design
editor_layout
editor_header
editor_messages
editor_bubble
editor_avatar
editor_label
editor_media
editor_image
editor_video
editor_audio
editor_tail
editor_keyboard
editor_text_input
editor_button_icon
editor_relief
editor_shadow
editor_border
```

## Candidatos Material sugeridos

Usar estos nombres como candidatos iniciales para buscar/descargar desde Material Symbols.

```text
system_duplicate        content_copy / file_copy / copy_all

editor_general          dashboard / category / apps
editor_style            style / palette / format_paint
editor_behavior         settings / tune / manufacturing
editor_content          article / notes / subject
editor_design           design_services / draw / architecture
editor_layout           view_quilt / dashboard_customize / grid_view
editor_header           vertical_align_top / table_rows / web_asset
editor_messages         forum / chat / sms
editor_bubble           chat_bubble / mode_comment
editor_avatar           account_circle / person
editor_label            label / sell / title
editor_media            perm_media / collections / photo_library
editor_image            image
editor_video            videocam / movie
editor_audio            graphic_eq / audio_file / mic
editor_tail             call_made / subdirectory_arrow_left
editor_keyboard         keyboard
editor_text_input       input / text_fields / short_text
editor_button_icon      smart_button / buttons_alt / radio_button_checked
editor_relief           texture / gradient / blur_on
editor_shadow           layers / filter_none / shadow
editor_border           border_style / border_outer
```

`editor_tail` probablemente no tendrá candidato perfecto. Elegir el más legible y documentarlo en el resumen.

## Parámetros recomendados

El script puede exponer:

```bash
node add-editor-icons.cjs <iconThemesRoot> --dry-run
node add-editor-icons.cjs <iconThemesRoot>
node add-editor-icons.cjs <iconThemesRoot> --overwrite
```

## Salida esperada

Después de ejecutarlo, deberían existir archivos como:

```text
material-rounded-basic/editor_behavior.svg
material-rounded-600/editor_behavior.svg
material-outlined-basic/editor_behavior.svg
material-outlined-600/editor_behavior.svg
```

Y lo mismo para todos los tokens requeridos.

## Integración posterior en MOCKUPS

Después de añadir los SVG:

1. Abrir el editor `Icon themes`.
2. Ejecutar `Refresh sets`.
3. Comprobar que los nuevos tokens aparecen en la tabla.
4. En el desktop shell, mapear los headers del editor y acciones del tree a estos tokens:

```text
Add       -> system_add
Delete    -> system_delete
Duplicate -> system_duplicate

General   -> editor_general
Style     -> editor_style
Behavior  -> editor_behavior
```

