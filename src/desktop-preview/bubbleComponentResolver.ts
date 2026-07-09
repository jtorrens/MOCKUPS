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
  BubbleMediaType,
  BubblePalettePairContract,
  BubbleState,
} from "./bubbleComponentContract.js";
import { resolveAudioComponentFromRecords } from "./audioComponentResolver.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import { resolveMediaComponentFromRecords } from "./mediaComponentResolver.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { resolveTextBoxComponentFromRecords } from "./textBoxComponentResolver.js";
import {
  simpleWriteOnFrameInProgress,
  simpleWriteOnFrameText,
  type SimpleWriteOnFramePlan,
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
  const imageMediaSlot = asRecord(bubble.imageMediaSlot);
  const videoMediaSlot = asRecord(bubble.videoMediaSlot);
  const audioSlot = asRecord(bubble.audioSlot);
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
  const writeOnPlan: SimpleWriteOnFramePlan = {
    enabled: optionalBoolean(preview, "writeOnTrigger"),
    frame: optionalNumber(preview, "writeOnFrame", 0),
    durationFrames: optionalNumber(preview, "writeOnDurationFrames", 30),
  };
  const visibleText = simpleWriteOnFrameText(fullText, writeOnPlan);

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
  const mediaType = bubbleMediaType(
    requiredString(bubble, "mediaType", "component.bubble.mediaType"),
  );
  const imageMediaConfig = mediaType === "image"
    ? mergeComponentDefaults(
        componentPresetConfig(
          componentBaseConfigs,
          "media",
          requiredString(
            imageMediaSlot,
            "presetId",
            "component.bubble.imageMediaSlot.presetId",
          ),
        ),
        asRecord(imageMediaSlot.overrides),
      )
    : undefined;
  const videoMediaConfig = mediaType === "video"
    ? mergeComponentDefaults(
        componentPresetConfig(
          componentBaseConfigs,
          "media",
          requiredString(
            videoMediaSlot,
            "presetId",
            "component.bubble.videoMediaSlot.presetId",
          ),
        ),
        asRecord(videoMediaSlot.overrides),
      )
    : undefined;
  const audioConfig = mediaType === "audio"
    ? mergeComponentDefaults(
        componentPresetConfig(
          componentBaseConfigs,
          "audio",
          requiredString(audioSlot, "presetId", "component.bubble.audioSlot.presetId"),
        ),
        asRecord(audioSlot.overrides),
      )
    : undefined;

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
      cursorVisible: simpleWriteOnFrameInProgress(fullText, writeOnPlan),
    },
    mediaSlot: {
      mediaType,
      media: imageMediaConfig || videoMediaConfig
        ? resolveMediaComponentFromRecords(
            imageMediaConfig || videoMediaConfig!,
            bubbleMediaInputs(
              preview,
              mediaType === "video" ? "video" : "image",
              maxWidth,
            ),
            componentBaseConfigs,
            `component.bubble.${mediaType}`,
          )
        : undefined,
      audio: audioConfig
        ? resolveAudioComponentFromRecords(
            audioConfig,
            bubbleAudioInputs(preview),
            componentBaseConfigs,
            "component.bubble.audio",
          )
        : undefined,
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

function bubbleMediaType(value: string): BubbleMediaType {
  if (value === "none" || value === "image" || value === "video" || value === "audio") {
    return value;
  }
  throw new Error(`Unsupported bubble media type ${value}`);
}

function bubbleMediaInputs(
  preview: Record<string, unknown>,
  mediaType: "image" | "video",
  maxWidth: number,
) {
  const width = Math.max(1, optionalNumber(preview, "mediaWidth", Math.min(maxWidth, 240)));
  const height = Math.max(1, optionalNumber(preview, "mediaHeight", 160));
  const viewportSize = optionalString(preview, "viewportSize") || `${width}|${height}`;
  return {
    ...preview,
    mediaType,
    mediaSource: optionalString(preview, "mediaSource"),
    viewportSize,
    mediaOffset: optionalString(preview, "mediaOffset") || "0|0",
    mediaScale: optionalNumber(preview, "mediaScale", 1),
    isPlaying: optionalBoolean(preview, "isPlaying"),
    isFullScreen: optionalBoolean(preview, "isFullScreen"),
    fullScreenTransition: optionalBoolean(preview, "fullScreenTransition"),
    currentTimeSeconds: optionalNumber(preview, "currentTimeSeconds", 0),
    durationSeconds: Math.max(1, optionalNumber(preview, "durationSeconds", 12)),
    fullframeOrientation: optionalString(preview, "fullframeOrientation") || "portrait",
    controlsElapsedMs: optionalNumber(preview, "controlsElapsedMs", 0),
    motionTimeSeconds: optionalNumber(preview, "motionTimeSeconds", 0),
  };
}

function bubbleAudioInputs(preview: Record<string, unknown>) {
  return {
    ...preview,
    actor: preview.actor ?? defaultActorPreview(optionalString(preview, "actorName") || "Alex Q"),
    isPlaying: optionalBoolean(preview, "isPlaying"),
    durationSeconds: Math.max(1, optionalNumber(preview, "durationSeconds", 65)),
    currentTimeSeconds: optionalNumber(preview, "currentTimeSeconds", 0),
  };
}

function defaultActorPreview(displayName: string) {
  return {
    id: "preview_actor",
    displayName,
    shortName: displayName,
    initials: initialsForName(displayName),
    avatar: {
      imageUri: "",
      backgroundColor: "#cfd8e3",
      textColor: "#263238",
      scale: 1,
      offsetX: 0,
      offsetY: 0,
      baseSize: 256,
    },
  };
}

function initialsForName(value: string) {
  const parts = value
    .trim()
    .split(/\s+/)
    .filter((part) => part.length > 0);
  const initials = parts.slice(0, 2).map((part) => part[0]).join("");
  return initials || "A";
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
