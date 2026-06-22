import { z } from "zod";
import type { DomainRepository } from "../repository/types.js";
import type {
  JsonObject,
  ResolvedChatScreenProps,
  ScreenInstance,
  ScreenTemplate,
  ScreenType,
} from "../schemas/index.js";
import { requireRecord } from "./helpers.js";
import { mergeTokenObjects, resolveChatScreen } from "./resolveChatScreen.js";

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

function jsonObjectAt(
  value: JsonObject | undefined,
  key: string,
): JsonObject {
  const candidate = value?.[key];
  return typeof candidate === "object" &&
    candidate !== null &&
    !Array.isArray(candidate)
    ? (candidate as JsonObject)
    : {};
}

function inheritedScreenInstanceFromTemplate(
  screenTemplate: ScreenTemplate,
  screenInstance: ScreenInstance,
): ScreenInstance {
  const templateConfig = screenTemplate.config_json ?? {};
  const templateModuleData = jsonObjectAt(templateConfig, "module_data_json");
  const templateModuleConfig = jsonObjectAt(
    templateConfig,
    "module_config_json",
  );
  const templateTokenOverrides = jsonObjectAt(
    templateConfig,
    "module_tokens_override_json",
  );
  const templateTransform = jsonObjectAt(templateConfig, "transform_json");

  return {
    ...screenInstance,
    module_data_json: mergeTokenObjects(
      templateModuleData,
      screenInstance.module_data_json ?? {},
    ),
    module_config_json: mergeTokenObjects(
      templateModuleConfig,
      screenInstance.module_config_json ?? {},
    ),
    module_tokens_override_json: mergeTokenObjects(
      templateTokenOverrides,
      screenInstance.module_tokens_override_json ?? {},
    ),
    transform_json: mergeTokenObjects(
      templateTransform,
      screenInstance.transform_json,
    ),
    props_json: mergeTokenObjects(
      screenTemplate.default_props_json ?? {},
      screenInstance.props_json,
    ),
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
  const screenTemplate = requireRecord(
    repository.getScreenTemplate(screenInstance.screen_template_id),
    "ScreenTemplate",
    screenInstance.screen_template_id,
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

  if (screenTemplate.screen_type !== screenInstance.screen_type) {
    throw new Error(
      `Screen template ${screenTemplate.id} type does not match instance ${screenInstance.id}`,
    );
  }
  const effectiveScreenInstance = inheritedScreenInstanceFromTemplate(
    screenTemplate,
    screenInstance,
  );

  const base: ResolvedScreenInstance = {
    screen_instance_id: screenInstance.id,
    screen_type: screenInstance.screen_type,
    shot_frame: shotFrame,
    local_frame: localFrame,
    layer_order: screenInstance.layer_order,
    transform: effectiveScreenInstance.transform_json,
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
        screenInstance: effectiveScreenInstance,
        ownerActor,
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
      deviceId: device.id,
      deviceStateId: deviceState?.id ?? null,
      themeId: theme.id,
      screenTemplateId: screenTemplate.id,
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
        ...effectiveScreenInstance.props_json,
      },
    },
  };
}
