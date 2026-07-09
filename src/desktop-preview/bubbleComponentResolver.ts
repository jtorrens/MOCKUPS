import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalBoolean,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredPlacement,
  requiredPossiblyEmptyString,
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
import {
  simpleWriteOnInProgress,
  simpleWriteOnText,
  type SimpleWriteOnPlan,
} from "./previewTextRevealHelpers.js";

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
  const legacySize = optionalSize(preview);
  const maxWidth = Math.max(
    1,
    optionalNumber(preview, "maxWidth", legacySize?.first ?? 260),
  );
  const padding = requiredStringPair(bubble, "padding", "component.bubble.padding");
  const state = bubbleState(requiredString(preview, "state", "component.bubble.input.state"));
  const fullText = requiredPossiblyEmptyString(
    preview,
    "sampleText",
    "component.bubble.input.sampleText",
  );
  const writeOnPlan: SimpleWriteOnPlan = {
    enabled: optionalBoolean(preview, "writeOnTrigger"),
    timeSeconds: optionalNumber(preview, "writeOnTimeSeconds", 0),
    durationSeconds: optionalNumber(preview, "writeOnDurationSeconds", 1.2),
  };
  const visibleText = simpleWriteOnText(fullText, writeOnPlan);

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

  const textBoxInputs = {
    sampleText: visibleText,
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
    size: `${maxWidth}|1`,
    maxWidth,
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
    maxWidth,
    padding: { xToken: padding.first, yToken: padding.second },
    surface: bubbleSurfaceForState(
      resolveSurfaceComponentAtSize(
        surfaceConfig,
        { width: maxWidth, height: 1 },
        "component.bubble.surface",
      ),
      state,
    ),
    textBox: {
      ...resolveTextBoxComponentFromRecords(
        textBoxConfig,
        textBoxInputs,
        componentBaseConfigs,
        "component.bubble.textBox",
      ),
      cursorVisible: simpleWriteOnInProgress(fullText, writeOnPlan),
    },
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
        background: palettePair(bubble, "incomingBackground", "component.bubble.incomingBackground"),
        text: palettePair(bubble, "incomingText", "component.bubble.incomingText"),
      },
      system: {
        background: palettePair(bubble, "systemBackground", "component.bubble.systemBackground"),
        text: palettePair(bubble, "systemText", "component.bubble.systemText"),
      },
      outgoing: {
        background: palettePair(bubble, "outgoingBackground", "component.bubble.outgoingBackground"),
        text: palettePair(bubble, "outgoingText", "component.bubble.outgoingText"),
      },
    },
  };
}

function optionalSize(value: Record<string, unknown>) {
  const raw = optionalString(value, "size");
  if (!raw) return undefined;
  const [first, second] = raw.split("|").map((part) => Number(part));
  return Number.isFinite(first) && Number.isFinite(second)
    ? { first, second }
    : undefined;
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
