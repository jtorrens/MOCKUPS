#!/usr/bin/env python3
"""Export Material Symbols Rounded SVGs mapped by an HTML table."""

from __future__ import annotations

import argparse
import json
import sys
import tempfile
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from html.parser import HTMLParser
from pathlib import Path


RAW_BASE_URL = "https://raw.githubusercontent.com/google/material-design-icons/master"


@dataclass(frozen=True)
class IconMapping:
    original_name: str
    glyph: str

    @property
    def filename(self) -> str:
        return f"{self.original_name}.svg"

    @property
    def official_path(self) -> str:
        return (
            f"symbols/web/{self.glyph}/materialsymbolsrounded/"
            f"{self.glyph}_24px.svg"
        )

    @property
    def official_url(self) -> str:
        return f"{RAW_BASE_URL}/{self.official_path}"


class MappingTableParser(HTMLParser):
    """Extract table rows without relying on presentation-specific markup."""

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.headers: list[str] = []
        self.rows: list[list[str]] = []
        self._in_header = False
        self._in_row = False
        self._in_cell = False
        self._current_row: list[str] = []
        self._cell_parts: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag == "thead":
            self._in_header = True
        elif tag == "tr":
            self._in_row = True
            self._current_row = []
        elif tag in {"th", "td"} and self._in_row:
            self._in_cell = True
            self._cell_parts = []

    def handle_data(self, data: str) -> None:
        if self._in_cell:
            self._cell_parts.append(data)

    def handle_endtag(self, tag: str) -> None:
        if tag in {"th", "td"} and self._in_cell:
            self._current_row.append(" ".join("".join(self._cell_parts).split()))
            self._in_cell = False
        elif tag == "tr" and self._in_row:
            if self._in_header:
                self.headers = self._current_row
            elif self._current_row:
                self.rows.append(self._current_row)
            self._in_row = False
            self._current_row = []
        elif tag == "thead":
            self._in_header = False


def parse_mappings(input_path: Path) -> list[IconMapping]:
    parser = MappingTableParser()
    parser.feed(input_path.read_text(encoding="utf-8"))
    parser.close()

    try:
        name_column = parser.headers.index("Nombre Original")
        glyph_column = parser.headers.index("Glyph Primario")
    except ValueError as error:
        raise ValueError("La tabla debe incluir las columnas Nombre Original y Glyph Primario.") from error

    mappings: list[IconMapping] = []
    seen_names: set[str] = set()
    for row_number, row in enumerate(parser.rows, start=1):
        if len(row) <= max(name_column, glyph_column):
            raise ValueError(f"Fila {row_number}: faltan columnas requeridas.")
        original_name = row[name_column].strip()
        glyph = row[glyph_column].strip()
        if not original_name or not glyph:
            raise ValueError(f"Fila {row_number}: Nombre Original y Glyph Primario son obligatorios.")
        if original_name in seen_names:
            raise ValueError(f"Nombre Original duplicado: {original_name!r}.")
        if "/" in original_name or "\\" in original_name or original_name in {".", ".."}:
            raise ValueError(f"Nombre Original no es un nombre de archivo válido: {original_name!r}.")
        seen_names.add(original_name)
        mappings.append(IconMapping(original_name, glyph))
    return mappings


def validate_svg(payload: bytes, source: str) -> None:
    if not payload.lstrip().startswith(b"<"):
        raise ValueError(f"{source}: la respuesta no parece XML/SVG.")
    try:
        root = ET.fromstring(payload)
    except ET.ParseError as error:
        raise ValueError(f"{source}: SVG/XML inválido ({error}).") from error
    if root.tag.rsplit("}", 1)[-1].lower() != "svg":
        raise ValueError(f"{source}: la respuesta no tiene un elemento raíz <svg>.")


