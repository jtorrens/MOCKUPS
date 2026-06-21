import { z } from "zod";
import type { DomainRepository } from "../repository/types.js";
import {
  ChatModuleConfigSchema,
  ChatModuleDataSchema,
  ResolvedChatScreenPropsSchema,
  type Actor,
  type ChatParticipant,
  type Device,
  type DeviceState,
  type ResolvedChatScreenProps,
  type ScreenInstance,
  type Theme,
} from "../schemas/index.js";
import { requireRecord } from "./helpers.js";
import {
  resolveMessageBubble,
  type ResolvedChatParticipant,
} from "./resolveMessageBubble.js";

const CHAT_MODULE_ID = "core.chat";
const CHAT_MODULE_SCHEMA_VERSION = 1;

const DeviceMetricsSchema = z.object({
  viewport: z.object({
    x: z.number(),
    y: z.number(),
    width: z.number().positive(),
    height: z.number().positive(),
  }),
  safeArea: z.object({
    top: z.number().min(0),
    right: z.number().min(0),
    bottom: z.number().min(0),
    left: z.number().min(0),
  }),
  statusBar: z.object({ height: z.number().min(0) }),
  cornerRadius: z.number().min(0),
  pixelRatio: z.number().positive(),
  defaultScreenScale: z.number().positive(),
});

const DeviceStateValuesSchema = z.object({
  time: z.string().min(1),
  batteryLevel: z.number().min(0).max(1),
  batteryCharging: z.boolean(),
  signalBars: z.number().int().min(0),
  networkLabel: z.string(),
  wifiEnabled: z.boolean(),
  wifiIconState: z.string().min(1),
  orientation: z.enum(["portrait", "landscape"]),
  locked: z.boolean(),
});

const ChatThemeSchema = z.object({
  fonts: z.record(z.string(), z.unknown()),
  colors: z.record(z.string(), z.unknown()),
  statusBar: z.record(z.string(), z.unknown()),
  layout: z.record(z.string(), z.unknown()),
  header: z.record(z.string(), z.unknown()),
  messages: z.record(z.string(), z.unknown()),
  typography: z.record(z.string(), z.unknown()).optional(),
  chatBubbles: z.record(z.string(), z.unknown()),
  avatars: z.record(z.string(), z.unknown()),
  cursor: z.record(z.string(), z.unknown()),
  radii: z.object({ bubble: z.number().min(0) }),
});

const ThemeEnvelopeSchema = z.object({
  defaultMode: z.enum(["light", "dark"]).optional(),
  modes: z
    .object({
      light: z.record(z.string(), z.unknown()).optional(),
      dark: z.record(z.string(), z.unknown()).optional(),
    })
    .optional(),
});

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function mergeTokenObjects(
  base: Record<string, unknown>,
  override: Record<string, unknown>,
): Record<string, unknown> {
  const merged = { ...base };
  for (const [key, value] of Object.entries(override)) {
    merged[key] =
      isObject(merged[key]) && isObject(value)
        ? mergeTokenObjects(merged[key], value)
        : value;
  }
  return merged;
}

export function resolveThemeModeTokens(
  tokens: Record<string, unknown>,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  const envelope = ThemeEnvelopeSchema.parse(tokens);
  return envelope.modes?.[themeMode] ?? {};
}

export function resolveGlobalThemeTokens(
  theme: Theme,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  return mergeTokenObjects(
    theme.tokens_json,
    resolveThemeModeTokens(theme.tokens_json, themeMode),
  );
}

export function resolveModuleThemeTokens(
  tokens: Record<string, unknown>,
  themeMode: "light" | "dark",
): Record<string, unknown> {
  return mergeTokenObjects(tokens, resolveThemeModeTokens(tokens, themeMode));
}

function resolveParticipant(
  repository: DomainRepository,
  participant: ChatParticipant,
): ResolvedChatParticipant {
  const actor = participant.actorId
    ? requireRecord(
        repository.getActor(participant.actorId),
        "Actor",
        participant.actorId,
      )
    : undefined;
  const avatarAssetId = participant.avatarAssetId ?? actor?.avatar_asset_id;
  const avatar = avatarAssetId
    ? requireRecord(
        repository.getMediaAsset(avatarAssetId),
        "MediaAsset",
        avatarAssetId,
      )
    : undefined;
  const displayName = participant.displayName ?? actor?.display_name;
  if (!displayName) {
    throw new Error(`Chat participant ${participant.id} has no display name`);
  }

  return {
    participantId: participant.id,
    ...(actor ? { actorId: actor.id } : {}),
    displayName,
    ...(avatar ? { avatarUri: avatar.uri } : {}),
    role: participant.role,
  };
}

