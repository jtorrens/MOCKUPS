import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const ComponentClassTypeSchema = z.enum([
  "avatar",
  "audio",
  "buttonIcon",
  "cursor",
  "keyboard",
  "label",
  "navigation_bar",
  "status_bar",
  "surface",
  "textBox",
  "textInputBar",
  "video",
]);

export const ComponentClassSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  component_type: ComponentClassTypeSchema,
  name: z.string().min(1),
  tokens_json: JsonObjectSchema,
  metadata_json: JsonObjectSchema.optional(),
});

export type ComponentClass = z.infer<typeof ComponentClassSchema>;
export type ComponentClassType = z.infer<typeof ComponentClassTypeSchema>;
