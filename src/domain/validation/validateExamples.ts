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
import shotExample from "../../../docs/examples/shot_chat.json" with {
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
  ModuleInstanceSchema,
  ProductionSchema,
  ResolvedChatScreenPropsSchema,
  ResolvedMessageBubblePropsSchema,
  ScreenInstanceSchema,
  ShotSchema,
  ThemeSchema,
} from "../schemas/index.js";

const ProductionMinimalExampleSchema = z.object({
  production: ProductionSchema,
  theme_refs: z.array(IdSchema).min(1),
  device_refs: z.array(IdSchema).min(1),
  app_refs: z.array(IdSchema).min(1),
  episodes: z.array(EpisodeSchema).min(1),
  actors: z.array(ActorSchema).min(1),
  shots: z.array(ShotSchema).min(1),
  screen_instances: z.array(ScreenInstanceSchema).min(1),
  module_instances: z.array(ModuleInstanceSchema).min(1),
});

const ShotChatExampleSchema = z
  .object({
    production_id: IdSchema,
    shot: ShotSchema,
    screen_instances: z.array(ScreenInstanceSchema).min(1),
    module_instances: z.array(ModuleInstanceSchema).min(1),
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

    const chatInstance = value.screen_instances.find(
      (instance) => instance.screen_type === "chat",
    );
    const chatModuleInstance = value.module_instances.find(
      (instance) => instance.screen_instance_id === chatInstance?.id,
    );
    value.module_instances.forEach((instance, index) => {
      if (!screenInstanceIds.has(instance.screen_instance_id)) {
        context.addIssue({
          code: "custom",
          message: "module instance must reference a listed screen instance",
          path: ["module_instances", index, "screen_instance_id"],
        });
      }
    });
    if (chatModuleInstance) {
      const dataResult = ChatModuleDataSchema.safeParse(
        chatModuleInstance.content_json,
      );
      if (!dataResult.success) {
        context.addIssue({
          code: "custom",
          message: "chat content_json must match ChatModuleDataSchema",
          path: ["module_instances", "content_json"],
        });
      }
      const configResult = ChatModuleConfigSchema.safeParse(
        chatModuleInstance.behavior_json,
      );
      if (!configResult.success) {
        context.addIssue({
          code: "custom",
          message: "chat behavior_json must match ChatModuleConfigSchema",
          path: ["module_instances", "behavior_json"],
        });
      }
    } else {
      context.addIssue({
        code: "custom",
        message: "chat screen instance must have a module instance",
        path: ["module_instances"],
      });
    }
    if (chatInstance?.data_ref_json !== null) {
      context.addIssue({
        code: "custom",
        message: "canonical Chat example must not use data_ref_json",
        path: ["screen_instances", "data_ref_json"],
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
    file: "shot_chat.json",
    schema: ShotChatExampleSchema,
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
