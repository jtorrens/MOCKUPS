import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const ProductionFontSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  family: z.string().min(1),
  style: z.string().min(1),
  file_path: z.string().min(1),
  source_path: z.string().min(1).optional(),
  postscript_name: z.string().min(1).optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type ProductionFont = z.infer<typeof ProductionFontSchema>;
