import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const ProductionSchema = z.object({
  id: IdSchema,
  name: z.string().min(1),
  slug: z.string().min(1).optional(),
  created_at: z.string().min(1).optional(),
  updated_at: z.string().min(1).optional(),
  settings_json: JsonObjectSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type Production = z.infer<typeof ProductionSchema>;
