import { z } from "zod";

export const IdSchema = z.string().min(1);
export const NonNegativeIntegerSchema = z.number().int().min(0);
export const PositiveIntegerSchema = z.number().int().positive();
export const JsonObjectSchema = z.record(z.string(), z.unknown());
export const NullableJsonObjectSchema = JsonObjectSchema.nullable();

export type JsonObject = z.infer<typeof JsonObjectSchema>;
