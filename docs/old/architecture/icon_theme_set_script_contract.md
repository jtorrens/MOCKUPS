# Icon theme set script contract

Este documento define el contrato que deben seguir los scripts externos que descarguen o actualicen iconos para MOCKUPS.

El objetivo es que podamos pedir a un script: “añade estos tokens de icono a todos los sets”, y que el resultado sea compatible con el editor `Icon themes`, el resolver de temas y el botón `Refresh sets`.

## Estructura de directorios esperada

Cada producción tiene un `mediaRoot`. Dentro de ese root, los temas de iconos viven en:

```text
<mediaRoot>/icon-themes/
  lucide-basic/
    nav_chevron_left.svg
    media_video.svg
    ...
  lucide-semibold/
    nav_chevron_left.svg
    media_video.svg
    ...
  material-rounded-basic/
    nav_chevron_left.svg
    media_video.svg
    ...
```

Regla estricta:

- Todos los sets hermanos dentro de `icon-themes/` deben contener los mismos tokens.
- El nombre de archivo es el token lógico.
- El archivo debe ser SVG.
- El token se calcula quitando `.svg`.

Ejemplo:

```text
chat_send.svg -> token chat_send
media_mic.svg -> token media_mic
```

## Naming de tokens

Formato recomendado:

```text
category_name_detail
```

Ejemplos:

```text
app_language
chat_send
chat_emoji
media_mic
media_video
nav_chevron_left
message_done_all
```

Reglas:

- minúsculas
- `snake_case`
- sin espacios
- sin prefijo de set o librería
- estable entre todos los sets

La categoría se deriva por defecto del primer segmento:

```text
media_video -> media
nav_chevron_left -> nav
```

## Qué debe hacer el script externo

Entrada mínima sugerida:

```json
{
  "iconThemesRoot": "/Volumes/.../FOQN_S2/icon-themes",
  "tokens": [
    {
      "token": "chat_send",
      "description": "Send message icon",
      "sources": {
        "lucide-basic": {
          "repository": "lucide-icons/lucide",
          "sourceName": "send"
        },
        "lucide-semibold": {
          "repository": "lucide-icons/lucide",
          "sourceName": "send",
          "strokeWidth": 2.5
        },
        "material-rounded-basic": {
          "repository": "google/material-design-icons",
          "sourceName": "send"
        }
      }
    }
  ]
}
```

Salida esperada:

- Un SVG por token en cada set.
- Todos los sets deben quedar completos.
- Si un icono no se puede descargar/generar para un set, el script debe fallar y no dejar el set a medias.

## Requisitos de los SVG

Los SVG deben:

- Ser monocromos o compatibles con `currentColor` cuando sea posible.
- No depender de CSS externo.
- No incluir raster embebido.
- Tener `viewBox`.
- Mantener tamaño lógico consistente dentro del set.
- No incluir metadatos innecesarios o IDs aleatorios si se puede evitar.

Recomendación:

```xml
<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" ...>
```

Para sets rellenos, `fill="currentColor"` es correcto.

## Manifest opcional

El sistema actual no necesita manifest para funcionar, porque el botón `Refresh sets` escanea los SVG por nombre.

Aun así, el script puede generar un manifest auxiliar para auditoría:

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-06-29T00:00:00.000Z",
  "sets": ["lucide-basic", "lucide-semibold", "material-rounded-basic"],
  "tokens": {
    "chat_send": {
      "category": "chat",
      "description": "Send message icon",
      "sources": {
        "lucide-basic": "send",
        "lucide-semibold": "send",
        "material-rounded-basic": "send"
      }
    }
  }
}
```

Este manifest no es fuente de verdad; la fuente de verdad son los archivos SVG presentes en todos los sets.

## Cómo lo incorpora MOCKUPS

El botón `Refresh sets` del editor `Icon themes`:

1. Localiza el directorio del set actual.
2. Sube un nivel hasta `icon-themes/`.
3. Escanea todos los subdirectorios de sets.
4. Calcula la intersección de tokens presentes en todos los sets.
5. Actualiza `mapping_json.tokens` solo con esa intersección.
6. Omite cualquier token que falte en al menos un set.

Esto evita que cambiar de set rompa iconos en preview/render.

## Checklist antes de entregar un set

- [ ] Todos los sets tienen exactamente los mismos `.svg`.
- [ ] No hay SVG con nombres fuera de `snake_case`.
- [ ] Los tokens representan la función, no la librería.
- [ ] Los SVG usan `currentColor` cuando aplica.
- [ ] El script falla si no puede completar todos los sets.
- [ ] El editor `Icon themes > Refresh sets` no omite tokens inesperados.
