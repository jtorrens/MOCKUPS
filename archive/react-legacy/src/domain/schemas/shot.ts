import { z } from "zod";
import {
  IdSchema,
  JsonObjectSchema,
  NonNegativeIntegerSchema,
  PositiveIntegerSchema,
} from "./common.js";

export const ShotSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  episode_id: IdSchema.nullable().optional(),
  owner_actor_id: IdSchema.nullable().optional(),
  name: z.string().min(1),
  slug: z.string().min(1).optional(),
  version: NonNegativeIntegerSchema.optional(),
  sort_order: NonNegativeIntegerSchema.optional(),
  duration_frames: PositiveIntegerSchema,
  fps: PositiveIntegerSchema,
  render_preset_id: IdSchema.nullable().optional(),
  canvas_json: JsonObjectSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type Shot = z.infer<typeof ShotSchema>;
