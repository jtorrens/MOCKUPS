import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredNumber,
  requiredPlacement,
  requiredString,
  requiredStringPair,
  requiredTypographyStyle,
} from "./componentResolverCommon.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

function requiredText(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string") return raw;
  throw new Error(`Missing text component value ${path}`);
}

function requiredPair(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = requiredString(value, key, path);
  const parts = raw.split("|");
  const first = Number(parts[0]?.replace(",", "."));
  const second = Number(parts[1]?.replace(",", "."));
  if (Number.isFinite(first) && Number.isFinite(second)) {
    return { first, second };
  }
  throw new Error(`Missing numeric pair component value ${path}`);
}

export function resolveLabelComponent(
  payload: DesignPreviewPayload,
): LabelDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveLabelComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.label",
  );
}

export function resolveLabelComponentFromRecords(
  config: Record<string, unknown>,
  preview: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): LabelDesignContract {
  const label = asRecord(config.label);
  const surfaceSlot = asRecord(label.surfaceSlot);
  const embeddedSurfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", surfaceSlot.presetId),
    asRecord(surfaceSlot.overrides),
  );
  const size = requiredPair(label, "size", "component.label.size");
  const padding = requiredStringPair(label, "padding", "component.label.padding");
  const dimensionMode = requiredString(
    label,
    "dimensionMode",
    "component.label.dimensionMode",
  );
  if (dimensionMode !== "content" && dimensionMode !== "fixed") {
    throw new Error(`Unsupported label dimension mode ${dimensionMode}`);
  }

  const textAlign = requiredString(label, "textAlign", "component.label.textAlign");
  if (textAlign !== "left" && textAlign !== "center" && textAlign !== "right") {
    throw new Error(`Unsupported label text align ${textAlign}`);
  }

  return {
    id,
    text: requiredText(preview, "sampleText", "component.label.preview.sampleText"),
    subtext: requiredText(
      preview,
      "sampleSubtext",
      "component.label.preview.sampleSubtext",
    ),
    dimensionMode,
    size: { width: size.first, height: size.second },
    padding: { xToken: padding.first, yToken: padding.second },
    textColorToken: requiredString(
      label,
      "textColorToken",
      "component.label.textColorToken",
    ),
    textTypography: {
      ...requiredTypographyStyle(label, "textTypography", "component.label.textTypography"),
      ...(typeof preview.textSizeToken === "string" && preview.textSizeToken.trim()
        ? { sizeToken: preview.textSizeToken }
        : {}),
    },
    textAlign,
    textGapToken: requiredString(label, "textGapToken", "component.label.textGapToken"),
    subtextPlacement: requiredPlacement(
      label,
      "subtextPlacement",
      "component.label.subtextPlacement",
    ),
    subtextColorToken: requiredString(
      label,
      "subtextColorToken",
      "component.label.subtextColorToken",
    ),
    subtextTypography: requiredTypographyStyle(
      label,
      "subtextTypography",
      "component.label.subtextTypography",
    ),
    surface: resolveSurfaceComponentAtSize(
      embeddedSurfaceConfig,
      { width: size.first, height: size.second },
      `${id}.surface`,
    ),
  };
}
