import {
  optionalBoolean,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  optionalObject,
  requiredObjectArray,
  type JsonRecord,
} from "./previewJsonHelpers.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { rootScreenFrame } from "./previewFrameContext.js";
import { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";
import { naturalWriteOnFrame } from "./behaviorTiming.js";
import type {
  ConversationMessageContract,
  ConversationModuleContract,
  ConversationIncomingRevealMode,
  ConversationTimingContract,
  ConversationTypingIndicatorAnimation,
} from "./conversationModuleContract.js";
import {
  simpleWriteOnFrameVisibleCount,
  textGraphemes,
} from "./previewTextRevealHelpers.js";
import {
  motionTotalDurationMs,
  requiredMotionContract,
  resolveMotionFrame,
} from "./previewMotionHelpers.js";
import type { ComponentMotionContract } from "./previewComponentContracts.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import {
  applyRuntimeInputForwarding,
  forwardedRuntimeInputPatch,
} from "./runtimeInputForwarding.js";
import { renderScale } from "./previewGeometryHelpers.js";

export function resolveConversationModule(
  payload: DesignPreviewPayload,
): ConversationModuleContract {
  const preview = resolveConversationModuleFrame(payload);
  const config = parseObject(payload.configJson);
  const conversation = requiredRecord(config, "conversation", "module config");
  const screenFrame = rootScreenFrame(payload);
  const resolvedMessages = conversationMessages(preview);
  const timing = conversationTiming(conversation, preview);
  const showKeyboard = requiredBoolean(
    conversation,
    "showKeyboard",
    "module.core.chat.showKeyboard",
  );
  const showTextInputBar = requiredBoolean(
    conversation,
    "showTextInputBar",
    "module.core.chat.showTextInputBar",
  );
  const messageMotion = requiredMotionContract(
    conversation,
    "messageMotion",
    "module.core.chat.messageMotion",
  );
  const composer = composerState(resolvedMessages, screenFrame, timing);
  const conversationType = requiredString(
    preview,
    "conversationType",
    "module.core.chat.input.conversationType",
  );
  if (conversationType !== "individual" && conversationType !== "group") {
    throw new Error(`Unsupported Conversation type ${conversationType}`);
  }
  const automaticEndFrame = Math.max(
    1,
    payload.screenTiming?.actionDurationFrames
      ?? optionalNumber(preview, "timelineDurationFrames", 1),
  );
  const visible = visibleMessages(
    resolvedMessages,
    screenFrame,
    timing,
    payload,
    messageMotion,
    automaticEndFrame,
    !showKeyboard && !showTextInputBar,
  )
    .map((message) => ({
      ...message,
      actorIdentityVisible: conversationMessageActorIdentityVisible(
        conversationType,
        message.state,
      ),
      playbackTimeSeconds: messagePlaybackTimeSeconds(message, payload.frameRate),
    }));
  const motionElapsedMs = screenFrame / Math.max(1, payload.frameRate) * 1000;
  const viewportMotion = conversation.messageViewportMotion
    ? requiredMotionContract(
        conversation,
        "messageViewportMotion",
        "module.core.chat.messageViewportMotion",
      )
    : {
        transition: "slide" as const,
        direction: "bottom" as const,
        bounds: "parent" as const,
        fade: false,
        translate: true,
        scale: false,
      };
  const viewportMotionProgress = resolveMotionFrame(payload, viewportMotion, {
    trigger: optionalBoolean(preview, "composerTransitionTrigger"),
    elapsedMs: optionalNumber(preview, "composerTransitionElapsedMs", 0),
  }).progress;
  const latestAppearanceFrame = visible.reduce(
    (latest, message) => Math.max(latest, message.visibleAtFrame),
    0,
  );
  const scrollMotionProgress = resolveMotionFrame(
    payload,
    {
      transition: "slide",
      direction: "bottom",
      bounds: "parent",
      fade: false,
      translate: true,
      scale: false,
    },
    {
      trigger: latestAppearanceFrame > 0,
      elapsedMs: Math.max(0, screenFrame - latestAppearanceFrame)
        / Math.max(1, payload.frameRate) * 1000,
    },
  ).progress;
  const keyboardVisible = composer.keyboardVisible
    && showKeyboard;
  const textInputVisible = composer.textInputVisible
    && showTextInputBar;
  const textInputConfig = textInputVisible
    ? resolvedTextInputConfig(payload, conversation, composer.text)
    : undefined;
  return {
    id: "conversation",
    preview,
    conversationType,
    frame: screenFrame,
    motionElapsedMs,
    timing,
    composer: {
      ...composer,
      keyboardVisible,
      textInputVisible,
    },
    messages: requiredObjectArray(preview, "messages", "module.conversation runtime"),
    visibleMessages: visible,
    viewportMotionProgress,
    scrollMotionProgress,
    ...(textInputConfig ? { textInputConfig } : {}),
  };
}

