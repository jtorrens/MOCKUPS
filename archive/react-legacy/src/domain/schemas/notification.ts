import { z } from "zod";
import { IdSchema, JsonObjectSchema, NonNegativeIntegerSchema } from "./common.js";

export const NotificationSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  app_id: IdSchema,
  owner_actor_id: IdSchema,
  sender_actor_id: IdSchema.nullable().optional(),
  notification_type: z.string().min(1),
  title: z.string().min(1),
  body: z.string(),
  sort_order: NonNegativeIntegerSchema,
  payload_json: JsonObjectSchema,
  style_override_json: JsonObjectSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type Notification = z.infer<typeof NotificationSchema>;
