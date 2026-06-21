import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const DeviceSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  manufacturer: z.string().min(1),
  model: z.string().min(1),
  os_family: z.string().min(1),
  metrics_json: JsonObjectSchema,
  frame_asset_id: IdSchema.nullable().optional(),
});

export const DeviceStateSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  device_id: IdSchema,
  name: z.string().min(1),
  state_json: JsonObjectSchema,
});

export type Device = z.infer<typeof DeviceSchema>;
export type DeviceState = z.infer<typeof DeviceStateSchema>;
