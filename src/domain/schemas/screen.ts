import { z } from "zod";
import {
  IdSchema,
  JsonObjectSchema,
  NonNegativeIntegerSchema,
  NullableJsonObjectSchema,
  PositiveIntegerSchema,
} from "./common.js";
import {
  ChatModuleConfigSchema,
  ChatModuleDataSchema,
  ThemeModeSchema,
} from "./module.js";

export const ScreenTypeSchema = z.enum([
  "chat",
  "lock_screen",
  "notification_stack",
  "incoming_call",
  "in_call",
  "home_screen",
  "custom_app",
]);

export const ScreenEventTypeSchema = z.enum([
  "notification_appears",
  "unlock_gesture",
  "incoming_call_accepted",
  "message_write_on_starts",
  "scroll_moves",
  "keyboard_appears",
  "app_transition",
]);

export const ScreenInstanceSchema = z
  .object({
    id: IdSchema,
    shot_id: IdSchema,
    app_id: IdSchema,
    screen_type: ScreenTypeSchema,
    module_id: IdSchema.nullable().optional(),
    module_schema_version: PositiveIntegerSchema.nullable().optional(),
    owner_actor_id: IdSchema,
    device_id: IdSchema.nullable().optional(),
    device_state_id: IdSchema.nullable().optional(),
    theme_id: IdSchema.nullable().optional(),
    theme_mode: ThemeModeSchema.nullable().optional(),
    start_frame: NonNegativeIntegerSchema,
    end_frame: NonNegativeIntegerSchema,
    layer_order: z.number().int(),
    data_ref_json: NullableJsonObjectSchema,
    module_data_json: JsonObjectSchema.nullable().optional(),
    module_config_json: JsonObjectSchema.nullable().optional(),
    module_tokens_override_json: JsonObjectSchema.nullable().optional(),
    transform_json: JsonObjectSchema,
    props_json: JsonObjectSchema,
    transition_in_json: NullableJsonObjectSchema,
    transition_out_json: NullableJsonObjectSchema,
  })
  .refine((value) => value.end_frame > value.start_frame, {
    message: "end_frame must be greater than start_frame",
    path: ["end_frame"],
  })
  .superRefine((value, context) => {
    if (value.screen_type !== "chat") {
      return;
    }
    if (value.module_id !== "core.chat") {
      context.addIssue({
        code: "custom",
        message: "Chat screen instances require module_id core.chat",
        path: ["module_id"],
      });
    }
    if (value.module_schema_version !== 1) {
      context.addIssue({
        code: "custom",
        message: "Chat screen instances require module_schema_version 1",
        path: ["module_schema_version"],
      });
    }
    if (value.data_ref_json !== null) {
      context.addIssue({
        code: "custom",
        message: "Canonical Chat screen instances must not use data_ref_json",
        path: ["data_ref_json"],
      });
    }
    const dataResult = ChatModuleDataSchema.safeParse(value.module_data_json);
    if (!dataResult.success) {
      context.addIssue({
        code: "custom",
        message: "Chat screen instance requires valid module_data_json",
        path: ["module_data_json"],
      });
    }
    const configResult = ChatModuleConfigSchema.safeParse(
      value.module_config_json,
    );
    if (!configResult.success) {
      context.addIssue({
        code: "custom",
        message: "Chat screen instance requires valid module_config_json",
        path: ["module_config_json"],
      });
    }
    if (!value.module_tokens_override_json) {
      context.addIssue({
        code: "custom",
        message: "Chat screen instance requires module_tokens_override_json",
        path: ["module_tokens_override_json"],
      });
    }
  });

export const ScreenEventSchema = z.object({
  id: IdSchema,
  screen_instance_id: IdSchema,
  event_type: ScreenEventTypeSchema,
  start_frame: NonNegativeIntegerSchema,
  duration_frames: NonNegativeIntegerSchema,
  target_id: IdSchema.nullable(),
  animation_preset_id: IdSchema.nullable(),
  payload_json: JsonObjectSchema,
});

export type ScreenType = z.infer<typeof ScreenTypeSchema>;
export type ScreenEventType = z.infer<typeof ScreenEventTypeSchema>;
export type ScreenInstance = z.infer<typeof ScreenInstanceSchema>;
export type ScreenEvent = z.infer<typeof ScreenEventSchema>;
