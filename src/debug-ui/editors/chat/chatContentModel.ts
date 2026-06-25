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

export interface ChatParticipantOption {
  value: string;
  label: string;
}

export function defaultGroupValue(groupKey: string): JsonValue {
  return groupKey === "messages" || groupKey === "participants" ? [] : {};
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
    if (groupKey === "participants") {
      const name = typeof value.displayName === "string" ? value.displayName : "";
      const role = typeof value.role === "string" ? value.role : "";
      const actor = typeof value.actorId === "string" ? value.actorId : "";
      return truncateContentSummary(
        [name, role, actor ? `actor ${actor}` : ""].filter(Boolean).join(" · "),
      );
    }
    if (groupKey === "messages") {
      const text = typeof value.text === "string" ? value.text : "";
      const direction = value.type === "system" ? "sistema" : "mensaje";
      const start = typeof value.startFrame === "number" ? value.startFrame : null;
      const duration =
        typeof value.enterDurationFrames === "number"
          ? value.enterDurationFrames
          : null;
      const timing =
        start !== null && duration !== null ? `${start}–${start + duration}f` : "";
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

export function defaultParticipantItem(index: number): Record<string, JsonValue> {
  return {
    id: `participant_${index + 1}`,
    displayName: "",
    actorId: "",
    role: "participant",
  };
}

export function defaultMessageItem(
  index: number,
  senderParticipantId = "",
): Record<string, JsonValue> {
  return {
    id: `message_${String(index + 1).padStart(3, "0")}`,
    senderParticipantId,
    direction: "incoming",
    type: "text",
    text: "",
    showBubbleBackground: true,
    textScale: 1,
    media: {
      type: "none",
    },
    startFrame: 0,
    enterDurationFrames: 10,
    textReveal: {
      mode: "simple_write_on",
      startFrame: 0,
      durationFrames: 30,
    },
  };
}

export function messageWithDirection(
  message: Record<string, JsonValue>,
  nextDirection: string,
  ownerParticipantId: string,
  receivedParticipantId: string,
  currentSenderId: string,
): Record<string, JsonValue> {
  if (nextDirection === "system") {
    return {
      ...message,
      direction: "system",
      type: "system",
      senderParticipantId: ownerParticipantId || currentSenderId,
    };
  }
  if (nextDirection === "sent") {
    return {
      ...message,
      direction: "outgoing",
      type: "text",
      senderParticipantId: ownerParticipantId || currentSenderId,
    };
  }
  return {
    ...message,
    direction: "incoming",
    type: "text",
    senderParticipantId: receivedParticipantId || currentSenderId,
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
      filePath: String(media.filePath ?? ""),
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
      startFrame: Number(textReveal.startFrame ?? message.startFrame ?? 0),
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
  const participants = contentRoot.participants;
  if (group === "header") {
    const headerValue = contentRoot.header as JsonValue;
    if (!isJsonObject(headerValue) || !Array.isArray(participants)) {
      return false;
    }
    const header = headerValue;
    const participant = (participants as JsonValue[])
      .filter(isJsonObject)
      .find((item) => item.id === header.avatarParticipantId);
    const inheritedName = participant
      ? String(
          participant.displayName ??
            actors.find((item) => item.id === participant.actorId)?.display_name ??
            "",
        )
      : "";
    return Boolean(inheritedName) && String(header.title ?? "") !== inheritedName;
  }
  if (group !== "participants") return false;
  if (!Array.isArray(participants)) return false;
  return participants.some((participant) => {
    if (!isJsonObject(participant)) return false;
    const actor = actors.find((item) => item.id === participant.actorId);
    const inheritedName = String(actor?.display_name ?? "");
    return (
      Boolean(inheritedName) &&
      String(participant.displayName ?? "") !== inheritedName
    );
  });
}

export function mediaNumberFieldsForMessage(
  message: Record<string, JsonValue>,
): ChatMediaNumberField[] {
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

export function participantsFromContentRoot(
  root: Record<string, unknown>,
): Array<Record<string, JsonValue>> {
  return Array.isArray(root.participants)
    ? root.participants.filter(isJsonObject)
    : [];
}

export function participantById(
  participants: Array<Record<string, JsonValue>>,
  participantId: unknown,
) {
  return participants.find((participant) => participant.id === participantId);
}

export function participantDisplayName(
  participant: Record<string, JsonValue> | undefined,
  actorDisplayName: (actorId: unknown) => string,
) {
  if (!participant) return "";
  if (typeof participant.displayName === "string" && participant.displayName) {
    return participant.displayName;
  }
  return actorDisplayName(participant.actorId);
}

export function ownerParticipant(participants: Array<Record<string, JsonValue>>) {
  return (
    participants.find((participant) => participant.role === "owner") ??
    participants[0]
  );
}

export function firstReceivedParticipant(
  participants: Array<Record<string, JsonValue>>,
) {
  return (
    participants.find((participant) => participant.role !== "owner") ??
    ownerParticipant(participants)
  );
}

export function participantOptions(
  participants: Array<Record<string, JsonValue>>,
  displayNameForParticipant: (participant: Record<string, JsonValue>) => string,
): ChatParticipantOption[] {
  return participants.map((participant, index) => {
    const value = String(participant.id ?? `participant_${index + 1}`);
    const label = displayNameForParticipant(participant) || value;
    return { value, label };
  });
}