export interface ResolveChatScreenInput {
  repository: DomainRepository;
  screenInstance: ScreenInstance;
  ownerActor: Actor;
  device: Device;
  deviceState: DeviceState;
  theme: Theme;
  localFrame: number;
  fps: number;
}

export function resolveChatScreen({
  repository,
  screenInstance,
  ownerActor,
  device,
  deviceState,
  theme,
  localFrame,
  fps,
}: ResolveChatScreenInput): ResolvedChatScreenProps {
  if (screenInstance.module_id !== CHAT_MODULE_ID) {
    throw new Error(
      `Chat screen instance ${screenInstance.id} requires module_id ${CHAT_MODULE_ID}`,
    );
  }
  if (
    screenInstance.module_schema_version !== CHAT_MODULE_SCHEMA_VERSION
  ) {
    throw new Error(
      `Chat screen instance ${screenInstance.id} requires module schema version ${CHAT_MODULE_SCHEMA_VERSION}`,
    );
  }

  const moduleData = ChatModuleDataSchema.parse(
    screenInstance.module_data_json,
  );
  const moduleConfig = ChatModuleConfigSchema.parse(
    screenInstance.module_config_json,
  );
  if (moduleData.schemaVersion !== screenInstance.module_schema_version) {
    throw new Error(
      `Chat module data schemaVersion does not match screen instance ${screenInstance.id}`,
    );
  }

  const participants = new Map(
    moduleData.participants.map((participant) => {
      const resolved = resolveParticipant(repository, participant);
      return [resolved.participantId, resolved] as const;
    }),
  );
  const ownerParticipant = [...participants.values()].find(
    (participant) => participant.role === "owner",
  );
  if (!ownerParticipant) {
    throw new Error(`Chat screen instance ${screenInstance.id} has no owner`);
  }
  if (
    ownerParticipant.actorId &&
    ownerParticipant.actorId !== ownerActor.id
  ) {
    throw new Error(
      `Chat owner participant does not match screen owner ${ownerActor.id}`,
    );
  }

  const headerAvatarParticipant = moduleData.header.avatarParticipantId
    ? participants.get(moduleData.header.avatarParticipantId)
    : undefined;
  if (
    moduleData.header.avatarParticipantId &&
    !headerAvatarParticipant
  ) {
    throw new Error(
      `Chat header references missing participant ${moduleData.header.avatarParticipantId}`,
    );
  }

  const metrics = DeviceMetricsSchema.parse(device.metrics_json);
  const state = DeviceStateValuesSchema.parse(deviceState.state_json);
  const themeEnvelope = ThemeEnvelopeSchema.parse(theme.tokens_json);
  const themeMode =
    screenInstance.theme_mode ?? themeEnvelope.defaultMode ?? "light";
  const moduleThemeConfig = repository.getModuleThemeConfig(
    theme.id,
    screenInstance.module_id,
    screenInstance.module_schema_version,
  );
  if (!moduleThemeConfig) {
    throw new Error(
      `No module theme config for theme ${theme.id}, module ${screenInstance.module_id}, schema ${screenInstance.module_schema_version}`,
    );
  }
  const globalThemeTokens = resolveGlobalThemeTokens(theme, themeMode);
  const moduleThemeTokens = resolveModuleThemeTokens(
    moduleThemeConfig.tokens_json,
    themeMode,
  );
  const inheritedModuleTokens = mergeTokenObjects(
    globalThemeTokens,
    moduleThemeTokens,
  );
  const mergedThemeTokens = mergeTokenObjects(
    inheritedModuleTokens,
    screenInstance.module_tokens_override_json ?? {},
  );
  const themeTokens = ChatThemeSchema.parse(mergedThemeTokens);

  const messages = moduleData.messages.map((message) => {
    const sender = participants.get(message.senderParticipantId);
    if (!sender) {
      throw new Error(
        `Chat message ${message.id} references missing participant ${message.senderParticipantId}`,
      );
    }
    const direction =
      message.type === "system"
        ? "system"
        : sender.participantId === ownerParticipant.participantId
          ? "outgoing"
          : "incoming";
    const bubble = resolveMessageBubble({
      message,
      sender,
      direction,
      themeTokens: mergedThemeTokens,
      localFrame,
      fps,
      viewportWidth: metrics.viewport.width,
    });
    const mediaAsset = message.mediaAssetId
      ? requireRecord(
          repository.getMediaAsset(message.mediaAssetId),
          "MediaAsset",
          message.mediaAssetId,
        )
      : undefined;

    return {
      id: bubble.id,
      direction: bubble.direction,
      text: bubble.text,
      visibleText: bubble.visibleText,
      sender: {
        id: bubble.actor.id,
        participantId: sender.participantId,
        displayName: bubble.actor.displayName,
      },
      ...(mediaAsset
        ? {
            media: {
              assetId: mediaAsset.id,
              uri: mediaAsset.uri,
              ...(message.media?.window
                ? { window: message.media.window }
                : {}),
              ...(message.media?.transform
                ? { transform: message.media.transform }
                : {}),
            },
          }
        : {}),
      timing: {
        startFrame: bubble.timing.startFrame,
        enterDurationFrames: bubble.timing.enterDurationFrames,
        ...(bubble.timing.writeOnStartFrame !== null
          ? { writeOnStartFrame: bubble.timing.writeOnStartFrame }
          : {}),
        ...(bubble.timing.writeOnDurationFrames !== null
          ? { writeOnDurationFrames: bubble.timing.writeOnDurationFrames }
          : {}),
      },
      style: bubble.style,
      layout: bubble.layout,
      animation: bubble.animation,
    };
  });

  const ownerAvatar = ownerParticipant.avatarUri
    ? {
        assetId:
          moduleData.participants.find(
            (participant) => participant.id === ownerParticipant.participantId,
          )?.avatarAssetId ?? ownerActor.avatar_asset_id,
        uri: ownerParticipant.avatarUri,
      }
    : undefined;
  const headerAvatar = headerAvatarParticipant?.avatarUri
    ? {
        assetId:
          moduleData.participants.find(
            (participant) =>
              participant.id === headerAvatarParticipant.participantId,
          )?.avatarAssetId ??
          (headerAvatarParticipant.actorId
            ? repository.getActor(headerAvatarParticipant.actorId)
                ?.avatar_asset_id
            : undefined),
        uri: headerAvatarParticipant.avatarUri,
      }
    : undefined;

  return ResolvedChatScreenPropsSchema.parse({
    frame: localFrame,
    fps,
    screenInstanceId: screenInstance.id,
    themeMode,
    viewport: {
      ...metrics.viewport,
      safeArea: metrics.safeArea,
    },
    theme: {
      id: theme.id,
      ...themeTokens,
      chatBubbles: {
        ...themeTokens.chatBubbles,
        radius: themeTokens.radii.bubble,
      },
    },
    device: {
      id: device.id,
      osFamily: device.os_family,
      pixelRatio: metrics.pixelRatio,
      statusBarHeight: metrics.statusBar.height,
      cornerRadius: metrics.cornerRadius,
      defaultScreenScale: metrics.defaultScreenScale,
    },
    deviceState: state,
    ownerActor: {
      id: ownerActor.id,
      displayName: ownerActor.display_name,
      ...(ownerAvatar?.assetId ? { avatar: ownerAvatar } : {}),
    },
    header: {
      title: moduleData.header.title,
      subtitle: moduleData.header.subtitle,
      ...(headerAvatar?.assetId ? { avatar: headerAvatar } : {}),
    },
    messages,
    events: repository
      .getScreenEventsForInstance(screenInstance.id)
      .map((event) => ({
        id: event.id,
        type: event.event_type,
        startFrame: event.start_frame,
        durationFrames: event.duration_frames,
        targetId: event.target_id,
        payload: event.payload_json,
      })),
    props: {
      showHeader: moduleConfig.showHeader,
      showStatusBar: moduleConfig.showStatusBar,
      showKeyboard: moduleConfig.showKeyboard,
      initialScroll: moduleConfig.initialScroll,
      messageGrouping: moduleConfig.messageGrouping,
      debugShowBounds: moduleConfig.debugShowBounds,
    },
  });
}
