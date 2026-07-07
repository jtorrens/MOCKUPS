import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalNumber,
  optionalString,
  parseObject,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveButtonIconComponentFromRecords } from "./buttonIconComponentResolver.js";
import type {
  IconRowDesignContract,
  IconRowHighlightContract,
} from "./iconRowComponentContract.js";

export function resolveIconRowComponent(
  payload: DesignPreviewPayload,
): IconRowDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveIconRowComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.iconRow",
  );
}

export function resolveIconRowComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): IconRowDesignContract {
  const iconRow = asRecord(config.iconRow);
  const buttonSlot = asRecord(iconRow.buttonIconSlot);
  const orientation = requiredString(inputs, "orientation", "component.iconRow.input.orientation");
  if (orientation !== "horizontal" && orientation !== "vertical") {
    throw new Error(`Unsupported icon row orientation ${orientation}`);
  }

  const baseButtonIconConfig = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "buttonIcon",
      requiredString(inputs, "buttonIconPresetId", "component.iconRow.input.buttonIconPresetId"),
    ),
    asRecord(buttonSlot.overrides),
  );
  const size = requiredNumber(inputs, "size", "component.iconRow.input.size");
  const icons = requiredStringArray(inputs, "icons", "component.iconRow.input.icons");
  const highlight = optionalHighlight(inputs);
  const buttons = icons.map((iconToken, index) => {
    const highlightOverrides = highlight && highlight.index === index
      ? iconButtonHighlightOverrides(highlight)
      : {};
    return resolveButtonIconComponentFromRecords(
      mergeComponentDefaults(baseButtonIconConfig, {
        buttonIcon: {
          size,
          iconToken,
          labelSlot: {
            showLabel: false,
            showSubtext: false,
          },
          ...highlightOverrides,
        },
      }),
      { iconToken },
      componentBaseConfigs,
      `${id}.slot${index}`,
    );
  });

  return {
    id,
    orientation,
    gapToken: requiredString(inputs, "gap", "component.iconRow.input.gap"),
    size,
    icons,
    highlight,
    buttons,
  };
}

function iconButtonHighlightOverrides(
  highlight: IconRowHighlightContract,
): Record<string, unknown> {
  const buttonIcon: Record<string, unknown> = {};
  if (highlight.backgroundPaletteColor) {
    buttonIcon.backgroundPaletteColor = highlight.backgroundPaletteColor;
  }
  if (highlight.iconPaletteColor) {
    buttonIcon.iconPaletteColor = highlight.iconPaletteColor;
  }
  if (highlight.backgroundAlpha !== undefined) {
    buttonIcon.surfaceSlot = {
      overrides: {
        surface: {
          backgroundAlpha: highlight.backgroundAlpha,
        },
      },
    };
  }
  return buttonIcon;
}

function requiredStringArray(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (Array.isArray(raw) && raw.every((entry) => typeof entry === "string")) {
    return raw.filter((entry) => entry.trim().length > 0);
  }

  throw new Error(`Missing string array value ${path}`);
}

function optionalHighlight(inputs: Record<string, unknown>): IconRowHighlightContract | undefined {
  const actionIconNumber = optionalNumber(inputs, "actionIconNumber", 0);
  if (actionIconNumber > 0) {
    return {
      index: Math.floor(actionIconNumber) - 1,
      backgroundAlpha: Math.max(
        0,
        Math.min(1, optionalNumber(inputs, "actionBackgroundAlpha", 1)),
      ),
      backgroundPaletteColor: optionalString(inputs, "actionBackgroundColor") || undefined,
      iconPaletteColor: optionalString(inputs, "actionIconColor") || undefined,
    };
  }

  const value = inputs.highlight;
  const highlight = asRecord(value);
  if (!Object.keys(highlight).length) return undefined;
  const index = typeof highlight.index === "number" && Number.isFinite(highlight.index)
    ? Math.max(0, Math.floor(highlight.index))
    : undefined;
  if (index === undefined) return undefined;
  return {
    index,
    backgroundAlpha: typeof highlight.backgroundAlpha === "number"
      ? Math.max(0, Math.min(1, highlight.backgroundAlpha))
      : undefined,
    backgroundPaletteColor: typeof highlight.backgroundPaletteColor === "string"
      ? highlight.backgroundPaletteColor
      : undefined,
    iconPaletteColor: typeof highlight.iconPaletteColor === "string"
      ? highlight.iconPaletteColor
      : undefined,
  };
}
