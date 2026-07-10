import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const IconThemeSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  family: z.string().min(1),
  asset_root: z.string().min(1),
  mapping_json: JsonObjectSchema,
  metadata_json: JsonObjectSchema.optional(),
});

export type IconTheme = z.infer<typeof IconThemeSchema>;
