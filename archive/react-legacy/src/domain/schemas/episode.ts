import { z } from "zod";
import {
  IdSchema,
  JsonObjectSchema,
  NonNegativeIntegerSchema,
} from "./common.js";

export const EpisodeSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  slug: z.string().min(1).optional(),
  sort_order: NonNegativeIntegerSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type Episode = z.infer<typeof EpisodeSchema>;
