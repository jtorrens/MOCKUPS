import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const ThemeSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  family: z.string().min(1),
  version: z.string().min(1),
  tokens_json: JsonObjectSchema,
});

export type Theme = z.infer<typeof ThemeSchema>;
