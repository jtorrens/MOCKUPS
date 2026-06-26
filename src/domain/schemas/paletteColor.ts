import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const PaletteColorSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  token: z
    .string()
    .min(1)
    .regex(/^[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)*$/),
  value_hex: z
    .string()
    .regex(/^#[0-9a-fA-F]{6}$/)
    .transform((value) => value.toUpperCase()),
  metadata_json: JsonObjectSchema.optional(),
});

export type PaletteColor = z.infer<typeof PaletteColorSchema>;
