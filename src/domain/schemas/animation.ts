import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const AnimationPresetSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  animation_type: z.string().min(1),
  version: z.string().min(1),
  parameters_json: JsonObjectSchema,
});

export type AnimationPreset = z.infer<typeof AnimationPresetSchema>;
