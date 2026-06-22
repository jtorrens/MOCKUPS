import { z } from "zod";
import type { DomainRepository } from "../repository/types.js";
import type {
  JsonObject,
  ResolvedChatScreenProps,
  ScreenInstance,
  ScreenType,
} from "../schemas/index.js";
import { requireRecord } from "./helpers.js";
import { resolveChatScreen } from "./resolveChatScreen.js";

const LightweightDataReferenceSchema = z.discriminatedUnion("type", [
  z.object({
    type: z.literal("notification"),
    notification_id: z.string().min(1),
  }),
  z.object({
    type: z.literal("conversation"),
    conversation_id: z.string().min(1),
  }),
]);

function resolveLightweightDataReference(
  repository: DomainRepository,
  dataReference: JsonObject | null,
): JsonObject | null {
  if (dataReference === null) {
    return null;
  }

  const reference = LightweightDataReferenceSchema.parse(dataReference);
  if (reference.type === "conversation") {
    const conversation = requireRecord(
      repository.getConversation(reference.conversation_id),
      "Conversation",
      reference.conversation_id,
    );
    return {
      type: reference.type,
      conversation,
      participants: repository.getConversationParticipants(conversation.id),
      messages: repository.getMessagesForConversation(conversation.id),
    };
  }

  const notification = requireRecord(
    repository.getNotification(reference.notification_id),
    "Notification",
    reference.notification_id,
  );
  const app = requireRecord(
    repository.getApp(notification.app_id),
    "App",
    notification.app_id,
  );
  return {
    type: reference.type,
    notification,
    app,
  };
}

export interface ResolvedScreenInstance {
  screen_instance_id: string;
  screen_type: ScreenType;
  shot_frame: number;
  local_frame: number;
  layer_order: number;
  transform: JsonObject;
  resolved_props?: ResolvedChatScreenProps;
  resolved_context?: JsonObject;
}

export interface ResolveScreenInstanceInput {
  repository: DomainRepository;
  screenInstance: ScreenInstance;
  shotOwnerActorId?: string | null;
  shotFrame: number;
  fps: number;
}

export function resolveScreenInstance({
  repository,
  screenInstance,
  shotOwnerActorId,
  shotFrame,
  fps,
}: ResolveScreenInstanceInput): ResolvedScreenInstance {
  const localFrame = shotFrame - screenInstance.start_frame;
  if (localFrame < 0 || shotFrame >= screenInstance.end_frame) {
    throw new Error(
      `Screen instance ${screenInstance.id} is not active at shot frame ${shotFrame}`,
    );
  }

  const ownerActor = requireRecord(
    repository.getActor(shotOwnerActorId ?? screenInstance.owner_actor_id),
    "Actor",
    shotOwnerActorId ?? screenInstance.owner_actor_id,
  );
  const app = requireRecord(
    repository.getApp(screenInstance.app_id),
    "App",
    screenInstance.app_id,
  );
  const deviceId = screenInstance.device_id ?? ownerActor.default_device_id;
  const themeId = screenInstance.theme_id ?? ownerActor.default_theme_id;
  if (!deviceId || !themeId) {
    throw new Error(
      `Screen instance ${screenInstance.id} cannot resolve device/theme defaults`,
    );
  }

  const device = requireRecord(
    repository.getDevice(deviceId),
    "Device",
    deviceId,
  );
  const theme = requireRecord(repository.getTheme(themeId), "Theme", themeId);
  const deviceState = screenInstance.device_state_id
    ? requireRecord(
        repository.getDeviceState(screenInstance.device_state_id),
        "DeviceState",
        screenInstance.device_state_id,
      )
    : undefined;
  const events = repository.getScreenEventsForInstance(screenInstance.id);

  const base: ResolvedScreenInstance = {
    screen_instance_id: screenInstance.id,
    screen_type: screenInstance.screen_type,
    shot_frame: shotFrame,
    local_frame: localFrame,
    layer_order: screenInstance.layer_order,
    transform: screenInstance.transform_json,
  };

  if (screenInstance.screen_type === "chat") {
    if (!deviceState) {
      throw new Error(
        `Chat screen instance ${screenInstance.id} requires a resolved device state`,
      );
    }
    return {
      ...base,
      resolved_props: resolveChatScreen({
        repository,
        screenInstance,
        ownerActor,
        app,
        device,
        deviceState,
        theme,
        localFrame,
        fps,
      }),
    };
  }

  return {
    ...base,
    resolved_context: {
      ownerActorId: ownerActor.id,
      appId: app.id,
      deviceId: device.id,
      deviceStateId: deviceState?.id ?? null,
      themeId: theme.id,
      dataRef: screenInstance.data_ref_json,
      data: resolveLightweightDataReference(
        repository,
        screenInstance.data_ref_json,
      ),
      events: events.map((event) => ({
        id: event.id,
        type: event.event_type,
        startFrame: event.start_frame,
        durationFrames: event.duration_frames,
        targetId: event.target_id,
        payload: event.payload_json,
      })),
      props: {
        ...screenInstance.props_json,
      },
    },
  };
}
