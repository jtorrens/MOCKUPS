import {
  asRecord,
  optionalNumber,
  optionalString,
  parseObject,
} from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { optionalObject } from "./previewJsonHelpers.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";
import { naturalWriteOnFrame } from "./behaviorTiming.js";

type JsonRecord = Record<string, unknown>;

export function resolveConversationModuleFrame(payload: DesignPreviewPayload): JsonRecord {
  const preview = parseObject(payload.designPreviewJson);
  const instance = parseObject(payload.instanceJson);
  const animation = optionalObject(instance, "animation", "Preview instance envelope");
  const context = optionalObject(instance, "context", "Preview instance envelope");
  const screenFrame = Math.max(0, Math.floor(optionalNumber(context, "localFrame", 0)));
  const themeTokens = parseObject(payload.themeTokensJson);
  const timeline = new RuntimeOwnerTimeline(preview, preview, animation, themeTokens);
  preview.headerSubtitle = resolveParameterAnimation(
    animation,
    "headerSubtitle",
    "",
    timeline.localFrame("headerSubtitle", "", screenFrame),
    preview.headerSubtitle,
  ).value;

  if (!Array.isArray(preview.messages)) return preview;
  preview.messages = preview.messages.map((value) => {
    const message = { ...asRecord(value) };
    const targetId = optionalString(message, "id");
    const direction = optionalString(message, "direction") || "incoming";
    const resolve = (fieldId: string, baseValue: unknown) =>
      resolveParameterAnimation(
        animation,
        fieldId,
        targetId,
        timeline.localFrame(fieldId, targetId, screenFrame),
        baseValue,
      );

    message.text = resolve("text", message.text).value;
    message.timelineStartFrame = timeline.itemStartFrame(targetId);
    message.timelineEndFrame = timeline.itemEndFrame(targetId);
    const textCompletionFrame = timeline.fieldCompletionFrame("text", targetId);
    const textOriginFrame = timeline.screenFrame("text", targetId, 0);
    const postHold = direction === "outgoing"
      ? Math.max(0, optionalNumber(message, "postWriteOnHoldFrames", 0))
      : 0;
    message.timelineRevealAtFrame = timeline.itemOwnerFrame(
      targetId,
      timeline.fieldCompletionLocal("text", targetId) + postHold,
    );
    message.writeOnDurationFrames = timeline.usesTrackCompletion("text", targetId)
      ? 0
      : Math.max(0, textCompletionFrame - textOriginFrame);
    message.writeOnFrame = naturalWriteOnFrame(
      optionalString(message, "text"),
      message.writeOnTiming,
      Math.max(0, screenFrame - textOriginFrame),
      optionalNumber(message, "writeOnDurationFrames", 0),
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
    message.isFullScreen = resolve("fullScreen", message.isFullScreen).value;
    return message;
  });
  return preview;
}
