import { z } from "zod";
import { IdSchema, JsonObjectSchema } from "./common.js";

export const CallSchema = z.object({
  id: IdSchema,
  production_id: IdSchema,
  app_id: IdSchema.nullable().optional(),
  owner_actor_id: IdSchema,
  target_actor_id: IdSchema,
  call_type: z.string().min(1),
  initial_state: z.string().min(1),
  payload_json: JsonObjectSchema.optional(),
  metadata_json: JsonObjectSchema.optional(),
});

export type Call = z.infer<typeof CallSchema>;
