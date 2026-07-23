import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const ComponentClassTypeSchema = z.enum([
  "avatar",
  "audio",
  "bubble",
  "buttonIcon",
  "button",
  "cursor",
  "iconBar",
  "componentStack",
  "collectionStack",
  "badge",
  "notification",
  "notifications",
  "listItem",
  "list",
  "codeIndicator",
  "iconRow",
  "keyboard",
  "keypad",
  "fingerprint",
  "faceRecognition",
  "drawPassword",
  "password",
  "label",
  "media",
  "navigation_bar",
  "status_bar",
  "surface",
  "textBox",
  "textInputBar",
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
