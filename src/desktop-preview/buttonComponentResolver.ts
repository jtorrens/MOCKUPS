import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredNumberPair,
  requiredStringPair,
  requiredString,
} from "./componentResolverCommon.js";
import type { ButtonContentMode, ButtonDesignContract, ButtonState, ButtonStateDesignContract } from "./buttonComponentContract.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

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
  const button = asRecord(config.button);
  const contentMode = buttonContentMode(requiredString(preview, "contentMode", "component.button.input.contentMode"));
  const state = typeof preview.pushTrigger === "boolean" && preview.pushTrigger
    ? "pushed"
    : buttonState(typeof preview.state === "string" ? preview.state : "normal");
  const dimensionMode = requiredString(button, "dimensionMode", "component.button.dimensionMode");
  if (dimensionMode !== "content" && dimensionMode !== "fixed") {
    throw new Error(`Unsupported button dimension mode ${dimensionMode}`);
  }
  const rawSize = requiredNumberPair(button, "size", "component.button.size");
  const size = { width: rawSize.first, height: rawSize.second };
  const rawPadding = requiredStringPair(button, "padding", "component.button.padding");
  const text = typeof preview.sampleText === "string" ? preview.sampleText : "";

  return {
    id,
    contentMode,
    state,
    dimensionMode,
    size,
    padding: { xToken: rawPadding.first, yToken: rawPadding.second },
    contentGapToken: requiredString(button, "contentGapToken", "component.button.contentGapToken"),
    iconToken: typeof preview.iconToken === "string" && preview.iconToken.trim()
      ? preview.iconToken
      : requiredString(button, "iconToken", "component.button.iconToken"),
    iconSizeToken: requiredString(preview, "iconSizeToken", "component.button.input.iconSizeToken"),
    pushedDurationToken: requiredString(button, "pushedDurationToken", "component.button.pushedDurationToken"),
    stateStyle: resolveButtonStateStyle(button, state, contentMode, text, preview, bases, size),
  };
}

function resolveButtonStateStyle(
  button: Record<string, unknown>,
  state: ButtonState,
  contentMode: ButtonContentMode,
  text: string,
  preview: Record<string, unknown>,
  bases: Record<string, unknown>,
  size: { width: number; height: number },
): ButtonStateDesignContract {
  const states = asRecord(button.states);
  const style = asRecord(states[state]);
  const surfaceSlot = asRecord(style.surfaceSlot);
  const labelSlot = asRecord(style.labelSlot);
  return {
    iconColorToken: requiredString(style, "iconColorToken", `component.button.states.${state}.iconColorToken`),
    label: contentMode === "icon" ? undefined : resolveLabelComponentFromRecords(
      mergeComponentDefaults(
        componentPresetConfig(bases, "label", requiredString(labelSlot, "presetId", `component.button.states.${state}.label.presetId`)),
        asRecord(labelSlot.overrides),
      ),
      { ...literalLabelPreview(text), textSizeToken: requiredString(preview, "textSizeToken", "component.button.input.textSizeToken") },
      bases,
      `component.button.${state}.label`,
      staticLabelFrameContext,
    ),
    surface: resolveSurfaceComponentAtSize(
      mergeComponentDefaults(
        componentPresetConfig(bases, "surface", requiredString(surfaceSlot, "presetId", `component.button.states.${state}.surface.presetId`)),
        asRecord(surfaceSlot.overrides),
      ),
      size,
      `component.button.${state}.surface`,
    ),
  };
}

function buttonContentMode(value: string): ButtonContentMode {
  if (value === "icon" || value === "text" || value === "iconText") return value;
  throw new Error(`Unsupported button content mode ${value}`);
}

function buttonState(value: string): ButtonState {
  if (value === "normal" || value === "active" || value === "pushed" || value === "disabled") return value;
  throw new Error(`Unsupported button state ${value}`);
}
