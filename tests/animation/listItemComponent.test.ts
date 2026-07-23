import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import Database from "better-sqlite3";

import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import { listItemComponentToRenderable } from "../../src/desktop-preview/listItemComponentRenderable.js";
import { resolveListItemComponent } from "../../src/desktop-preview/listItemComponentResolver.js";

type ComponentRow = {
  id: string;
  component_type: string;
  config_json: string;
  design_preview_json: string;
  metadata_json: string;
};

function fixture(variantId = "calls"): DesignPreviewPayload {
  const database = new Database(
    path.join(process.cwd(), "data", "desktop-editor-spike.sqlite"),
    { readonly: true, fileMustExist: true },
  );
  try {
    const rows = database.prepare(`
      SELECT id, component_type, config_json, design_preview_json, metadata_json
      FROM component_classes
    `).all() as ComponentRow[];
    const listItem = rows.find((row) => row.component_type === "listItem");
    assert.ok(listItem);
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
    const selectedReference = `${listItem.id}::variant::${variantId}`;
    const preview = JSON.parse(listItem.design_preview_json) as {
      contentSets: Array<Record<string, unknown>>;
    };
    for (const contentSet of preview.contentSets) {
      const actorId = String(contentSet.actorId);
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
      const palette = Object.fromEntries(
        (database.prepare(
          "SELECT token, value_hex FROM palette_colors WHERE project_id = 'project_foqn_s2'",
        ).all() as Array<{ token: string; value_hex: string }>)
          .map((color) => [color.token, color.value_hex]),
      );
      contentSet.actor = {
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
    const theme = database.prepare(
      "SELECT tokens_json, icon_theme_id FROM themes WHERE id = 'theme_project_foqn_s2_ios_default'",
    ).get() as { tokens_json: string; icon_theme_id: string };
    const iconTheme = database.prepare(
      "SELECT asset_root, mapping_json FROM icon_themes WHERE id = ?",
    ).get(theme.icon_theme_id) as { asset_root: string; mapping_json: string };
    const paletteRows = database.prepare(
      "SELECT token, value_hex, is_neutral FROM palette_colors WHERE project_id = 'project_foqn_s2'",
    ).all() as Array<{ token: string; value_hex: string; is_neutral: number }>;
    return {
      kind: "componentClass",
      componentType: "listItem",
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
      paletteColors: Object.fromEntries(
        paletteRows.map((row) => [row.token, row.value_hex]),
      ),
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

test("List Item Calls and Chats Variants share the element model boundary", () => {
  const calls = resolveListItemComponent(fixture("calls"));
  const chats = resolveListItemComponent(fixture("chats"));

  assert.deepEqual(
    calls.elements.map((element) => element.componentType),
    ["avatar", "label", "iconRow"],
  );
  assert.deepEqual(
    chats.elements.map((element) => element.componentType),
    ["avatar", "label", "iconRow"],
  );
  assert.equal(calls.elements[2]?.componentType === "iconRow"
    && calls.elements[2].component.orientation, "horizontal");
  assert.equal(chats.elements[2]?.componentType === "iconRow"
    && chats.elements[2].component.orientation, "vertical");
  assert.equal(calls.selectedSetId, "set_a");
});

test("List Item animates selected content set and the selected set state independently", () => {
  const source = fixture("calls");
  source.localFrame = 10;
  source.instanceJson = JSON.stringify({
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          id: "selected-set",
          fieldId: "selectedSetId",
          targetId: "",
          keyframes: [{ id: "selected-set-10", frame: 10, value: "set_b", interpolation: "hold" }],
        },
        {
          id: "set-state",
          fieldId: "state",
          targetId: "set_b",
          keyframes: [{ id: "set-state-10", frame: 10, value: "inactive", interpolation: "hold" }],
        },
      ],
    },
  });

  const resolved = resolveListItemComponent(source);
  assert.equal(resolved.selectedSetId, "set_b");
  assert.equal(resolved.state, "inactive");
  assert.equal(resolved.elementsOpacity, 0.45);
  assert.equal(
    resolved.elements[1]?.componentType === "label"
      && resolved.elements[1].component.text,
    "+34 848 983 160",
  );
});

test("List Item rejects content sets whose Icon Row values do not match Variant slots", () => {
  const source = fixture("calls");
  const preview = JSON.parse(source.designPreviewJson) as {
    contentSets: Array<{ iconRowValues: unknown[] }>;
  };
  preview.contentSets[0]!.iconRowValues = preview.contentSets[0]!.iconRowValues.slice(0, 1);
  source.designPreviewJson = JSON.stringify(preview);
  assert.throws(
    () => resolveListItemComponent(source),
    /must match the Variant slots exactly/,
  );
});

test("List Item renderable keeps Surface outside the element opacity group", () => {
  const source = fixture("chats");
  const contract = resolveListItemComponent(source);
  const node = listItemComponentToRenderable(source, contract);

  assert.deepEqual(node.box, {
    x: 0,
    y: 318,
    width: 360,
    height: 84,
  });
  assert.equal(node.children?.length, 2);
  assert.equal(node.children?.[0]?.id, "component.listItem.normal.surface");
  assert.equal(node.children?.[1]?.id, "component.listItem.elements");
  assert.equal(node.children?.[1]?.transform?.opacity, 1);
  assert.equal(node.children?.[1]?.children?.length, 3);
});
