import {
  optionalBoolean,
  optionalNumber,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredPossiblyEmptyString,
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
import { resolvedTextInputBarRuntimeConfig } from "./textInputBarRuntimeConfig.js";
import { renderScale } from "./previewGeometryHelpers.js";
import { resolvedRuntimeRecordReference } from "./runtimeRecordReferenceCatalog.js";
import {
  requiredReflowTiming,
  resolveReflowProgress,
} from "./previewReflowHelpers.js";

export function resolveConversationModule(
  payload: DesignPreviewPayload,
): ConversationModuleContract {
  const preview = resolveConversationModuleFrame(payload);
  const config = parseObject(payload.configJson);
  const conversation = requiredRecord(config, "conversation", "module config");
  const screenFrame = rootScreenFrame(payload);
  const resolvedMessages = conversationMessages(preview);
  const timing = conversationTiming(preview);
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
  const decorateMessage = (
    message: UndecoratedConversationMessage,
  ): ConversationMessageContract => ({
    ...message,
    actorIdentityVisible: conversationMessageActorIdentityVisible(
      conversationType,
      message.state,
    ),
    playbackTimeSeconds: messagePlaybackTimeSeconds(message, payload.frameRate),
  });
  const visible = visibleMessages(
    resolvedMessages,
    screenFrame,
    timing,
    payload,
    messageMotion,
    automaticEndFrame,
    !showKeyboard && !showTextInputBar,
  ).map(decorateMessage);
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
  const reflowTiming = requiredReflowTiming(
    requiredRecord(
      conversation,
      "messageReflowTiming",
      "module.core.chat.messageReflowTiming",
    ),
    "module.core.chat.messageReflowTiming",
  );
  const messageReflow = resolveMessageReflow(
    resolvedMessages,
    screenFrame,
    timing,
    payload,
    messageMotion,
    automaticEndFrame,
    !showKeyboard && !showTextInputBar,
    reflowTiming,
    decorateMessage,
  );
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
    ...(messageReflow ? { messageReflow } : {}),
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
  const messages = requiredObjectArray(preview, "messages", "module.conversation runtime");
  messages.forEach(validateConversationMessageRuntime);
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
  const resolvedActorId = resolveParameterAnimation(
    animation,
    "actor",
    "",
    timeline.temporalLocalFrame("actor", "", screenFrame),
    preview.actorId,
  );
  if (resolvedActorId.animated) {
    if (typeof resolvedActorId.value !== "string" || !resolvedActorId.value.trim()) {
      throw new Error("module.core.chat.input.actor animation must resolve a non-empty Actor id");
    }
    preview.actorId = resolvedActorId.value;
    preview.actor = resolvedRuntimeRecordReference(
      payload,
      "actors",
      resolvedActorId.value,
      "module.core.chat.input.actor",
    );
  }

  preview.messages = messages.map((value, index) => {
    const message = { ...value };
    const targetId = requiredString(
      message,
      "id",
      `module.core.chat.messages[${index}]`,
    );
    const authoredDirection = requiredString(
      message,
      "direction",
      `module.core.chat.messages[${index}]`,
    );
    const resolvedDirection = resolveParameterAnimation(
      animation,
      "direction",
      targetId,
      timeline.temporalLocalFrame("direction", targetId, screenFrame),
      authoredDirection,
    ).value;
    if (typeof resolvedDirection !== "string") {
      throw new Error(
        `module.core.chat.messages[${index}] direction animation must resolve a string`,
      );
    }
    const direction = resolvedDirection;
    if (direction !== "incoming" && direction !== "outgoing" && direction !== "system") {
      throw new Error(
        `module.core.chat.messages[${index}] has unsupported direction '${direction}'`,
      );
    }
    message.direction = direction;
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
      ? Math.max(0, requiredNumber(
          message,
          "postWriteOnHoldFrames",
          `module.core.chat.messages[${index}].postWriteOnHoldFrames`,
        ))
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
    const messageText = requiredPossiblyEmptyString(
      message,
      "text",
      `module.core.chat.messages[${index}].text`,
    );
    message.writeOnFrame = naturalWriteOnFrame(
      messageText,
      message.writeOnTiming,
      Math.max(0, timeline.temporalLocalFrame(
        "text",
        targetId,
        screenFrame,
        message.hasExplicitPresenceEnd ? presenceEndFrame : undefined,
      )),
      optionalNumber(message, "writeOnDurationFrames", 0),
      `${targetId}:${messageText}`,
    );
    message.keepCursorAfterWrite = resolve(
      "keepCursorAfterWrite",
      requiredBoolean(
        message,
        "keepCursorAfterWrite",
        `module.core.chat.messages[${index}].keepCursorAfterWrite`,
      ),
    ).value;
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
      messageText,
      message.writeOnTiming,
      composerElapsedFrame,
      optionalNumber(message, "composerWriteOnDurationFrames", 0),
      `${targetId}:${messageText}`,
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
      const duration = Math.max(1, Math.floor(requiredNumber(
        message,
        "playDurationFrames",
        `module.core.chat.messages[${index}].playDurationFrames`,
      )));
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

type UndecoratedConversationMessage = ResolvedConversationMessage & {
  presenceMotion: ComponentMotionContract;
  presenceMotionKind?: "enter" | "exit";
  presenceMotionFrame?: ConversationMessageContract["presenceMotionFrame"];
};

function conversationTiming(preview: JsonRecord): ConversationTimingContract {
  const incomingRevealMode = requiredString(
    preview,
    "incomingRevealMode",
    "module.core.chat.input.incomingRevealMode",
  );
  if (incomingRevealMode !== "writeOn"
    && incomingRevealMode !== "typingIndicator") {
    throw new Error(
      `Unsupported Conversation incoming reveal mode ${incomingRevealMode}`,
    );
  }
  const bubbleRevealMode = requiredString(
    preview,
    "bubbleRevealMode",
    "module.core.chat.input.bubbleRevealMode",
  );
  if (bubbleRevealMode !== "afterWriteOn" && bubbleRevealMode !== "duringWriteOn") {
    throw new Error(`Unsupported Conversation bubble reveal mode ${bubbleRevealMode}`);
  }
  return {
    bubbleRevealMode,
    incomingRevealMode: incomingRevealMode as ConversationIncomingRevealMode,
    textInputVisible: requiredBoolean(
      preview,
      "textInputVisible",
      "module.core.chat.input.textInputVisible",
    ),
    keyboardVisible: requiredBoolean(
      preview,
      "keyboardVisible",
      "module.core.chat.input.keyboardVisible",
    ),
    typingIndicatorText: requiredPossiblyEmptyString(
      preview,
      "typingIndicatorText",
      "module.core.chat.input.typingIndicatorText",
    ),
    typingIndicatorSizeToken: requiredString(
      preview,
      "typingIndicatorSizeToken",
      "module.core.chat.input.typingIndicatorSizeToken",
    ),
    typingIndicatorAnimation: typingIndicatorAnimation(
      requiredString(
        preview,
        "typingIndicatorAnimation",
        "module.core.chat.input.typingIndicatorAnimation",
      ),
    ),
  };
}

function typingIndicatorAnimation(
  value: string,
): ConversationTypingIndicatorAnimation {
  if (value === "none" || value === "pulsating" || value === "wave") return value;
  throw new Error(`Unsupported Conversation typing indicator animation ${value}`);
}

function conversationMessages(preview: JsonRecord): ResolvedConversationMessage[] {
  const messages = requiredObjectArray(preview, "messages", "module.conversation runtime");
  return messages.map((message, index) => {
    const path = `module.core.chat.messages[${index}]`;
    return {
      id: requiredString(message, "id", `${path}.id`),
      actor: requiredRecord(message, "actor", path),
      state: requiredString(message, "direction", path),
      text: requiredPossiblyEmptyString(message, "text", `${path}.text`),
      statusState: conversationStatusState(requiredString(
        message,
        "statusState",
        `${path}.statusState`,
      )),
      statusText: requiredPossiblyEmptyString(message, "statusText", `${path}.statusText`),
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
      keepCursorAfterWrite: requiredBoolean(
        message,
        "keepCursorAfterWrite",
        `${path}.keepCursorAfterWrite`,
      ),
      statusVisible: requiredBoolean(message, "statusVisible", `${path}.statusVisible`),
      visibleAtFrame: 0,
      mediaType: messageMediaType(message, path),
      mediaSource: requiredPossiblyEmptyString(message, "mediaSource", `${path}.mediaSource`),
      viewportSize: requiredString(message, "viewportSize", `${path}.viewportSize`),
      mediaScale: requiredNumber(message, "mediaScale", `${path}.mediaScale`),
      mediaOffset: requiredString(message, "mediaOffset", `${path}.mediaOffset`),
      isPlaying: requiredBoolean(message, "isPlaying", `${path}.isPlaying`),
      playbackMode: playbackMode(requiredString(
        message,
        "playbackMode",
        `${path}.playbackMode`,
      )),
      playbackFrame: Math.max(0, Math.floor(optionalNumber(message, "playbackFrame", 0))),
      durationSeconds: Math.max(
        1,
        requiredNumber(message, "durationSeconds", `${path}.durationSeconds`),
      ),
      isFullScreen: requiredBoolean(message, "isFullScreen", `${path}.isFullScreen`),
      fullScreenTransition: requiredBoolean(
        message,
        "fullScreenTransition",
        `${path}.fullScreenTransition`,
      ),
      fullScreenMotionElapsedMs: optionalNumber(message, "motionElapsedMs", 0),
      fullframeOrientation: conversationFullframeOrientation(requiredString(
        message,
        "fullframeOrientation",
        `${path}.fullframeOrientation`,
      )),
      controlsElapsedMs: requiredNumber(
        message,
        "controlsElapsedMs",
        `${path}.controlsElapsedMs`,
      ),
      isTypingIndicator: false,
      currentTimeSeconds: requiredNumber(
        message,
        "currentTimeSeconds",
        `${path}.currentTimeSeconds`,
      ),
    };
  });
}

function resolveMessageReflow(
  messages: ResolvedConversationMessage[],
  frame: number,
  timing: ConversationTimingContract,
  payload: DesignPreviewPayload,
  messageMotion: ComponentMotionContract,
  automaticEndFrame: number,
  writesInBubble: boolean,
  reflowTiming: { durationMs: number; easing: string; intensity: number },
  decorateMessage: (
    message: UndecoratedConversationMessage,
  ) => ConversationMessageContract,
) {
  const durationFrames = reflowTiming.durationMs / 1000
    * Math.max(1, payload.frameRate);
  if (durationFrames <= 0) return undefined;
  const events = messages.flatMap((message) => {
    const visibleAt = messageVisibleAtFrame(message, timing, writesInBubble);
    const appearance = visibleAt > 0 ? [visibleAt] : [];
    const disappearance = message.hasExplicitPresenceEnd
      && message.presenceEndFrame < automaticEndFrame
      ? [message.presenceEndFrame]
      : [];
    return [...appearance, ...disappearance];
  }).filter((eventFrame) => eventFrame <= frame && frame < eventFrame + durationFrames)
    .sort((a, b) => b - a);
  const startFrame = events[0];
  if (startFrame === undefined) return undefined;
  const fromMessages = visibleMessages(
    messages,
    Math.max(0, startFrame - 1),
    timing,
    payload,
    messageMotion,
    automaticEndFrame,
    writesInBubble,
  ).map(decorateMessage);
  return {
    progress: resolveReflowProgress(
      reflowTiming,
      (frame - startFrame + 1) / Math.max(1, payload.frameRate) * 1000,
    ),
    fromMessages,
  };
}

function messageVisibleAtFrame(
  message: ResolvedConversationMessage,
  timing: ConversationTimingContract,
  writesInBubble: boolean,
) {
  const revealAfterWriteOn = message.state === "outgoing"
    && timing.bubbleRevealMode === "afterWriteOn"
    && !writesInBubble;
  return revealAfterWriteOn ? message.timelineRevealAtFrame : message.timelineStartFrame;
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
    const isSystemMessage = message.state === "system";
    const isOutgoingMessage = message.state === "outgoing";
    const isIncomingMessage = message.state === "incoming";
    const effectiveWriteOnFrames = isSystemMessage ? 0 : message.writeOnDurationFrames;
    const actionFrame = message.timelineTemporalFrame;
    const revealAfterWriteOn = isOutgoingMessage
      && timing.bubbleRevealMode === "afterWriteOn"
      && !writesInBubble;
    const visibleAt = messageVisibleAtFrame(message, timing, writesInBubble);
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

function messageMediaType(
  message: JsonRecord,
  path: string,
): ResolvedConversationMessage["mediaType"] {
  const mediaType = requiredString(message, "mediaType", `${path}.mediaType`);
  return mediaType === "image" || mediaType === "video" || mediaType === "audio"
    ? mediaType
    : mediaType === "none"
      ? "none"
      : unsupportedConversationValue("media type", mediaType);
}

function playbackMode(value: string): ResolvedConversationMessage["playbackMode"] {
  if (value === "once" || value === "loop") return value;
  return unsupportedConversationValue("playback mode", value);
}

function conversationStatusState(value: string) {
  if (value === "none" || value === "sent" || value === "delivered" || value === "read") {
    return value;
  }
  return unsupportedConversationValue("status state", value);
}

function conversationFullframeOrientation(value: string) {
  if (value === "portrait" || value === "landscape") return value;
  return unsupportedConversationValue("fullframe orientation", value);
}

function unsupportedConversationValue(label: string, value: string): never {
  throw new Error(`Unsupported Conversation ${label} ${value}`);
}

function validateConversationMessageRuntime(message: JsonRecord, index: number) {
  const path = `module.core.chat.messages[${index}]`;
  requiredString(message, "actorId", `${path}.actorId`);
  requiredRecord(message, "actor", `${path}.actor`);
  requiredString(message, "direction", `${path}.direction`);
  requiredPossiblyEmptyString(message, "text", `${path}.text`);
  requiredNumber(message, "delayAfterPreviousFrames", `${path}.delayAfterPreviousFrames`);
  requiredRecord(message, "writeOnTiming", `${path}.writeOnTiming`);
  requiredNumber(message, "postWriteOnHoldFrames", `${path}.postWriteOnHoldFrames`);
  requiredBoolean(message, "keepCursorAfterWrite", `${path}.keepCursorAfterWrite`);
  requiredBoolean(message, "statusVisible", `${path}.statusVisible`);
  conversationStatusState(requiredString(message, "statusState", `${path}.statusState`));
  requiredPossiblyEmptyString(message, "statusText", `${path}.statusText`);
  messageMediaType(message, path);
  requiredPossiblyEmptyString(message, "mediaSource", `${path}.mediaSource`);
  requiredString(message, "viewportSize", `${path}.viewportSize`);
  requiredNumber(message, "mediaScale", `${path}.mediaScale`);
  requiredString(message, "mediaOffset", `${path}.mediaOffset`);
  requiredBoolean(message, "isPlaying", `${path}.isPlaying`);
  requiredNumber(message, "currentTimeSeconds", `${path}.currentTimeSeconds`);
  requiredNumber(message, "durationSeconds", `${path}.durationSeconds`);
  playbackMode(requiredString(message, "playbackMode", `${path}.playbackMode`));
  requiredNumber(message, "playDurationFrames", `${path}.playDurationFrames`);
  requiredBoolean(message, "isFullScreen", `${path}.isFullScreen`);
  requiredBoolean(message, "fullScreenTransition", `${path}.fullScreenTransition`);
  conversationFullframeOrientation(requiredString(
    message,
    "fullframeOrientation",
    `${path}.fullframeOrientation`,
  ));
  requiredNumber(message, "controlsElapsedMs", `${path}.controlsElapsedMs`);
  requiredNumber(message, "visibleDurationFrames", `${path}.visibleDurationFrames`);
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
  return resolvedTextInputBarRuntimeConfig(
    payload,
    componentBaseConfigs,
    slot,
    text,
    payload.previewFrame.screenWidth / renderScale(payload),
    "module.core.chat.textInputBarSlot",
  );
}
