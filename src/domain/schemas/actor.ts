import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const ActorSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  display_name: z.string().min(1),
  short_name: z.string().min(1).nullable().optional(),
  default_device_id: IdSchema.nullable().optional(),
  default_theme_id: IdSchema.nullable().optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type Actor = z.infer<typeof ActorSchema>;
