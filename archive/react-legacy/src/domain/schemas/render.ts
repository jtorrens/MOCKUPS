import { z } from "zod";
import { IdSchema, JsonObjectSchema, PositiveIntegerSchema } from "./common.js";

export const RenderPresetSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  width: PositiveIntegerSchema,
  height: PositiveIntegerSchema,
  fps: PositiveIntegerSchema,
  format: z.string().min(1),
  codec_json: JsonObjectSchema.optional(),
  color_json: JsonObjectSchema.optional(),
  quality_json: JsonObjectSchema.optional(),
  export_json: JsonObjectSchema.optional(),
});

export type RenderPreset = z.infer<typeof RenderPresetSchema>;