export function resolveConversationModuleFrame(
  payload: DesignPreviewPayload,
): JsonRecord {
  const preview = parseObject(payload.designPreviewJson);
  const instance = parseObject(payload.instanceJson);
  const animation = optionalObject(instance, "animation", "Preview instance envelope");
  const screenFrame = rootScreenFrame(payload);
  const themeTokens = parseObject(payload.themeTokensJson);
  const timeline = new RuntimeOwnerTimeline(
    preview,
    preview,
    animation,
    themeTokens,
    0,
    payload.frameRate,
  );
  const automaticEndFrame = Math.max(
    1,
    payload.screenTiming?.actionDurationFrames ?? timeline.durationFrames,
  );
  preview.timelineDurationFrames = automaticEndFrame;
  preview.headerSubtitle = resolveParameterAnimation(
    animation,
    "headerSubtitle",
    "",
    timeline.temporalLocalFrame("headerSubtitle", "", screenFrame),
    preview.headerSubtitle,
  ).value;

  const messages = requiredObjectArray(preview, "messages", "module.conversation runtime");
  preview.messages = messages.map((value, index) => {
    const message = { ...value };
    const targetId = requiredString(
      message,
      "id",
      `module.core.chat.messages[${index}]`,
    );
    const direction = requiredString(
      message,
      "direction",
      `module.core.chat.messages[${index}]`,
    );
    if (direction !== "incoming" && direction !== "outgoing" && direction !== "system") {
      throw new Error(
        `module.core.chat.messages[${index}] has unsupported direction '${direction}'`,
      );
    }
    message.timelineStartFrame = timeline.itemStartFrame(targetId);
    message.timelineEndFrame = timeline.itemEndFrame(targetId);
    const presenceEndFrame = timeline.itemPresenceEndFrame(targetId, automaticEndFrame);
    message.presenceEndFrame = presenceEndFrame;
    message.hasExplicitPresenceEnd = timeline.itemHasExplicitPresenceEnd(targetId);
    message.timelineTemporalFrame = timeline.temporalOwnerFrame(
      targetId,
      screenFrame,
      message.hasExplicitPresenceEnd ? presenceEndFrame : undefined,
    );
    const resolve = (fieldId: string, baseValue: unknown) =>
      resolveParameterAnimation(
        animation,
        fieldId,
        targetId,
        timeline.temporalLocalFrame(
          fieldId,
          targetId,
          screenFrame,
          message.hasExplicitPresenceEnd ? presenceEndFrame : undefined,
        ),
        baseValue,
      );

    const resolvedText = resolve("text", message.text);
    message.text = resolvedText.value;
    const textCompletionFrame = timeline.fieldCompletionFrame("text", targetId);
    const textOriginFrame = timeline.screenFrame("text", targetId, 0);
    const textUsesTrackCompletion = timeline.usesTrackCompletion("text", targetId);
    const postHold = direction === "outgoing"
      ? Math.max(0, optionalNumber(message, "postWriteOnHoldFrames", 0))
      : 0;
    message.timelineRevealAtFrame = timeline.itemOwnerFrame(
      targetId,
      timeline.fieldCompletionLocal("text", targetId) + postHold,
    );
    message.timelineTextStartFrame = textOriginFrame;
    message.writeOnDurationFrames = textUsesTrackCompletion
      ? 0
      : Math.max(0, textCompletionFrame - textOriginFrame);
    // A text track already resolves the complete value for this exact frame,
    // including its destination keyframe's write-on segment.  The composer
    // must display that value directly instead of applying the base write-on
    // a second time.
    message.composerWriteOnDurationFrames = textUsesTrackCompletion
      ? 0
      : Math.max(0, textCompletionFrame - textOriginFrame);
    message.writeOnFrame = naturalWriteOnFrame(
      optionalString(message, "text"),
      message.writeOnTiming,
      Math.max(0, timeline.temporalLocalFrame(
        "text",
        targetId,
        screenFrame,
        message.hasExplicitPresenceEnd ? presenceEndFrame : undefined,
      )),
      optionalNumber(message, "writeOnDurationFrames", 0),
      `${targetId}:${optionalString(message, "text")}`,
    );
    const composerElapsedFrame = Math.max(
      0,
      timeline.temporalLocalFrame(
        "text",
        targetId,
        screenFrame,
        message.hasExplicitPresenceEnd ? presenceEndFrame : undefined,
      ),
    );
    message.composerWriteOnFrame = naturalWriteOnFrame(
      optionalString(message, "text"),
      message.writeOnTiming,
      composerElapsedFrame,
      optionalNumber(message, "composerWriteOnDurationFrames", 0),
      `${targetId}:${optionalString(message, "text")}`,
    );
    message.statusVisible = resolve("statusVisible", message.statusVisible).value;
    message.statusState = resolve("status", message.statusState).value;
    message.statusText = resolve("statusText", message.statusText).value;
    const playing = resolve("isPlaying", message.isPlaying);
    message.isPlaying = playing.value;
    if (playing.animated && playing.value === true && playing.sourceKeyframeFrame !== undefined) {
      const elapsed = Math.max(
        0,
        timeline.temporalLocalFrame(
          "isPlaying",
          targetId,
          screenFrame,
          message.hasExplicitPresenceEnd ? presenceEndFrame : undefined,
        ) - playing.sourceKeyframeFrame,
      );
      const duration = Math.max(1, Math.floor(optionalNumber(message, "playDurationFrames", 1)));
      message.isPlaying = elapsed < duration;
      message.playbackFrame = Math.min(elapsed, duration);
    }
    const fullScreen = resolve("fullScreen", message.isFullScreen);
    message.isFullScreen = fullScreen.value;
    const fullScreenChanged = fullScreen.sourceKeyframeFrame !== undefined
      && typeof fullScreen.previousValue === "boolean"
      && typeof fullScreen.value === "boolean"
      && fullScreen.previousValue !== fullScreen.value;
    if (fullScreenChanged) {
      const ownerFrame = timeline.temporalLocalFrame(
        "fullScreen",
        targetId,
        screenFrame,
        message.hasExplicitPresenceEnd ? presenceEndFrame : undefined,
      );
      message.fullScreenTransition = true;
      message.motionElapsedMs = Math.max(
        0,
        ownerFrame - fullScreen.sourceKeyframeFrame!,
      ) / Math.max(1, payload.frameRate) * 1000;
    }
    return message;
  });
  return preview;
}

