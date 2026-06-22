import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const ModuleThemeConfigSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  theme_id: IdSchema,
  app_id: IdSchema,
  module_id: z.string().min(1),
  module_schema_version: z.number().int().positive(),
  name: z.string().min(1),
  tokens_json: JsonObjectSchema,
  metadata_json: JsonObjectSchema.optional(),
});

export type ModuleThemeConfig = z.infer<typeof ModuleThemeConfigSchema>;
