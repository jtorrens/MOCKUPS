import type { RenderableBox } from "../visual/renderable/types.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumberPair,
  requiredPlacement,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import type {
  BubbleDesignContract,
  BubblePalettePairContract,
  BubbleState,
} from "./bubbleComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { resolveTextBoxComponentFromRecords } from "./textBoxComponentResolver.js";

export function resolveBubbleComponent(
  payload: DesignPreviewPayload,
): BubbleDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const bubble = asRecord(config.bubble);
  const surfaceSlot = asRecord(bubble.surfaceSlot);
  const textBoxSlot = asRecord(bubble.textBoxSlot);
  const actorLabelSlot = asRecord(bubble.actorLabelSlot);
  const size = requiredNumberPair(preview, "size", "component.bubble.input.size");
  const state = bubbleState(requiredString(preview, "state", "component.bubble.input.state"));

  const surfaceConfig = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "surface",
      requiredString(surfaceSlot, "presetId", "component.bubble.surfaceSlot.presetId"),
    ),
    asRecord(surfaceSlot.overrides),
  );
  const textBoxConfig = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "textBox",
      requiredString(textBoxSlot, "presetId", "component.bubble.textBoxSlot.presetId"),
    ),
    asRecord(textBoxSlot.overrides),
  );

  const box: RenderableBox = {
    x: 0,
    y: 0,
    width: Math.max(1, size.first),
    height: Math.max(1, size.second),
  };
  const textBoxInputs = {
    sampleText: optionalString(preview, "sampleText"),
    placeholder: "",
    maxLines: 12,
    leftIconRowSlot: {},
    leftIcons: [],
    rightIconRowSlot: {},
    rightIcons: [],
    buttonIconSlot: {},
    iconGap: "theme.spacing.none",
    iconRowSize: "theme.iconSizes.s",
    iconRowGap: "theme.spacing.none",
    iconRowOrientation: "horizontal",
    size: `${box.width}|${box.height}`,
    maxWidth: box.width,
  };
  const actorLabelVisible = requiredBoolean(
    actorLabelSlot,
    "showLabel",
    "component.bubble.actorLabel.showLabel",
  );
  const actorLabelConfig = actorLabelVisible
    ? mergeComponentDefaults(
        componentPresetConfig(
          componentBaseConfigs,
          "label",
          requiredString(
            actorLabelSlot,
            "presetId",
            "component.bubble.actorLabel.presetId",
          ),
        ),
        asRecord(actorLabelSlot.overrides),
      )
    : undefined;

  return {
    id: "component.bubble",
    state,
    renderBox: box,
    surface: bubbleSurfaceForState(
      resolveSurfaceComponentAtSize(surfaceConfig, box, "component.bubble.surface"),
      state,
    ),
    textBox: resolveTextBoxComponentFromRecords(
      textBoxConfig,
      textBoxInputs,
      componentBaseConfigs,
      "component.bubble.textBox",
    ),
    actorLabelSlot: {
      showLabel: actorLabelVisible,
      placement: requiredPlacement(
        actorLabelSlot,
        "placement",
        "component.bubble.actorLabel.placement",
      ),
      label: actorLabelConfig
        ? resolveLabelComponentFromRecords(
            actorLabelConfig,
            {
              sampleText: optionalString(preview, "actorName") || "Actor",
              sampleSubtext: "",
            },
            componentBaseConfigs,
            "component.bubble.actorLabel",
          )
        : undefined,
    },
    colors: {
      incoming: {
        background: palettePair(preview, "incomingBackground", "component.bubble.input.incomingBackground"),
        text: palettePair(preview, "incomingText", "component.bubble.input.incomingText"),
      },
      system: {
        background: palettePair(preview, "systemBackground", "component.bubble.input.systemBackground"),
        text: palettePair(preview, "systemText", "component.bubble.input.systemText"),
      },
      outgoing: {
        background: palettePair(preview, "outgoingBackground", "component.bubble.input.outgoingBackground"),
        text: palettePair(preview, "outgoingText", "component.bubble.input.outgoingText"),
      },
    },
  };
}

function bubbleState(value: string): BubbleState {
  if (value === "incoming" || value === "system" || value === "outgoing") {
    return value;
  }
  throw new Error(`Unsupported bubble state ${value}`);
}

function bubbleSurfaceForState(
  surface: BubbleDesignContract["surface"],
  state: BubbleState,
): SurfaceDesignContract {
  const side: SurfaceDesignContract["tail"]["side"] =
    state === "outgoing" ? "right" : "left";
  return {
    ...surface,
    tail: {
      ...surface.tail,
      enabled: state !== "system",
      side,
    },
  };
}

function palettePair(
  value: Record<string, unknown>,
  key: string,
  path: string,
): BubblePalettePairContract {
  const pair = requiredStringPair(value, key, path);
  return {
    light: pair.first,
    dark: pair.second,
  };
}