export function conversationMessageActorIdentityVisible(
  conversationType: string,
  direction: string,
) {
  return conversationType === "group" && direction === "incoming";
}

type ResolvedConversationMessage = Omit<
  ConversationMessageContract,
  | "actorIdentityVisible"
  | "playbackTimeSeconds"
  | "presenceMotion"
  | "presenceMotionKind"
  | "presenceMotionFrame"
> & {
  composerWriteOnDurationFrames: number;
  composerWriteOnFrame: number;
  timelineStartFrame: number;
  timelineTextStartFrame: number;
  timelineTemporalFrame: number;
  timelineRevealAtFrame: number;
  presenceEndFrame: number;
  hasExplicitPresenceEnd: boolean;
  playbackMode: "once" | "loop";
  playbackFrame: number;
  currentTimeSeconds: number;
};

function conversationTiming(
  conversation: JsonRecord,
  preview: JsonRecord,
): ConversationTimingContract {
  const incomingRevealMode = optionalString(preview, "incomingRevealMode")
    || optionalString(conversation, "incomingRevealMode");
  const resolvedIncomingRevealMode = incomingRevealMode || "writeOn";
  if (resolvedIncomingRevealMode !== "writeOn"
    && resolvedIncomingRevealMode !== "typingIndicator") {
    throw new Error(
      `Unsupported Conversation incoming reveal mode ${resolvedIncomingRevealMode}`,
    );
  }
  const bubbleRevealMode = optionalString(preview, "bubbleRevealMode")
    || optionalString(conversation, "bubbleRevealMode");
  return {
    bubbleRevealMode: bubbleRevealMode === "afterWriteOn" ? "afterWriteOn" : "duringWriteOn",
    incomingRevealMode: resolvedIncomingRevealMode as ConversationIncomingRevealMode,
    textInputVisible: optionalBooleanWithFallback(preview, conversation, "textInputVisible", true),
    keyboardVisible: optionalBooleanWithFallback(preview, conversation, "keyboardVisible", true),
    typingIndicatorText: optionalString(preview, "typingIndicatorText")
      || optionalString(conversation, "typingIndicatorText")
      || "•••",
    typingIndicatorSizeToken: optionalString(preview, "typingIndicatorSizeToken")
      || optionalString(conversation, "typingIndicatorSizeToken")
      || "theme.typography.sizes.m",
    typingIndicatorAnimation: typingIndicatorAnimation(
      optionalString(preview, "typingIndicatorAnimation")
        || optionalString(conversation, "typingIndicatorAnimation"),
    ),
  };
}

