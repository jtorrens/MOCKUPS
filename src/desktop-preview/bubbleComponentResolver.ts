import {
  embeddedComponentConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  optionalBoolean,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredPlacement,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import type {
  BubbleAlignment,
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
  validateBubbleRuntimeDocument(preview);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const bubble = requiredRecord(config, "bubble", "component.bubble");
  const surfaceSlot = requiredRecord(bubble, "surfaceSlot", "component.bubble");
  const textBoxSlot = requiredRecord(bubble, "textBoxSlot", "component.bubble");
  const imageMediaSlot = requiredRecord(bubble, "imageMediaSlot", "component.bubble");
  const videoMediaSlot = requiredRecord(bubble, "videoMediaSlot", "component.bubble");
  const audioSlot = requiredRecord(bubble, "audioSlot", "component.bubble");
  const actorLabelSlot = requiredRecord(bubble, "actorLabelSlot", "component.bubble");
  const avatarSlot = requiredRecord(bubble, "avatarSlot", "component.bubble");
  const status = requiredRecord(bubble, "status", "component.bubble");
  const maxWidthPercent = Math.min(
    100,
    Math.max(1, requiredNumber(preview, "maxWidth", "component.bubble.input.maxWidth")),
  );
  const maxWidth = screenPercentToDesignWidth(payload, maxWidthPercent);
  const padding = requiredStringPair(bubble, "padding", "component.bubble.padding");
  const state = bubbleState(requiredString(preview, "state", "component.bubble.input.state"));
  const alignments: Record<BubbleState, BubbleAlignment> = {
    incoming: bubbleAlignment(requiredString(
      bubble,
      "incomingAlignment",
      "component.bubble.incomingAlignment",
    )),
    system: bubbleAlignment(requiredString(
      bubble,
      "systemAlignment",
      "component.bubble.systemAlignment",
    )),
    outgoing: bubbleAlignment(requiredString(
      bubble,
      "outgoingAlignment",
      "component.bubble.outgoingAlignment",
    )),
  };
  const alignment = alignments[state];
  const actorIdentityVisible = typeof preview.actorIdentityVisible === "boolean"
    ? preview.actorIdentityVisible
    : true;
  const actorPreview = actorIdentityVisible && state === "incoming"
    ? resolveBubbleActorPreview(preview)
    : undefined;
  const fullText = requiredPossiblyEmptyString(
    preview,
    "sampleText",
    "component.bubble.input.sampleText",
  );
  const writeOnPlan: SimpleWriteOnFramePlan = {
    enabled: requiredBoolean(preview, "writeOnTrigger", "component.bubble.input.writeOnTrigger"),
    frame: requiredNumber(preview, "writeOnFrame", "component.bubble.input.writeOnFrame"),
    durationFrames: requiredNumber(
      preview,
      "writeOnDurationFrames",
      "component.bubble.input.writeOnDurationFrames",
    ),
  };
  const visibleText = simpleWriteOnFrameText(fullText, writeOnPlan);
  const surfaceConfig = embeddedComponentConfig(
    componentBaseConfigs,
    surfaceSlot,
    "surface",
    "component.bubble.surfaceSlot",
  );
  const textBoxBaseConfig = embeddedComponentConfig(
    componentBaseConfigs,
    textBoxSlot,
    "textBox",
    "component.bubble.textBoxSlot",
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
    requiredString(preview, "mediaType", "component.bubble.input.mediaType"),
  );
  const imageMediaConfig = mediaType === "image"
    ? embeddedComponentConfig(
        componentBaseConfigs,
        imageMediaSlot,
        "media",
        "component.bubble.imageMediaSlot",
      )
    : undefined;
  const videoMediaConfig = mediaType === "video"
    ? embeddedComponentConfig(
        componentBaseConfigs,
        videoMediaSlot,
        "media",
        "component.bubble.videoMediaSlot",
      )
    : undefined;
  let audioConfig = mediaType === "audio"
    ? embeddedComponentConfig(
        componentBaseConfigs,
        audioSlot,
        "audio",
        "component.bubble.audioSlot",
      )
    : undefined;
  if (audioConfig && !actorPreview) {
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
    textSizeToken: optionalString(preview, "textSizeToken"),
    textAnimationMode: optionalString(preview, "textAnimationMode"),
    textAnimationElapsedMs: optionalNumber(preview, "textAnimationElapsedMs", 0),
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
    ? embeddedComponentConfig(
        componentBaseConfigs,
        actorLabelSlot,
        "label",
        "component.bubble.actorLabelSlot",
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
    ? embeddedComponentConfig(
        componentBaseConfigs,
        avatarSlot,
        "avatar",
        "component.bubble.avatarSlot",
      )
    : undefined;
  const resolvedTextBox = resolveTextBoxComponentFromRecords(
    textBoxConfig,
    textBoxInputs,
    componentBaseConfigs,
    "component.bubble.textBox",
    payload,
  );

  return {
    id: "component.bubble",
    state,
    alignment,
    maxWidth,
    padding: { xToken: padding.first, yToken: padding.second },
    surface: bubbleSurfaceForAlignment(
      resolveSurfaceComponentAtSize(
        surfaceConfig,
        { width: maxWidth, height: 1 },
        "component.bubble.surface",
      ),
      alignment,
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
            payload,
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
        ? actorPreview!.avatar.backgroundColor
        : undefined,
      label: actorLabelConfig
        ? resolveLabelComponentFromRecords(
            actorLabelConfig,
            literalLabelPreview(actorPreview!.displayName),
            componentBaseConfigs,
            "component.bubble.actorLabel",
            staticLabelFrameContext,
          )
        : undefined,
    },
    avatarSlot: {
      showAvatar: avatarVisible,
      reserveTextSpace: requiredBoolean(
        avatarSlot,
        "reserveTextSpace",
        "component.bubble.avatar.reserveTextSpace",
      ),
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
              actor: actorPreview!,
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
      text: requiredPossiblyEmptyString(
        preview,
        "statusText",
        "component.bubble.input.statusText",
      ),
      state: bubbleStatusState(requiredString(
        preview,
        "statusState",
        "component.bubble.input.statusState",
      )),
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
  const actor = Object.hasOwn(preview, "actor")
    ? requiredRecord(preview, "actor", "component.bubble")
    : defaultActorPreview(requiredString(
        preview,
        "actorName",
        "component.bubble.input.actorName",
      ));
  const avatar = requiredRecord(actor, "avatar", "component.bubble.actor");
  return {
    id: requiredString(actor, "id", "component.bubble.actor.id"),
    displayName: requiredString(actor, "displayName", "component.bubble.actor.displayName"),
    shortName: requiredPossiblyEmptyString(
      actor,
      "shortName",
      "component.bubble.actor.shortName",
    ),
    initials: requiredString(actor, "initials", "component.bubble.actor.initials"),
    avatar: {
      imageUri: requiredPossiblyEmptyString(
        avatar,
        "imageUri",
        "component.bubble.actor.avatar.imageUri",
      ),
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
      scale: requiredNumber(avatar, "scale", "component.bubble.actor.avatar.scale"),
      offsetX: requiredNumber(avatar, "offsetX", "component.bubble.actor.avatar.offsetX"),
      offsetY: requiredNumber(avatar, "offsetY", "component.bubble.actor.avatar.offsetY"),
      baseSize: requiredNumber(avatar, "baseSize", "component.bubble.actor.avatar.baseSize"),
    },
  };
}

function statusIcon(
  status: Record<string, unknown>,
  state: Exclude<BubbleStatusState, "none">,
  path: string,
) {
  const raw = requiredRecord(status, state, "component.bubble.status");
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
  _maxWidth: number,
) {
  return {
    ...preview,
    mediaType,
    mediaSource: requiredPossiblyEmptyString(
      preview,
      "mediaSource",
      "component.bubble.input.mediaSource",
    ),
    viewportSize: requiredString(preview, "viewportSize", "component.bubble.input.viewportSize"),
    mediaOffset: requiredString(preview, "mediaOffset", "component.bubble.input.mediaOffset"),
    mediaScale: requiredNumber(preview, "mediaScale", "component.bubble.input.mediaScale"),
    isPlaying: requiredBoolean(preview, "isPlaying", "component.bubble.input.isPlaying"),
    isFullScreen: requiredBoolean(
      preview,
      "isFullScreen",
      "component.bubble.input.isFullScreen",
    ),
    fullScreenTransition: requiredBoolean(
      preview,
      "fullScreenTransition",
      "component.bubble.input.fullScreenTransition",
    ),
    currentTimeSeconds: requiredNumber(
      preview,
      "currentTimeSeconds",
      "component.bubble.input.currentTimeSeconds",
    ),
    durationSeconds: Math.max(
      1,
      requiredNumber(preview, "durationSeconds", "component.bubble.input.durationSeconds"),
    ),
    playbackMode: optionalString(preview, "playbackMode") || "once",
    fullframeOrientation: requiredString(
      preview,
      "fullframeOrientation",
      "component.bubble.input.fullframeOrientation",
    ),
    controlsElapsedMs: requiredNumber(
      preview,
      "controlsElapsedMs",
      "component.bubble.input.controlsElapsedMs",
    ),
    motionElapsedMs: requiredNumber(
      preview,
      "motionElapsedMs",
      "component.bubble.input.motionElapsedMs",
    ),
  };
}

function bubbleAudioInputs(
  preview: Record<string, unknown>,
  actorPreview: ReturnType<typeof resolveBubbleActorPreview> | undefined,
  availableWidth: number,
) {
  return {
    ...preview,
    actor: actorPreview ?? {},
    availableWidth,
    // Bubble owns this child-input binding at its Audio slot boundary.
    // It is explicit composition data, not an Audio resolver fallback.
    showBadge: false,
    isPlaying: requiredBoolean(preview, "isPlaying", "component.bubble.input.isPlaying"),
    durationSeconds: Math.max(
      1,
      requiredNumber(preview, "durationSeconds", "component.bubble.input.durationSeconds"),
    ),
    currentTimeSeconds: requiredNumber(
      preview,
      "currentTimeSeconds",
      "component.bubble.input.currentTimeSeconds",
    ),
    playbackMode: optionalString(preview, "playbackMode") || "once",
  };
}

function validateBubbleRuntimeDocument(preview: Record<string, unknown>) {
  requiredString(preview, "state", "component.bubble.input.state");
  requiredPossiblyEmptyString(preview, "sampleText", "component.bubble.input.sampleText");
  requiredNumber(preview, "maxWidth", "component.bubble.input.maxWidth");
  requiredNumber(
    preview,
    "writeOnDurationFrames",
    "component.bubble.input.writeOnDurationFrames",
  );
  requiredBoolean(preview, "writeOnTrigger", "component.bubble.input.writeOnTrigger");
  requiredNumber(preview, "writeOnFrame", "component.bubble.input.writeOnFrame");
  requiredPossiblyEmptyString(preview, "actorId", "component.bubble.input.actorId");
  requiredPossiblyEmptyString(preview, "actorName", "component.bubble.input.actorName");
  requiredPossiblyEmptyString(preview, "statusText", "component.bubble.input.statusText");
  requiredString(preview, "statusState", "component.bubble.input.statusState");
  requiredString(preview, "mediaType", "component.bubble.input.mediaType");
  requiredPossiblyEmptyString(preview, "mediaSource", "component.bubble.input.mediaSource");
  requiredString(preview, "viewportSize", "component.bubble.input.viewportSize");
  requiredNumber(preview, "mediaScale", "component.bubble.input.mediaScale");
  requiredString(preview, "mediaOffset", "component.bubble.input.mediaOffset");
  requiredBoolean(preview, "isPlaying", "component.bubble.input.isPlaying");
  requiredNumber(preview, "currentTimeSeconds", "component.bubble.input.currentTimeSeconds");
  requiredNumber(preview, "durationSeconds", "component.bubble.input.durationSeconds");
  requiredBoolean(preview, "isFullScreen", "component.bubble.input.isFullScreen");
  requiredBoolean(
    preview,
    "fullScreenTransition",
    "component.bubble.input.fullScreenTransition",
  );
  requiredString(
    preview,
    "fullframeOrientation",
    "component.bubble.input.fullframeOrientation",
  );
  requiredNumber(preview, "controlsElapsedMs", "component.bubble.input.controlsElapsedMs");
  requiredNumber(preview, "motionElapsedMs", "component.bubble.input.motionElapsedMs");
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

function bubbleSurfaceForAlignment(
  surface: BubbleDesignContract["surface"],
  alignment: BubbleAlignment,
): SurfaceDesignContract {
  const side: SurfaceDesignContract["tail"]["side"] = alignment === "right" ? "right" : "left";
  return {
    ...surface,
    tail: {
      ...surface.tail,
      enabled: surface.tail.enabled && alignment !== "center",
      side,
    },
  };
}

function bubbleAlignment(value: string): BubbleAlignment {
  if (value === "left" || value === "center" || value === "right") return value;
  throw new Error(`Unsupported bubble alignment ${value}`);
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
