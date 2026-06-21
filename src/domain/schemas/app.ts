import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const AppSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  bundle_key: z.string().min(1),
  app_type: z.string().min(1),
  icon_asset_id: IdSchema.nullable().optional(),
  config_json: JsonObjectSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type App = z.infer<typeof AppSchema>;