function typingIndicatorAnimation(
  value: string | undefined,
): ConversationTypingIndicatorAnimation {
  return value === "none" || value === "wave" ? value : "pulsating";
}

function optionalBooleanWithFallback(
  primary: JsonRecord,
  secondary: JsonRecord,
  key: string,
  fallback: boolean,
) {
  if (typeof primary[key] === "boolean") return primary[key];
  if (typeof secondary[key] === "boolean") return secondary[key];
  return fallback;
}

function conversationMessages(preview: JsonRecord): ResolvedConversationMessage[] {
  const messages = requiredObjectArray(preview, "messages", "module.conversation runtime");
  return messages.map((message, index) => {
    const path = `module.core.chat.messages[${index}]`;
    return {
      actor: optionalObject(message, "actor", path),
      state: requiredString(message, "direction", path),
      text: optionalString(message, "text"),
      statusState: optionalString(message, "statusState") || "none",
      statusText: optionalString(message, "statusText"),
      composerWriteOnDurationFrames: Math.max(
        0,
        Math.floor(optionalNumber(
          message,
          "composerWriteOnDurationFrames",
          optionalNumber(message, "writeOnDurationFrames", 0),
        )),
      ),
      composerWriteOnFrame: Math.max(
        0,
        Math.floor(optionalNumber(message, "composerWriteOnFrame", 0)),
      ),
      timelineStartFrame: Math.max(
        0,
        Math.floor(optionalNumber(message, "timelineStartFrame", 0)),
      ),
      timelineTextStartFrame: Math.max(
        0,
        Math.floor(optionalNumber(
          message,
          "timelineTextStartFrame",
          optionalNumber(message, "timelineStartFrame", 0),
        )),
      ),
      timelineTemporalFrame: Math.max(
        0,
        Math.floor(optionalNumber(message, "timelineTemporalFrame", 0)),
      ),
      timelineRevealAtFrame: Math.max(
        0,
        Math.floor(optionalNumber(message, "timelineRevealAtFrame", 0)),
      ),
      presenceEndFrame: Math.max(
        1,
        Math.floor(optionalNumber(message, "presenceEndFrame", 1)),
      ),
      hasExplicitPresenceEnd: optionalBoolean(message, "hasExplicitPresenceEnd"),
      writeOnDurationFrames: Math.max(
        0,
        Math.floor(optionalNumber(message, "writeOnDurationFrames", 0)),
      ),
      writeOnTrigger: false,
      writeOnFrame: Math.max(0, Math.floor(optionalNumber(message, "writeOnFrame", 0))),
      statusVisible: optionalBoolean(message, "statusVisible")
        || optionalString(message, "statusState") !== "none",
      visibleAtFrame: 0,
      mediaType: messageMediaType(message),
      mediaSource: optionalString(message, "mediaSource"),
      viewportSize: optionalString(message, "viewportSize") || "240|160",
      mediaScale: optionalNumber(message, "mediaScale", 1),
      mediaOffset: optionalString(message, "mediaOffset") || "0|0",
      isPlaying: optionalBoolean(message, "isPlaying"),
      playbackMode: playbackMode(optionalString(message, "playbackMode")),
      playbackFrame: Math.max(0, Math.floor(optionalNumber(message, "playbackFrame", 0))),
      durationSeconds: Math.max(1, optionalNumber(message, "durationSeconds", 12)),
      isFullScreen: optionalBoolean(message, "isFullScreen"),
      fullScreenTransition: optionalBoolean(message, "fullScreenTransition"),
      fullScreenMotionElapsedMs: optionalNumber(message, "motionElapsedMs", 0),
      fullframeOrientation: optionalString(message, "fullframeOrientation") || "portrait",
      controlsElapsedMs: optionalNumber(message, "controlsElapsedMs", 0),
      isTypingIndicator: false,
      currentTimeSeconds: optionalNumber(message, "currentTimeSeconds", 0),
    };
  });
}

