import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import {
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredStringPair,
  requiredString,
  requiredRecord,
} from "./componentResolverCommon.js";
import type { ButtonAppearanceContract, ButtonContentMode, ButtonDesignContract } from "./buttonComponentContract.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { resolveBadgeComponentFromRecords } from "./badgeComponentResolver.js";

export function resolveButtonComponent(payload: DesignPreviewPayload): ButtonDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  return resolveButtonComponentFromRecords(config, preview, bases, "component.button");
}

export function resolveButtonComponentFromRecords(
  config: Record<string, unknown>,
  preview: Record<string, unknown>,
  bases: Record<string, unknown>,
  id: string,
): ButtonDesignContract {
  const button = requiredRecord(config, "button", "component.button");
  const contentMode = buttonContentMode(
    requiredString(button, "contentMode", "component.button.contentMode"),
  );
  const enabled = requiredBoolean(preview, "enabled", "component.button.input.enabled");
  const pressed = requiredBoolean(preview, "pressed", "component.button.input.pressed");
  const disabledOpacity = unitInterval(
    requiredNumber(button, "disabledOpacity", "component.button.disabledOpacity"),
    "component.button.disabledOpacity",
  );
  const pressedScale = positive(
    requiredNumber(button, "pressedScale", "component.button.pressedScale"),
    "component.button.pressedScale",
  );
  const dimensionMode = requiredString(button, "dimensionMode", "component.button.dimensionMode");
  if (dimensionMode !== "content" && dimensionMode !== "fixed") {
    throw new Error(`Unsupported button dimension mode ${dimensionMode}`);
  }
  const rawSize = requiredNumberPair(button, "size", "component.button.size");
  const size = { width: rawSize.first, height: rawSize.second };
  const rawPadding = requiredStringPair(button, "padding", "component.button.padding");
  const text = typeof preview.sampleText === "string" ? preview.sampleText : "";
  const badgeSlot = requiredRecord(button, "badgeSlot", "component.button.badgeSlot");
  const showBadge = requiredBoolean(preview, "showBadge", "component.button.input.showBadge");

  return {
    id,
    contentMode,
    enabled,
    pressed,
    opacity: enabled ? 1 : disabledOpacity,
    scale: pressed ? pressedScale : 1,
    dimensionMode,
    size,
    padding: { xToken: rawPadding.first, yToken: rawPadding.second },
    contentGapToken: requiredString(button, "contentGapToken", "component.button.contentGapToken"),
    iconToken: typeof preview.iconToken === "string" && preview.iconToken.trim()
      ? preview.iconToken
      : requiredString(button, "iconToken", "component.button.iconToken"),
    iconSizeToken: requiredString(preview, "iconSizeToken", "component.button.input.iconSizeToken"),
    appearance: resolveButtonAppearance(button, contentMode, text, preview, bases, size),
    badge: showBadge ? resolveBadgeComponentFromRecords(
      embeddedComponentConfig(bases, badgeSlot, "badge", "component.button.badgeSlot"),
      {
        contentMode: requiredString(preview, "badgeContentMode", "component.button.input.badgeContentMode"),
        iconToken: requiredString(preview, "badgeIconToken", "component.button.input.badgeIconToken"),
        text: requiredString(preview, "badgeText", "component.button.input.badgeText"),
        size: requiredNumber(preview, "badgeSize", "component.button.input.badgeSize"),
        backgroundPaletteColor: requiredString(preview, "badgeBackgroundPaletteColor", "component.button.input.badgeBackgroundPaletteColor"),
        contentPaletteColor: requiredString(preview, "badgeContentPaletteColor", "component.button.input.badgeContentPaletteColor"),
      },
      `${id}.badge`,
    ) : undefined,
  };
}

function resolveButtonAppearance(
  button: Record<string, unknown>,
  contentMode: ButtonContentMode,
  text: string,
  preview: Record<string, unknown>,
  bases: Record<string, unknown>,
  size: { width: number; height: number },
): ButtonAppearanceContract {
  const appearance = requiredRecord(button, "appearance", "component.button.appearance");
  const surfaceSlot = requiredRecord(appearance, "surfaceSlot", "component.button.appearance.surfaceSlot");
  const labelSlot = requiredRecord(appearance, "labelSlot", "component.button.appearance.labelSlot");
  return {
    iconColorToken: typeof preview.iconColorToken === "string" && preview.iconColorToken.trim()
      ? preview.iconColorToken
      : requiredString(appearance, "iconColorToken", "component.button.appearance.iconColorToken"),
    label: contentMode === "icon" || !text.trim() ? undefined : resolveLabelComponentFromRecords(
      embeddedComponentConfig(bases, labelSlot, "label", "component.button.appearance.labelSlot"),
      {
        ...literalLabelPreview(text),
        textSizeToken: requiredString(preview, "textSizeToken", "component.button.input.textSizeToken"),
        ...(typeof preview.textColorToken === "string" && preview.textColorToken.trim()
          ? { textColorToken: preview.textColorToken }
          : {}),
      },
      bases,
      "component.button.appearance.label",
      staticLabelFrameContext,
    ),
    surface: resolveSurfaceComponentAtSize(
      embeddedComponentConfig(bases, surfaceSlot, "surface", "component.button.appearance.surfaceSlot"),
      size,
      "component.button.appearance.surface",
    ),
  };
}

function buttonContentMode(value: string): ButtonContentMode {
  if (value === "icon" || value === "text" || value === "iconText") return value;
  throw new Error(`Unsupported button content mode ${value}`);
}

function unitInterval(value: number, owner: string) {
  if (value >= 0 && value <= 1) return value;
  throw new Error(`${owner} must be between 0 and 1`);
}

function positive(value: number, owner: string) {
  if (value > 0) return value;
  throw new Error(`${owner} must be positive`);
}
