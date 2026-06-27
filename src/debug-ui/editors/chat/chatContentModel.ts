import {
  isJsonObject,
  type JsonPath,
  type JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";

export interface ChatMediaNumberField {
  label: string;
  path: JsonPath;
  value: number;
  fallback: number;
}

export interface ChatActorOption {
  value: string;
  label: string;
}

export function defaultGroupValue(groupKey: string): JsonValue {
  return groupKey === "messages" ? [] : {};
}

export function isPrimitiveContentValue(value: JsonValue) {
  return (
    value === null ||
    typeof value === "string" ||
    typeof value === "number" ||
    typeof value === "boolean"
  );
}

export function truncateContentSummary(value: string) {
  const normalized = value.replace(/\s+/g, " ").trim();
  return normalized.length > 96 ? `${normalized.slice(0, 93)}…` : normalized;
}

export function contentSummary(value: JsonValue, groupKey?: string): string {
  if (isJsonObject(value)) {
    if (groupKey === "messages") {
      const text = typeof value.text === "string" ? value.text : "";
      const direction = value.type === "system" ? "sistema" : "mensaje";
      const delay =
        typeof value.delayAfterPreviousFrames === "number"
          ? value.delayAfterPreviousFrames
          : typeof value.startFrame === "number"
            ? value.startFrame
            : null;
      const duration =
        typeof value.enterDurationFrames === "number"
          ? value.enterDurationFrames
          : null;
      const timing =
        delay !== null && duration !== null
          ? `+${delay}f · ${duration}f enter`
          : delay !== null
            ? `+${delay}f`
            : "";
      const mediaSummary: string = value.media
        ? contentSummary(value.media as JsonValue)
        : "";
      return truncateContentSummary(
        [direction, text || mediaSummary, timing].filter(Boolean).join(" · "),
      );
    }
    for (const key of ["displayName", "text", "title", "name", "role", "type", "id"]) {
      const candidate = value[key];
      if (typeof candidate === "string" && candidate.trim()) {
        return truncateContentSummary(candidate);
      }
    }
    return `${Object.keys(value).length} fields`;
  }
  if (Array.isArray(value)) return `${value.length} items`;
  if (value === null) return "Empty";
  return String(value);
}

export function messageDirectionFromSenderRole(
  message: Record<string, JsonValue>,
  senderRole?: unknown,
) {
  if (message.direction === "system" || message.type === "system") {
    return "system";
  }
  if (message.direction === "outgoing") return "sent";
  if (message.direction === "incoming") return "received";
  return senderRole === "owner" ? "sent" : "received";
}

export function defaultMessageItem(
  index: number,
  actorId = "",
): Record<string, JsonValue> {
  return {
    id: `message_${String(index + 1).padStart(3, "0")}`,
    actorId,
    direction: "incoming",
    type: "text",
    text: "",
    showBubbleBackground: true,
    textScale: 1,
    status: {
      text: "",
      deliveryStatus: "none",
    },
    media: {
      type: "none",
    },
    delayAfterPreviousFrames: 0,
    enterDurationFrames: 10,
    textReveal: {
      mode: "simple_write_on",
      durationFrames: 30,
    },
  };
}

export function messageWithDirection(
  message: Record<string, JsonValue>,
  nextDirection: string,
  ownerActorId: string,
  receivedActorId: string,
  currentActorId: string,
): Record<string, JsonValue> {
  if (nextDirection === "system") {
    const { actorId: _actorId, ...messageWithoutActor } = message;
    return {
      ...messageWithoutActor,
      direction: "system",
      type: "system",
    };
  }
  if (nextDirection === "sent") {
    return {
      ...message,
      direction: "outgoing",
      type: "text",
      actorId: ownerActorId || currentActorId,
    };
  }
  return {
    ...message,
    direction: "incoming",
    type: "text",
    actorId: receivedActorId || currentActorId,
  };
}

export function messageWithMediaType(
  message: Record<string, JsonValue>,
  nextType: string,
): Record<string, JsonValue> {
  const { mediaAssetId: _mediaAssetId, ...messageWithoutAsset } = message;
  if (nextType === "none") {
    return {
      ...messageWithoutAsset,
      media: { type: "none" },
    };
  }

  const media = isJsonObject(message.media) ? message.media : {};
  const mediaWindow = isJsonObject(media.window) ? media.window : {};
  const mediaTransform = isJsonObject(media.transform) ? media.transform : {};

  return {
    ...messageWithoutAsset,
    type: messageWithoutAsset.type === "system" ? "text" : messageWithoutAsset.type,
    media: {
      type: nextType,
      ...(nextType === "audio"
        ? {}
        : { filePath: String(media.filePath ?? "") }),
      ...(nextType === "audio"
        ? {
            durationSeconds: Number(media.durationSeconds ?? 8),
            playStartFrame: Number(media.playStartFrame ?? 0),
          }
        : {}),
      ...(nextType === "video"
        ? {
            playMode: media.playMode === "loop" ? "loop" : "once",
            playStartFrame: Number(media.playStartFrame ?? 0),
          }
        : {}),
      ...(nextType === "audio"
        ? {}
        : {
            window: {
              width: Number(mediaWindow.width ?? 360),
              height: Number(mediaWindow.height ?? 240),
              offsetX: Number(mediaWindow.offsetX ?? 0),
              offsetY: Number(mediaWindow.offsetY ?? 0),
            },
            transform: {
              scale: Number(mediaTransform.scale ?? 1),
              translateX: Number(mediaTransform.translateX ?? 0),
              translateY: Number(mediaTransform.translateY ?? 0),
              rotationDegrees: Number(mediaTransform.rotationDegrees ?? 0),
            },
          }),
    },
  };
}

export function messageWithMediaPath(
  message: Record<string, JsonValue>,
  mediaType: string,
  filePath: string,
): Record<string, JsonValue> {
  const { mediaAssetId: _mediaAssetId, ...messageWithoutAsset } = message;
  const media = isJsonObject(message.media) ? message.media : {};
  return {
    ...messageWithoutAsset,
    media: {
      ...media,
      type: mediaType === "none" ? "image" : mediaType,
      filePath,
    },
  };
}

export function messageWithTextRevealMode(
  message: Record<string, JsonValue>,
  mode: string,
): Record<string, JsonValue> {
  const textReveal = isJsonObject(message.textReveal) ? message.textReveal : {};
  return {
    ...message,
    textReveal: {
      durationFrames: Number(textReveal.durationFrames ?? 30),
      ...textReveal,
      mode,
    },
  };
}

export function chatContentGroupHasWarning({
  group,
  contentRoot,
  actors,
}: {
  group: string;
  contentRoot: Record<string, unknown>;
  actors: Record<string, unknown>[];
}) {
  if (group === "header") {
    const headerValue = contentRoot.header as JsonValue;
    if (!isJsonObject(headerValue)) {
      return false;
    }
    const header = headerValue;
    const actor = actors.find((item) => item.id === header.actorId);
    const inheritedName = String(actor?.display_name ?? "");
    return Boolean(inheritedName) && String(header.title ?? "") !== inheritedName;
  }
  return false;
}

export function mediaNumberFieldsForMessage(
  message: Record<string, JsonValue>,
): ChatMediaNumberField[] {
  const media = isJsonObject(message.media) ? message.media : {};
  if (media.type === "audio") {
    return [];
  }
  return (
    [
      ["Container width", ["media", "window", "width"], 360],
      ["Container height", ["media", "window", "height"], 240],
      ["Crop X offset", ["media", "window", "offsetX"], 0],
      ["Crop Y offset", ["media", "window", "offsetY"], 0],
      ["Media scale", ["media", "transform", "scale"], 1],
      ["Media X offset", ["media", "transform", "translateX"], 0],
      ["Media Y offset", ["media", "transform", "translateY"], 0],
    ] as Array<[string, JsonPath, number]>
  ).map(([label, path, fallback]) => ({
    label,
    path,
    fallback,
    value: Number(
      path.reduce<JsonValue>(
        (current, part) =>
          isJsonObject(current) && typeof part === "string"
            ? current[part] ?? null
            : null,
        message,
      ) ?? fallback,
    ),
  }));
}

export function actorOptions(
  actors: Record<string, unknown>[],
): ChatActorOption[] {
  return actors.map((actor, index) => {
    const value = String(actor.id ?? `actor_${index + 1}`);
    const label = String(actor.display_name ?? actor.name ?? value);
    return { value, label };
  });
}
