import assert from "node:assert/strict";
import test from "node:test";
import path from "node:path";
import Database from "better-sqlite3";

import { resolveTextBoxComponentFromRecords } from "../../src/desktop-preview/textBoxComponentResolver.js";
import { resolveTextInputBarComponent } from "../../src/desktop-preview/textInputBarComponentResolver.js";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";

const surfaceVariantReference = "component_surface::variant::default";
const cursorVariantReference = "component_cursor::variant::default";
const iconRowVariantReference = "component_icon_row::variant::default";

const bases = {
  variants: {
    [surfaceVariantReference]: {
      style: {
        shadowEnabled: false,
        reliefEnabled: false,
        borderWidth: 0,
        borderColorToken: "theme.borders.primary",
        cornerRadiusToken: "theme.radii.none",
        reliefAngle: -45,
        reliefExtent: 1,
        reliefSpread: 0,
        reliefTopIntensity: 0.12,
        reliefBottomIntensity: -0.1,
      },
      surface: {
        backgroundColorToken: "theme.colors.surface",
        backgroundAlpha: 1,
        borderAlpha: 1,
        tail: {
          enabled: false,
          style: "rounded_wedge",
          side: "left",
          vertical: "bottom",
          size: "18|14",
          outerCornerRadius: 0,
        },
      },
    },
    [cursorVariantReference]: {
      cursor: {
        colorToken: "theme.cursor.color",
        width: 2,
        minimumFade: 0.15,
        fadeDurationMs: 480,
      },
    },
    [iconRowVariantReference]: {
      iconRow: {
        orientation: "horizontal",
        gap: "theme.spacing.s",
        items: [],
        sizeSource: "shared",
        iconSizeToken: "theme.iconSizes.m",
        textSizeToken: "theme.typography.sizes.s",
      },
    },
  },
};

const config = {
  textBox: {
    dimensionMode: "fixed",
    padding: "theme.spacing.s|theme.spacing.s",
    surfaceSlot: {
      variantReference: surfaceVariantReference,
      overrides: {},
    },
    cursorSlot: {
      showCursor: true,
      variantReference: cursorVariantReference,
      overrides: {},
    },
    textColorToken: "theme.colors.textPrimary",
    placeholderColorToken: "theme.colors.textSecondary",
    typography: {
      fontFamilyId: "theme",
      weight: "theme.typography.weight",
      style: "theme.typography.style",
      sizeToken: "theme.typography.sizes.s",
      lineHeight: "theme.typography.lineHeights.normal",
    },
    textAlign: "left",
    overflowMode: "clip",
    placeholder: "Message",
    maxLines: 4,
    iconGap: "theme.spacing.m",
    leftIconRowSlot: {
      variantReference: iconRowVariantReference,
      overrides: {
        iconRow: {
          gap: "theme.spacing.s",
          orientation: "horizontal",
          items: [],
        },
      },
    },
    rightIconRowSlot: {
      variantReference: iconRowVariantReference,
      overrides: {
        iconRow: {
          gap: "theme.spacing.m",
          orientation: "vertical",
          items: [],
        },
      },
    },
  },
};

function inputs() {
  return {
    sampleText: "Message",
    size: "220|44",
    maxWidth: 220,
  };
}

test("Text Box resolves both Icon Row boundaries from its Variant slots", () => {
  const resolved = resolveTextBoxComponentFromRecords(
    config,
    inputs(),
    bases,
    "component.textBox",
  );

  assert.equal(resolved.leftIconRow.orientation, "horizontal");
  assert.equal(resolved.leftIconRow.gapToken, "theme.spacing.s");
  assert.deepEqual(resolved.leftIconRow.items, []);
  assert.equal(resolved.rightIconRow.orientation, "vertical");
  assert.equal(resolved.rightIconRow.gapToken, "theme.spacing.m");
  assert.deepEqual(resolved.rightIconRow.items, []);
});

test("Text Box requires complete Icon Row slots even when their item lists are empty", () => {
  for (const invalidSlot of [undefined, null, [], {}, {
    variantReference: iconRowVariantReference,
  }, {
    variantReference: "default",
    overrides: {},
  }, {
    variantReference: iconRowVariantReference,
    overrides: null,
  }, {
    variantReference: iconRowVariantReference,
    overrides: {},
    componentType: "iconRow",
  }]) {
    const invalidConfig = structuredClone(config);
    invalidConfig.textBox.leftIconRowSlot = invalidSlot as never;
    assert.throws(() => resolveTextBoxComponentFromRecords(
      invalidConfig,
      inputs(),
      bases,
      "component.textBox",
    ));
  }
});

test("Text Box rejects Variant-owned values at its Runtime boundary", () => {
  for (const retired of [
    "placeholder",
    "maxLines",
    "leftIconRowSlot",
    "leftIconRowItems",
    "leftIconRowGap",
    "leftIconRowOrientation",
    "rightIconRowSlot",
    "rightIconRowItems",
    "rightIconRowGap",
    "rightIconRowOrientation",
    "iconGap",
    "leftIcons",
    "rightIcons",
    "leftIconRowInputs",
    "rightIconRowInputs",
    "iconRowSize",
    "iconRowGap",
    "iconRowOrientation",
  ]) {
    assert.throws(() => resolveTextBoxComponentFromRecords(
      config,
      { ...inputs(), [retired]: [] },
      bases,
      "component.textBox",
    ));
  }
});

test("the committed Text Input Bar resolves its migrated structured Button item", () => {
  const database = new Database(
    path.join(process.cwd(), "data", "desktop-editor-spike.sqlite"),
    { readonly: true, fileMustExist: true },
  );
  try {
    const rows = database.prepare(
      "SELECT id, component_type, config_json, design_preview_json, metadata_json FROM component_classes",
    ).all() as {
      id: string;
      component_type: string;
      config_json: string;
      design_preview_json: string;
      metadata_json: string;
    }[];
    const textInputBar = rows.find((row) => row.component_type === "textInputBar");
    assert.ok(textInputBar);
    const variants: Record<string, unknown> = {};
    const variantTypes: Record<string, string> = {};
    for (const row of rows) {
      const metadata = JSON.parse(row.metadata_json) as {
        variants: { id: string; config: Record<string, unknown> }[];
      };
      for (const variant of metadata.variants) {
        const reference = `${row.id}::variant::${variant.id}`;
        variants[reference] = variant.config;
        variantTypes[reference] = row.component_type;
      }
    }
    const payload: DesignPreviewPayload = {
      kind: "componentClass",
      componentType: "textInputBar",
      componentBaseConfigsJson: JSON.stringify({ variants, variantTypes }),
      appConfigJson: "{}",
      instanceJson: "{}",
      frameRate: 25,
      localFrame: 0,
      configJson: textInputBar.config_json,
      designPreviewJson: textInputBar.design_preview_json,
      runtimeContractJson: textInputBar.design_preview_json,
      previewFrame: {
        canvasWidth: 360,
        canvasHeight: 720,
        screenX: 0,
        screenY: 0,
        screenWidth: 360,
        screenHeight: 720,
        scaleToPixels: 1,
      },
      themeMode: "light",
      themeTokensJson: "{}",
    };

    const resolved = resolveTextInputBarComponent(payload);
    assert.equal(resolved.textBox.rightIconRow.items.length, 1);
    assert.equal(
      resolved.textBox.rightIconRow.items[0]?.button.iconToken,
      "chat_attach",
    );
    assert.equal(
      resolved.textBox.rightIconRow.items[0]?.id,
      "button_attachment",
    );
  } finally {
    database.close();
  }
});
