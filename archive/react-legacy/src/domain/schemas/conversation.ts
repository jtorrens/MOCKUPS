import { z } from "zod";
import { IdSchema, JsonObjectSchema, NonNegativeIntegerSchema } from "./common.js";

export const ConversationSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  app_id: IdSchema.nullable().optional(),
  owner_actor_id: IdSchema.nullable().optional(),
  target_actor_id: IdSchema.nullable().optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export const ConversationParticipantSchema = z.object({
  id: IdSchema,
  conversation_id: IdSchema,
  actor_id: IdSchema,
  role: z.string().min(1),
  sort_order: NonNegativeIntegerSchema,
  metadata_json: JsonObjectSchema.optional(),
});

export const MessageSchema = z.object({
  id: IdSchema,
  conversation_id: IdSchema,
  sort_order: NonNegativeIntegerSchema,
  sender_actor_id: IdSchema,
  message_type: z.string().min(1),
  text: z.string().nullable(),
  start_frame: NonNegativeIntegerSchema,
  enter_duration_frames: NonNegativeIntegerSchema,
  write_on_enabled: z.boolean(),
  write_on_start_frame: NonNegativeIntegerSchema.nullable(),
  write_on_duration_frames: NonNegativeIntegerSchema.nullable(),
  exit_frame: NonNegativeIntegerSchema.nullable(),
  style_override_json: JsonObjectSchema,
  animation_override_json: JsonObjectSchema,
  layout_override_json: JsonObjectSchema,
  metadata_json: JsonObjectSchema,
});

export type Conversation = z.infer<typeof ConversationSchema>;
export type ConversationParticipant = z.infer<
  typeof ConversationParticipantSchema
>;
export type Message = z.infer<typeof MessageSchema>;
