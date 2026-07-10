import { z } from "zod";
import {
  IdSchema,
  JsonObjectSchema,
  NonNegativeIntegerSchema,
  PositiveIntegerSchema,
} from "./common.js";

export const AnimationPresetSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  animation_type: z.string().min(1),
  version: z.string().min(1),
  parameters_json: JsonObjectSchema,
});

export const ModuleParameterAnimationKeyframeSchema = z
  .object({
    frame: NonNegativeIntegerSchema,
    value: z.unknown(),
    enabled: z.boolean().optional(),
  })
  .passthrough();

export const ModuleParameterAnimationTrackSchema = z
  .object({
    id: IdSchema.optional(),
    parameterId: z.string().min(1),
    itemId: IdSchema.optional(),
    label: z.string().optional(),
    keyframes: z.array(ModuleParameterAnimationKeyframeSchema).default([]),
  })
  .passthrough();

export const ModuleParameterAnimationSchema = z
  .object({
    schemaVersion: PositiveIntegerSchema.default(1),
    tracks: z.array(ModuleParameterAnimationTrackSchema).default([]),
  })
  .passthrough()
  .default({ schemaVersion: 1, tracks: [] });

export type AnimationPreset = z.infer<typeof AnimationPresetSchema>;
export type ModuleParameterAnimation = z.infer<
  typeof ModuleParameterAnimationSchema
>;
