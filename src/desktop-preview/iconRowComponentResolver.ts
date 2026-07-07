import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
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
  const orientation = requiredString(iconRow, "orientation", "component.iconRow.orientation");
  if (orientation !== "horizontal" && orientation !== "vertical") {
    throw new Error(`Unsupported icon row orientation ${orientation}`);
  }

  const baseButtonIconConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "buttonIcon", buttonSlot.presetId),
    asRecord(buttonSlot.overrides),
  );
  const size = requiredNumber(iconRow, "size", "component.iconRow.size");
  const icons = requiredStringArray(inputs, "icons", "component.iconRow.input.icons");
  const highlight = optionalHighlight(inputs.highlight);
  const buttons = icons.map((iconToken, index) => {
    const highlightOverrides = highlight && highlight.index === index
      ? {
          backgroundPaletteColor: highlight.backgroundPaletteColor,
          iconPaletteColor: highlight.iconPaletteColor,
        }
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
    gapToken: requiredString(iconRow, "gap", "component.iconRow.gap"),
    size,
    icons,
    highlight,
    buttons,
  };
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

function optionalHighlight(value: unknown): IconRowHighlightContract | undefined {
  const highlight = asRecord(value);
  if (!Object.keys(highlight).length) return undefined;
  const index = typeof highlight.index === "number" && Number.isFinite(highlight.index)
    ? Math.max(0, Math.floor(highlight.index))
    : undefined;
  if (index === undefined) return undefined;
  return {
    index,
    backgroundPaletteColor: typeof highlight.backgroundPaletteColor === "string"
      ? highlight.backgroundPaletteColor
      : undefined,
    iconPaletteColor: typeof highlight.iconPaletteColor === "string"
      ? highlight.iconPaletteColor
      : undefined,
  };
}
