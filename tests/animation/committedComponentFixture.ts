import assert from "node:assert/strict";
import path from "node:path";

import Database from "better-sqlite3";

import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";

type ComponentRow = {
  id: string;
  component_type: string;
  design_preview_json: string;
  metadata_json: string;
};

export function committedComponentFixture(
  componentType: string,
  variantId = "default",
): DesignPreviewPayload {
  const database = new Database(
    path.join(process.cwd(), "data", "desktop-editor-spike.sqlite"),
    { readonly: true, fileMustExist: true },
  );
  try {
    const rows = database.prepare(`
      SELECT id, component_type, design_preview_json, metadata_json
      FROM component_classes
    `).all() as ComponentRow[];
    const component = rows.find((row) => row.component_type === componentType);
    assert.ok(component);
    const variants: Record<string, unknown> = {};
    const variantTypes: Record<string, string> = {};
    for (const row of rows) {
      const metadata = JSON.parse(row.metadata_json) as {
        variants: Array<{ id: string; config: Record<string, unknown> }>;
      };
      for (const variant of metadata.variants) {
        const reference = `${row.id}::variant::${variant.id}`;
        variants[reference] = variant.config;
        variantTypes[reference] = row.component_type;
      }
    }
    const selectedReference = `${component.id}::variant::${variantId}`;
    const preview = JSON.parse(component.design_preview_json) as Record<string, unknown>;
    const paletteRows = database.prepare(
      "SELECT token, value_hex, is_neutral FROM palette_colors WHERE project_id = 'project_foqn_s2'",
    ).all() as Array<{ token: string; value_hex: string; is_neutral: number }>;
    const palette = Object.fromEntries(
      paletteRows.map((row) => [row.token, row.value_hex]),
    );
    resolveActors(preview, database, palette);
    const theme = database.prepare(
      "SELECT tokens_json, icon_theme_id FROM themes WHERE id = 'theme_project_foqn_s2_ios_default'",
    ).get() as { tokens_json: string; icon_theme_id: string };
    const iconTheme = database.prepare(
      "SELECT asset_root, mapping_json FROM icon_themes WHERE id = ?",
    ).get(theme.icon_theme_id) as { asset_root: string; mapping_json: string };
    return {
      kind: "componentClass",
      componentType,
      componentBaseConfigsJson: JSON.stringify({ variants, variantTypes }),
      appConfigJson: "{}",
      instanceJson: "{}",
      frameRate: 25,
      localFrame: 0,
      configJson: JSON.stringify(variants[selectedReference]),
      designPreviewJson: JSON.stringify(preview),
      runtimeContractJson: JSON.stringify(preview),
      previewFrame: {
        canvasWidth: 360,
        canvasHeight: 720,
        screenX: 0,
        screenY: 0,
        screenWidth: 360,
        screenHeight: 720,
        scaleToPixels: 1,
      },
      iconAssetRoot: iconTheme.asset_root,
      iconMappingJson: iconTheme.mapping_json,
      fontFaces: [
        {
          fontId: "font_2f3748a776df454c8a7a0a3e1fe6a3bb",
          family: "SF Pro Text",
          category: "text",
          relativePath: "assets/FOQN_S2/fonts/sf/SF-Pro-Text-Regular.otf",
          weight: 400,
          style: "normal",
        },
        {
          fontId: "font_6c1d6c39206c41658a64ba1381fcaa1f",
          family: "Noto Color Emoji",
          category: "emoji",
          relativePath: "assets/FOQN_S2/fonts/notocoloremoji/NotoColorEmoji-Regular.ttf",
          weight: 400,
          style: "normal",
        },
      ],
      paletteColors: palette,
      paletteNeutralColors: Object.fromEntries(
        paletteRows.map((row) => [row.token, row.is_neutral === 1]),
      ),
      themeMode: "light",
      themeTokensJson: theme.tokens_json,
    };
  } finally {
    database.close();
  }
}

function resolveActors(
  value: unknown,
  database: Database.Database,
  palette: Record<string, string>,
) {
  if (Array.isArray(value)) {
    for (const item of value) resolveActors(item, database, palette);
    return;
  }
  if (!value || typeof value !== "object") return;
  const record = value as Record<string, unknown>;
  if (typeof record.actorId === "string") {
    record.actor = resolvedActor(record.actorId, database, palette);
  }
  for (const child of Object.values(record)) {
    resolveActors(child, database, palette);
  }
}

function resolvedActor(
  actorId: string,
  database: Database.Database,
  palette: Record<string, string>,
) {
  const actor = database.prepare(
    "SELECT id, display_name, short_name, metadata_json FROM actors WHERE id = ?",
  ).get(actorId) as {
    id: string;
    display_name: string;
    short_name: string;
    metadata_json: string;
  };
  const metadata = JSON.parse(actor.metadata_json) as {
    modes: { light: { color: string; avatarTextColor: string } };
    avatar: {
      filePath: string;
      scale: number;
      offsetX: number;
      offsetY: number;
      baseSize: number;
    };
  };
  return {
    id: actor.id,
    displayName: actor.display_name,
    shortName: actor.short_name,
    initials: actor.display_name.split(/\s+/).map((part) => part[0]).join("").slice(0, 2),
    avatar: {
      imageUri: metadata.avatar.filePath,
      backgroundColor: palette[metadata.modes.light.color],
      textColor: palette[metadata.modes.light.avatarTextColor],
      scale: metadata.avatar.scale,
      offsetX: metadata.avatar.offsetX,
      offsetY: metadata.avatar.offsetY,
      baseSize: metadata.avatar.baseSize,
    },
  };
}