function visibleMessages(
  messages: ResolvedConversationMessage[],
  frame: number,
  timing: ConversationTimingContract,
  payload: DesignPreviewPayload,
  messageMotion: ComponentMotionContract,
  automaticEndFrame: number,
  writesInBubble: boolean,
) {
  return messages.flatMap((message) => {
    const startFrame = message.timelineStartFrame;
    const isSystemMessage = message.state === "system";
    const isOutgoingMessage = message.state === "outgoing";
    const isIncomingMessage = message.state === "incoming";
    const effectiveWriteOnFrames = isSystemMessage ? 0 : message.writeOnDurationFrames;
    const actionFrame = message.timelineTemporalFrame;
    const revealAfterWriteOn = isOutgoingMessage
      && timing.bubbleRevealMode === "afterWriteOn"
      && !writesInBubble;
    const visibleAt = revealAfterWriteOn ? message.timelineRevealAtFrame : startFrame;
    if (frame < visibleAt || frame >= message.presenceEndFrame) return [];
    const motionDurationFrames = Math.ceil(
      motionTotalDurationMs(payload, messageMotion)
        / 1000 * Math.max(1, payload.frameRate),
    );
    const explicitExit = message.hasExplicitPresenceEnd
      && message.presenceEndFrame < automaticEndFrame;
    const exitStartFrame = Math.max(visibleAt, message.presenceEndFrame - motionDurationFrames);
    const presenceMotion = explicitExit && frame >= exitStartFrame
      ? {
          presenceMotionKind: "exit" as const,
          presenceMotionFrame: resolveMotionFrame(payload, messageMotion, {
            trigger: true,
            elapsedMs: Math.max(0, frame - exitStartFrame)
              / Math.max(1, payload.frameRate) * 1000,
          }),
        }
      : motionDurationFrames > 0 && frame - visibleAt < motionDurationFrames
        ? {
            presenceMotionKind: "enter" as const,
            presenceMotionFrame: resolveMotionFrame(payload, messageMotion, {
              trigger: true,
              elapsedMs: Math.max(0, frame - visibleAt)
                / Math.max(1, payload.frameRate) * 1000,
            }),
          }
        : {};
    const incomingTyping = isIncomingMessage
      && timing.incomingRevealMode === "typingIndicator"
      && actionFrame < effectiveWriteOnFrames;
    const incomingWriteOn = isIncomingMessage
      && timing.incomingRevealMode === "writeOn"
      && effectiveWriteOnFrames > 0
      && actionFrame < effectiveWriteOnFrames;
    const messageIsWriting = actionFrame < effectiveWriteOnFrames
      && effectiveWriteOnFrames > 0
      && (isOutgoingMessage || incomingWriteOn || incomingTyping);
    return [{
      ...message,
      visibleAtFrame: visibleAt,
      text: incomingTyping
        ? timing.typingIndicatorText
        : actionFrame <= 0 && effectiveWriteOnFrames > 0
          ? ""
          : message.text,
      mediaType: messageIsWriting ? "none" as const : message.mediaType,
      mediaSource: messageIsWriting ? "" : message.mediaSource,
      isTypingIndicator: incomingTyping,
      writeOnTrigger: (isOutgoingMessage || incomingWriteOn)
        && !revealAfterWriteOn
        && actionFrame >= 0
        && effectiveWriteOnFrames > 0,
      writeOnDurationFrames: effectiveWriteOnFrames,
      presenceMotion: messageMotion,
      ...presenceMotion,
    }];
  });
}

