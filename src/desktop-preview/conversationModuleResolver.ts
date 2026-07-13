import {
  asRecord,
  optionalNumber,
  optionalString,
  parseObject,
} from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";

type JsonRecord = Record<string, unknown>;

export function resolveConversationModuleFrame(payload: DesignPreviewPayload): JsonRecord {
  const preview = parseObject(payload.designPreviewJson);
  const instance = parseObject(payload.instanceJson ?? "{}");
  const animation = asRecord(instance.animation);
  const context = asRecord(instance.context);
  const screenFrame = Math.max(0, Math.floor(optionalNumber(context, "localFrame", 0)));
  preview.headerSubtitle = resolveParameterAnimation(
    animation,
    "headerSubtitle",
    "",
    screenFrame,
    preview.headerSubtitle,
  ).value;

  if (!Array.isArray(preview.messages)) return preview;
  let cursor = 0;
  preview.messages = preview.messages.map((value) => {
    const message = { ...asRecord(value) };
    const targetId = optionalString(message, "id");
    const direction = optionalString(message, "direction") || "incoming";
    const delay = Math.max(0, Math.floor(optionalNumber(message, "delayAfterPreviousFrames", 0)));
    const writeOn = direction === "system"
      ? 0
      : Math.max(0, Math.floor(optionalNumber(message, "writeOnDurationFrames", 0)));
    const hold = direction === "outgoing"
      ? Math.max(0, Math.floor(optionalNumber(message, "postWriteOnHoldFrames", 0)))
      : 0;
    const startFrame = cursor + delay;
    cursor = startFrame + writeOn + hold;
    const localFrame = screenFrame - startFrame;
    const resolve = (fieldId: string, baseValue: unknown) =>
      resolveParameterAnimation(animation, fieldId, targetId, localFrame, baseValue);

    message.text = resolve("text", message.text).value;
    message.statusVisible = resolve("statusVisible", message.statusVisible).value;
    message.statusState = resolve("status", message.statusState).value;
    message.statusText = resolve("statusText", message.statusText).value;
    const playing = resolve("isPlaying", message.isPlaying);
    message.isPlaying = playing.value;
    if (playing.animated && playing.value === true && playing.sourceKeyframeFrame !== undefined) {
      const elapsed = Math.max(0, localFrame - playing.sourceKeyframeFrame);
      const duration = Math.max(1, Math.floor(optionalNumber(message, "playDurationFrames", 1)));
      message.isPlaying = elapsed < duration;
      message.playbackFrame = Math.min(elapsed, duration);
    }
    message.isFullScreen = resolve("fullScreen", message.isFullScreen).value;
    return message;
  });
  return preview;
}
