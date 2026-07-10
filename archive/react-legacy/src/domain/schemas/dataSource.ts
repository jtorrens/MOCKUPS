import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const DataSourceSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  data_type: z.string().min(1),
  app_id: IdSchema.nullable().optional(),
  version: z.string().min(1),
  data_json: JsonObjectSchema,
  config_json: JsonObjectSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type DataSource = z.infer<typeof DataSourceSchema>;