function composerState(
  messages: ResolvedConversationMessage[],
  frame: number,
  timing: ConversationTimingContract,
) {
  for (const message of messages) {
    const startFrame = message.timelineTextStartFrame;
    const effectiveWriteOnFrames = message.state === "system"
      ? 0
      : message.composerWriteOnDurationFrames;
    const holdEndFrame = message.timelineRevealAtFrame;
    const composerVisible = message.state === "outgoing"
      && frame >= startFrame
      && frame < holdEndFrame;
    if (composerVisible) {
      const graphemes = textGraphemes(message.text);
      const writeOnInProgress = message.timelineTemporalFrame < effectiveWriteOnFrames;
      const textLength = writeOnInProgress
        ? simpleWriteOnFrameVisibleCount(message.text, {
            enabled: true,
            frame: message.composerWriteOnFrame,
            durationFrames: effectiveWriteOnFrames,
          })
        : graphemes.length;
      return {
        text: graphemes.slice(0, textLength).join(""),
        currentCharacter: writeOnInProgress ? textLength : 0,
        textInputVisible: timing.textInputVisible,
        keyboardVisible: timing.keyboardVisible,
      };
    }
  }
  return {
    text: "",
    currentCharacter: 0,
    textInputVisible: false,
    keyboardVisible: false,
  };
}

function messageMediaType(message: JsonRecord): ResolvedConversationMessage["mediaType"] {
  const mediaType = optionalString(message, "mediaType");
  return mediaType === "image" || mediaType === "video" || mediaType === "audio"
    ? mediaType
    : "none";
}

function playbackMode(value: string): ResolvedConversationMessage["playbackMode"] {
  return value === "loop" ? "loop" : "once";
}

function messagePlaybackTimeSeconds(
  message: ResolvedConversationMessage,
  frameRate: number,
) {
  const elapsedSeconds = message.playbackFrame > 0
    ? message.playbackFrame / Math.max(1, frameRate)
    : message.currentTimeSeconds;
  if (message.playbackMode === "loop") {
    return elapsedSeconds % message.durationSeconds;
  }
  return Math.min(message.durationSeconds, Math.max(0, elapsedSeconds));
}

function resolvedTextInputConfig(
  payload: DesignPreviewPayload,
  conversation: JsonRecord,
  text: string,
) {
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const slot = requiredRecord(
    conversation,
    "textInputBarSlot",
    "module.core.chat.textInputBarSlot",
  );
  const config = embeddedComponentConfig(
    componentBaseConfigs,
    slot,
    "textInputBar",
    "module.core.chat.textInputBarSlot",
  );
  const resolved = applyRuntimeInputForwarding({
    ...payload,
    kind: "componentClass",
    componentType: "textInputBar",
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify({
      ...forwardedRuntimeInputPatch(
        config,
        "forwarded.component.textInputBar.textBox.inputs.sampleText",
        text,
      ),
      availableWidth: payload.previewFrame.screenWidth / renderScale(payload),
    }),
  });
  return parseObject(resolved.configJson);
}
