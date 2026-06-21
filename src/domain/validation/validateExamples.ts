import { z } from "zod";
import deviceExample from "../../../docs/examples/device_iphone_generic.json" with {
  type: "json",
};
import productionExample from "../../../docs/examples/production_minimal.json" with {
  type: "json",
};
import resolvedChatExample from "../../../docs/examples/resolved_props_chat_screen.json" with {
  type: "json",
};
import resolvedBubbleExample from "../../../docs/examples/resolved_props_message_bubble.json" with {
  type: "json",
};
import shotExample from "../../../docs/examples/shot_lock_to_chat.json" with {
  type: "json",
};
import themeExample from "../../../docs/examples/theme_ios_light.json" with {
  type: "json",
};
import {
  ActorSchema,
  ChatModuleConfigSchema,
  ChatModuleDataSchema,
  DeviceSchema,
  EpisodeSchema,
  IdSchema,
  ProductionSchema,
  ResolvedChatScreenPropsSchema,
  ResolvedMessageBubblePropsSchema,
  ScreenEventSchema,
  ScreenInstanceSchema,
  ShotSchema,
  ThemeSchema,
} from "../schemas/index.js";

const ProductionMinimalExampleSchema = z.object({
  production: ProductionSchema,
  theme_refs: z.array(IdSchema).min(1),
  device_refs: z.array(IdSchema).min(1),
  screen_template_refs: z.array(IdSchema).min(1),
  episodes: z.array(EpisodeSchema).min(1),
  actors: z.array(ActorSchema).min(1),
  shots: z.array(ShotSchema).min(1),
  screen_instances: z.array(ScreenInstanceSchema).min(1),
});

const ShotLockToChatExampleSchema = z
  .object({
    production_id: IdSchema,
    shot: ShotSchema,
    references: z.object({
      notification_id: IdSchema,
    }),
    screen_instances: z.array(ScreenInstanceSchema).min(2),
    screen_events: z.array(ScreenEventSchema).min(2),
  })
  .superRefine((value, context) => {
    const screenInstanceIds = new Set(
      value.screen_instances.map((instance) => instance.id),
    );

    if (value.shot.production_id !== value.production_id) {
      context.addIssue({
        code: "custom",
        message: "shot.production_id must match production_id",
        path: ["shot", "production_id"],
      });
    }

    value.screen_instances.forEach((instance, index) => {
      if (instance.shot_id !== value.shot.id) {
        context.addIssue({
          code: "custom",
          message: "screen instance must reference shot.id",
          path: ["screen_instances", index, "shot_id"],
        });
      }
    });

    value.screen_events.forEach((event, index) => {
      if (!screenInstanceIds.has(event.screen_instance_id)) {
        context.addIssue({
          code: "custom",
          message: "screen event must reference a listed screen instance",
          path: ["screen_events", index, "screen_instance_id"],
        });
      }
    });

    const chatInstance = value.screen_instances.find(
      (instance) => instance.screen_type === "chat",
    );
    if (chatInstance?.module_data_json) {
      const result = ChatModuleDataSchema.safeParse(
        chatInstance.module_data_json,
      );
      if (!result.success) {
        context.addIssue({
          code: "custom",
          message: "chat module_data_json must match ChatModuleDataSchema",
          path: ["screen_instances", "module_data_json"],
        });
      }
    }
    if (chatInstance?.module_config_json) {
      const result = ChatModuleConfigSchema.safeParse(
        chatInstance.module_config_json,
      );
      if (!result.success) {
        context.addIssue({
          code: "custom",
          message: "chat module_config_json must match ChatModuleConfigSchema",
          path: ["screen_instances", "module_config_json"],
        });
      }
    }
    if (chatInstance?.data_ref_json !== null) {
      context.addIssue({
        code: "custom",
        message: "canonical Chat example must not use data_ref_json",
        path: ["screen_instances", "data_ref_json"],
      });
    }

    const notificationEvent = value.screen_events.find(
      (event) => event.event_type === "notification_appears",
    );
    if (
      notificationEvent?.payload_json.notification_id !==
      value.references.notification_id
    ) {
      context.addIssue({
        code: "custom",
        message:
          "notification event payload must match references.notification_id",
        path: ["references", "notification_id"],
      });
    }
  });

const validations = [
  {
    file: "production_minimal.json",
    schema: ProductionMinimalExampleSchema,
    value: productionExample,
  },
  {
    file: "shot_lock_to_chat.json",
    schema: ShotLockToChatExampleSchema,
    value: shotExample,
  },
  {
    file: "theme_ios_light.json",
    schema: ThemeSchema,
    value: themeExample,
  },
  {
    file: "device_iphone_generic.json",
    schema: DeviceSchema,
    value: deviceExample,
  },
  {
    file: "resolved_props_chat_screen.json",
    schema: ResolvedChatScreenPropsSchema,
    value: resolvedChatExample,
  },
  {
    file: "resolved_props_message_bubble.json",
    schema: ResolvedMessageBubblePropsSchema,
    value: resolvedBubbleExample,
  },
] as const;

let hasFailure = false;

for (const validation of validations) {
  const result = validation.schema.safeParse(validation.value);
  if (result.success) {
    console.log(`✓ ${validation.file}`);
    continue;
  }

  hasFailure = true;
  console.error(`✗ ${validation.file}`);
  console.error(z.prettifyError(result.error));
}

if (hasFailure) {
  throw new Error("Example validation failed");
}

console.log(`Validated ${validations.length} example JSON files successfully.`);
