import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const NavigationBarSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  family: z.string().min(1),
  config_json: JsonObjectSchema,
  metadata_json: JsonObjectSchema.optional(),
});

export type NavigationBar = z.infer<typeof NavigationBarSchema>;
