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
  ConversationTimingContract,
  ConversationTypingIndicatorAnimation,
} from "./conversationModuleContract.js";
import {
  simpleWriteOnFrameVisibleCount,
  textGraphemes,
} from "./previewTextRevealHelpers.js";
import {
  requiredMotionContract,
  resolveMotionFrame,
} from "./previewMotionHelpers.js";
import { componentVariantConfig } from "./componentPreviewDefaults.js";
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
  const composer = composerState(resolvedMessages, screenFrame, timing);
  const conversationType = requiredString(
    preview,
    "conversationType",
    "module.conversation.input.conversationType",
  );
  if (conversationType !== "individual" && conversationType !== "group") {
    throw new Error(`Unsupported Conversation type ${conversationType}`);
  }
  const visible = visibleMessages(resolvedMessages, screenFrame, timing)
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
        "module.conversation.messageViewportMotion",
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
    && requiredBoolean(conversation, "showKeyboard", "module.conversation.showKeyboard");
  const textInputVisible = composer.textInputVisible
    && requiredBoolean(conversation, "showTextInputBar", "module.conversation.showTextInputBar");
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
  const timeline = new RuntimeOwnerTimeline(preview, preview, animation, themeTokens);
  preview.headerSubtitle = resolveParameterAnimation(
    animation,
    "headerSubtitle",
    "",
    timeline.localFrame("headerSubtitle", "", screenFrame),
    preview.headerSubtitle,
  ).value;

  const messages = requiredObjectArray(preview, "messages", "module.conversation runtime");
  preview.messages = messages.map((value, index) => {
    const message = { ...value };
    const targetId = requiredString(
      message,
      "id",
      `module.conversation.messages[${index}]`,
    );
    const direction = requiredString(
      message,
      "direction",
      `module.conversation.messages[${index}]`,
    );
    if (direction !== "incoming" && direction !== "outgoing" && direction !== "system") {
      throw new Error(
        `module.conversation.messages[${index}] has unsupported direction '${direction}'`,
      );
    }
    const resolve = (fieldId: string, baseValue: unknown) =>
      resolveParameterAnimation(
        animation,
        fieldId,
        targetId,
        timeline.localFrame(fieldId, targetId, screenFrame),
        baseValue,
      );

    const resolvedText = resolve("text", message.text);
    message.text = resolvedText.value;
    message.timelineStartFrame = timeline.itemStartFrame(targetId);
    message.timelineEndFrame = timeline.itemEndFrame(targetId);
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
      Math.max(0, screenFrame - textOriginFrame),
      optionalNumber(message, "writeOnDurationFrames", 0),
      `${targetId}:${optionalString(message, "text")}`,
    );
    const composerElapsedFrame = Math.max(0, screenFrame - textOriginFrame);
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
        timeline.localFrame("isPlaying", targetId, screenFrame) - playing.sourceKeyframeFrame,
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
      const ownerFrame = timeline.localFrame("fullScreen", targetId, screenFrame);
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
  "actorIdentityVisible" | "playbackTimeSeconds"
> & {
  composerWriteOnDurationFrames: number;
  composerWriteOnFrame: number;
  timelineStartFrame: number;
  timelineRevealAtFrame: number;
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
  const bubbleRevealMode = optionalString(preview, "bubbleRevealMode")
    || optionalString(conversation, "bubbleRevealMode");
  return {
    bubbleRevealMode: bubbleRevealMode === "afterWriteOn" ? "afterWriteOn" : "duringWriteOn",
    incomingRevealMode: incomingRevealMode === "writeOn" || incomingRevealMode === "typingIndicator"
      ? incomingRevealMode
      : "instant",
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
    const path = `module.conversation.messages[${index}]`;
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
      timelineRevealAtFrame: Math.max(
        0,
        Math.floor(optionalNumber(message, "timelineRevealAtFrame", 0)),
      ),
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
) {
  return messages.flatMap((message) => {
    const startFrame = message.timelineStartFrame;
    const isSystemMessage = message.state === "system";
    const isOutgoingMessage = message.state === "outgoing";
    const isIncomingMessage = message.state === "incoming";
    const effectiveWriteOnFrames = isSystemMessage ? 0 : message.writeOnDurationFrames;
    const revealEndFrame = startFrame + effectiveWriteOnFrames;
    const revealAfterWriteOn = isOutgoingMessage && timing.bubbleRevealMode === "afterWriteOn";
    const visibleAt = revealAfterWriteOn ? message.timelineRevealAtFrame : startFrame;
    if (frame < visibleAt) return [];
    const incomingTyping = isIncomingMessage
      && timing.incomingRevealMode === "typingIndicator"
      && frame < revealEndFrame;
    const incomingWriteOn = isIncomingMessage
      && timing.incomingRevealMode === "writeOn"
      && effectiveWriteOnFrames > 0
      && frame < revealEndFrame;
    const messageIsWriting = frame < revealEndFrame
      && effectiveWriteOnFrames > 0
      && (isOutgoingMessage || incomingWriteOn || incomingTyping);
    return [{
      ...message,
      visibleAtFrame: visibleAt,
      text: incomingTyping ? timing.typingIndicatorText : message.text,
      mediaType: messageIsWriting ? "none" as const : message.mediaType,
      mediaSource: messageIsWriting ? "" : message.mediaSource,
      isTypingIndicator: incomingTyping,
      writeOnTrigger: (isOutgoingMessage || incomingWriteOn)
        && !revealAfterWriteOn
        && effectiveWriteOnFrames > 0,
      writeOnDurationFrames: effectiveWriteOnFrames,
    }];
  });
}

function composerState(
  messages: ResolvedConversationMessage[],
  frame: number,
  timing: ConversationTimingContract,
) {
  for (const message of messages) {
    const startFrame = message.timelineStartFrame;
    const effectiveWriteOnFrames = message.state === "system"
      ? 0
      : message.composerWriteOnDurationFrames;
    const endFrame = startFrame + effectiveWriteOnFrames;
    const holdEndFrame = message.timelineRevealAtFrame;
    const composerVisible = message.state === "outgoing"
      && frame >= startFrame
      && frame < holdEndFrame;
    if (composerVisible) {
      const graphemes = textGraphemes(message.text);
      const writeOnInProgress = frame < endFrame;
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
  const config = componentVariantConfig(
    componentBaseConfigs,
    "textInputBar",
    requiredString(
      conversation,
      "textInputBarVariant",
      "module.conversation.textInputBarVariant",
    ),
  );
  const resolved = applyRuntimeInputForwarding({
    ...payload,
    kind: "componentClass",
    componentType: "textInputBar",
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify({
      ...forwardedRuntimeInputPatch(
        config,
        "forwarded.component.textInput.textBox.inputs.sampleText",
        text,
      ),
      availableWidth: payload.previewFrame.screenWidth / renderScale(payload),
    }),
  });
  return parseObject(resolved.configJson);
}
