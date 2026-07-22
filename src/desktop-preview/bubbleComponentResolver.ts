import {
  componentVariantConfig,
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
  BubbleMediaPosition,
  BubbleMediaType,
  BubblePalettePairContract,
  BubbleState,
  BubbleStatusState,
} from "./bubbleComponentContract.js";
import { resolveAudioComponentFromRecords } from "./audioComponentResolver.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import { resolveMediaComponentFromRecords } from "./mediaComponentResolver.js";
import { screenPercentToDesignWidth } from "./previewGeometryHelpers.js";
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
  const avatarSlot = asRecord(bubble.avatarSlot);
  const status = asRecord(bubble.status);
  const actorPreview = resolveBubbleActorPreview(preview);
  const maxWidthPercent = Math.min(
    100,
    Math.max(1, optionalNumber(preview, "maxWidth", 66)),
  );
  const maxWidth = screenPercentToDesignWidth(payload, maxWidthPercent);
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
  const actorIdentityVisible = typeof preview.actorIdentityVisible === "boolean"
    ? preview.actorIdentityVisible
    : true;

  const surfaceConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "surface",
      requiredString(surfaceSlot, "variantReference", "component.bubble.surfaceSlot.variantReference"),
    ),
    asRecord(surfaceSlot.overrides),
  );
  const textBoxBaseConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "textBox",
      requiredString(textBoxSlot, "variantReference", "component.bubble.textBoxSlot.variantReference"),
    ),
    asRecord(textBoxSlot.overrides),
  );
  // A bubble owns the message's size and weight choices, but not its font
  // family: message text always follows the active theme text face.
  const textBoxConfig = mergeComponentDefaults(textBoxBaseConfig, {
    textBox: {
      typography: { fontFamilyId: "theme" },
      ...(state === "system" ? { textAlign: "center" } : {}),
    },
  });
  const mediaType = bubbleMediaType(
    optionalString(preview, "mediaType")
      || requiredString(bubble, "mediaType", "component.bubble.mediaType"),
  );
  const imageMediaConfig = mediaType === "image"
    ? mergeComponentDefaults(
        componentVariantConfig(
          componentBaseConfigs,
          "media",
          requiredString(
            imageMediaSlot,
            "variantReference",
            "component.bubble.imageMediaSlot.variantReference",
          ),
        ),
        asRecord(imageMediaSlot.overrides),
      )
    : undefined;
  const videoMediaConfig = mediaType === "video"
    ? mergeComponentDefaults(
        componentVariantConfig(
          componentBaseConfigs,
          "media",
          requiredString(
            videoMediaSlot,
            "variantReference",
            "component.bubble.videoMediaSlot.variantReference",
          ),
        ),
        asRecord(videoMediaSlot.overrides),
      )
    : undefined;
  let audioConfig = mediaType === "audio"
    ? mergeComponentDefaults(
        componentVariantConfig(
          componentBaseConfigs,
          "audio",
          requiredString(audioSlot, "variantReference", "component.bubble.audioSlot.variantReference"),
        ),
        asRecord(audioSlot.overrides),
      )
    : undefined;
  if (audioConfig && state !== "incoming") {
    audioConfig = mergeComponentDefaults(audioConfig, {
      audio: {
        avatarSlot: {
          showAvatar: false,
        },
      },
    });
  }

  const textBoxInputs = {
    sampleText: visibleText,
    placeholder: "",
    maxLines: 12,
    textSizeToken: optionalString(preview, "textSizeToken"),
    textAnimationMode: optionalString(preview, "textAnimationMode"),
    textAnimationElapsedMs: optionalNumber(preview, "textAnimationElapsedMs", 0),
    leftIconRowSlot: {},
    leftIcons: [],
    rightIconRowSlot: {},
    rightIcons: [],
    iconGap: "theme.spacing.none",
    iconRowSize: "theme.iconSizes.s",
    iconRowGap: "theme.spacing.none",
    iconRowOrientation: "horizontal",
    size: `${maxWidth}|1`,
    maxWidth,
  };
  const actorLabelVisible = actorIdentityVisible
    && state === "incoming"
    && requiredBoolean(
      actorLabelSlot,
      "showLabel",
      "component.bubble.actorLabel.showLabel",
    );
  const actorLabelUseActorColor = requiredBoolean(
    actorLabelSlot,
    "useActorColor",
    "component.bubble.actorLabel.useActorColor",
  );
  const actorLabelConfig = actorLabelVisible
    ? mergeComponentDefaults(
        componentVariantConfig(
          componentBaseConfigs,
          "label",
          requiredString(
            actorLabelSlot,
            "variantReference",
            "component.bubble.actorLabel.variantReference",
          ),
        ),
        asRecord(actorLabelSlot.overrides),
      )
    : undefined;
  const avatarVisible = actorIdentityVisible
    && state === "incoming"
    && requiredBoolean(
      avatarSlot,
      "showAvatar",
      "component.bubble.avatar.showAvatar",
    );
  const avatarConfig = avatarVisible
    ? mergeComponentDefaults(
        componentVariantConfig(
          componentBaseConfigs,
          "avatar",
          requiredString(
            avatarSlot,
            "variantReference",
            "component.bubble.avatar.variantReference",
          ),
        ),
        asRecord(avatarSlot.overrides),
      )
    : undefined;
  const resolvedTextBox = resolveTextBoxComponentFromRecords(
    textBoxConfig,
    textBoxInputs,
    componentBaseConfigs,
    "component.bubble.textBox",
  );

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
      ...resolvedTextBox,
      textAlign: optionalBoolean(preview, "typingIndicator") || state === "system"
        ? "center"
        : resolvedTextBox.textAlign,
      cursorVisible: simpleWriteOnFrameInProgress(fullText, writeOnPlan),
    },
    mediaSlot: {
      mediaType,
      position: bubbleMediaPosition(
        requiredString(bubble, "mediaPosition", "component.bubble.mediaPosition"),
      ),
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
            bubbleAudioInputs(preview, actorPreview, maxWidth),
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
      textColorOverride: actorLabelVisible && actorLabelUseActorColor
        ? actorPreview.avatar.backgroundColor
        : undefined,
      label: actorLabelConfig
        ? resolveLabelComponentFromRecords(
            actorLabelConfig,
            literalLabelPreview(actorPreview.displayName),
            componentBaseConfigs,
            "component.bubble.actorLabel",
            staticLabelFrameContext,
          )
        : undefined,
    },
    avatarSlot: {
      showAvatar: avatarVisible,
      placement: requiredPlacement(
        avatarSlot,
        "placement",
        "component.bubble.avatar.placement",
      ),
      avatar: avatarConfig
        ? resolveAvatarComponentFromRecords(
            avatarConfig,
            {
              ...preview,
              actor: actorPreview,
              showBadge: false,
              badgeIconToken: "system_check",
              badgeText: "1",
            },
            componentBaseConfigs,
            "component.bubble.avatar",
          )
        : undefined,
    },
    status: {
      text: optionalString(preview, "statusText"),
      state: bubbleStatusState(optionalString(preview, "statusState") || "none"),
      sizeToken: requiredString(status, "sizeToken", "component.bubble.status.sizeToken"),
      textSizeToken: requiredString(status, "textSizeToken", "component.bubble.status.textSizeToken"),
      gapToken: requiredString(status, "gapToken", "component.bubble.status.gapToken"),
      icons: {
        sent: statusIcon(status, "sent", "component.bubble.status.sent"),
        delivered: statusIcon(status, "delivered", "component.bubble.status.delivered"),
        read: statusIcon(status, "read", "component.bubble.status.read"),
      },
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

function resolveBubbleActorPreview(preview: Record<string, unknown>) {
  const actor = asRecord(
    preview.actor ?? defaultActorPreview(optionalString(preview, "actorName") || "Alex Q"),
  );
  const avatar = asRecord(actor.avatar);
  return {
    id: requiredString(actor, "id", "component.bubble.actor.id"),
    displayName: requiredString(actor, "displayName", "component.bubble.actor.displayName"),
    shortName: optionalString(actor, "shortName"),
    initials: requiredString(actor, "initials", "component.bubble.actor.initials"),
    avatar: {
      imageUri: optionalString(avatar, "imageUri"),
      backgroundColor: requiredString(
        avatar,
        "backgroundColor",
        "component.bubble.actor.avatar.backgroundColor",
      ),
      textColor: requiredString(
        avatar,
        "textColor",
        "component.bubble.actor.avatar.textColor",
      ),
      scale: optionalNumber(avatar, "scale", 1),
      offsetX: optionalNumber(avatar, "offsetX", 0),
      offsetY: optionalNumber(avatar, "offsetY", 0),
      baseSize: optionalNumber(avatar, "baseSize", 256),
    },
  };
}

function statusIcon(
  status: Record<string, unknown>,
  state: Exclude<BubbleStatusState, "none">,
  path: string,
) {
  const raw = asRecord(status[state]);
  return {
    iconToken: optionalString(raw, "iconToken"),
    colorToken: requiredString(raw, "colorToken", `${path}.colorToken`),
  };
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

function bubbleMediaPosition(value: string): BubbleMediaPosition {
  if (value === "top" || value === "bottom" || value === "left" || value === "right") {
    return value;
  }
  throw new Error(`Unsupported bubble media position ${value}`);
}

function bubbleStatusState(value: string): BubbleStatusState {
  if (value === "none" || value === "sent" || value === "delivered" || value === "read") {
    return value;
  }
  throw new Error(`Unsupported bubble status state ${value}`);
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
    playbackMode: optionalString(preview, "playbackMode") || "once",
    fullframeOrientation: optionalString(preview, "fullframeOrientation") || "portrait",
    controlsElapsedMs: optionalNumber(preview, "controlsElapsedMs", 0),
    motionElapsedMs: optionalNumber(preview, "motionElapsedMs", 0),
  };
}

function bubbleAudioInputs(
  preview: Record<string, unknown>,
  actorPreview: ReturnType<typeof resolveBubbleActorPreview>,
  availableWidth: number,
) {
  return {
    ...preview,
    actor: actorPreview,
    availableWidth,
    // Bubble owns this child-input binding at its Audio slot boundary.
    // It is explicit composition data, not an Audio resolver fallback.
    showBadge: false,
    isPlaying: optionalBoolean(preview, "isPlaying"),
    durationSeconds: Math.max(1, optionalNumber(preview, "durationSeconds", 65)),
    currentTimeSeconds: optionalNumber(preview, "currentTimeSeconds", 0),
    playbackMode: optionalString(preview, "playbackMode") || "once",
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
