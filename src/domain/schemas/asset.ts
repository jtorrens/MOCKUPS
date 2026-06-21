import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const MediaAssetSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  name: z.string().min(1),
  asset_type: z.string().min(1),
  uri: z.string().min(1),
  mime_type: z.string().min(1),
  checksum: z.string().min(1).nullable().optional(),
  dimensions_json: JsonObjectSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type MediaAsset = z.infer<typeof MediaAssetSchema>;