def download_svg(mapping: IconMapping) -> bytes:
    request = urllib.request.Request(
        mapping.official_url,
        headers={"User-Agent": "mockups-material-symbol-exporter/1.0", "Accept": "image/svg+xml"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = response.read()
    except urllib.error.HTTPError as error:
        if error.code == 404:
            raise FileNotFoundError(mapping.official_url) from error
        raise RuntimeError(f"HTTP {error.code}: {mapping.official_url}") from error
    except urllib.error.URLError as error:
        raise RuntimeError(f"No se pudo descargar {mapping.official_url}: {error.reason}") from error
    validate_svg(payload, mapping.official_url)
    return payload


def write_bytes(path: Path, payload: bytes) -> None:
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as temporary:
        temporary.write(payload)
        temporary_path = Path(temporary.name)
    temporary_path.replace(path)


def write_outputs(output_dir: Path, mappings: list[IconMapping], results: dict[str, str], errors: dict[str, str]) -> None:
    manifest = [
        {
            "nombre original": mapping.original_name,
            "glyph": mapping.glyph,
            "nombre de archivo": mapping.filename,
            "ruta oficial de origen": mapping.official_url,
        }
        for mapping in mappings
    ]
    (output_dir / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )

    grouped = {state: [mapping for mapping in mappings if results.get(mapping.original_name) == state] for state in ("created", "skipped")}
    missing = [mapping for mapping in mappings if errors.get(mapping.original_name, "").startswith("not found")]
    other_errors = [mapping for mapping in mappings if mapping.original_name in errors and mapping not in missing]

    def bullet(items: list[IconMapping], formatter) -> list[str]:
        return [f"- {formatter(item)}" for item in items] or ["- Ninguno"]

    report = [
        "# Material Symbols export report",
        "",
        f"- Esperados: {len(mappings)}",
        f"- Creados: {len(grouped['created'])}",
        f"- Omitidos: {len(grouped['skipped'])}",
        f"- Fallidos: {len(errors)}",
        "",
        "## Iconos creados",
        *bullet(grouped["created"], lambda item: f"`{item.filename}` ← `{item.glyph}`"),
        "",
        "## Iconos omitidos",
        *bullet(grouped["skipped"], lambda item: f"`{item.filename}` ya existía"),
        "",
        "## Glyphs no encontrados",
        *bullet(missing, lambda item: f"`{item.glyph}` ({item.filename}) — {errors[item.original_name]}"),
        "",
        "## Errores",
        *bullet(other_errors, lambda item: f"`{item.glyph}` ({item.filename}) — {errors[item.original_name]}"),
        "",
    ]
    (output_dir / "export-report.md").write_text("\n".join(report), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, default=Path("tools/icon-export/icon-mapping.html"))
    parser.add_argument("--output", type=Path, default=Path("assets/icons/components"))
    parser.add_argument("--overwrite", action="store_true", help="Reemplaza SVG existentes.")
    args = parser.parse_args()

    if not args.input.is_file():
        parser.error(f"No existe el archivo de entrada: {args.input}")
    try:
        mappings = parse_mappings(args.input)
    except (OSError, UnicodeError, ValueError) as error:
        parser.error(str(error))
    if not mappings:
        parser.error("No se encontraron filas de iconos en la tabla.")

    args.output.mkdir(parents=True, exist_ok=True)
    results: dict[str, str] = {}
    errors: dict[str, str] = {}
    for mapping in mappings:
        target = args.output / mapping.filename
        if target.exists() and not args.overwrite:
            results[mapping.original_name] = "skipped"
            continue
        try:
            write_bytes(target, download_svg(mapping))
            results[mapping.original_name] = "created"
        except FileNotFoundError:
            errors[mapping.original_name] = f"not found: {mapping.official_url}"
        except (OSError, RuntimeError, ValueError) as error:
            errors[mapping.original_name] = str(error)

    write_outputs(args.output, mappings, results, errors)
    print(f"Esperados: {len(mappings)} | Creados: {len([s for s in results.values() if s == 'created'])} | Omitidos: {len([s for s in results.values() if s == 'skipped'])} | Fallidos: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
