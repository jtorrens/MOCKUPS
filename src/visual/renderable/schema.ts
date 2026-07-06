import { z } from "zod";
import { renderableNodeTypes, type RenderableNode } from "./types.js";

const RenderableBoxSchema = z.object({
  x: z.number(),
  y: z.number(),
  width: z.number().min(0),
  height: z.number().min(0),
});

const RenderableTransformSchema = z.object({
  x: z.number().optional(),
  y: z.number().optional(),
  scale: z.number().optional(),
  rotation: z.number().optional(),
  opacity: z.number().min(0).max(1).optional(),
});

const RenderableAssetSchema = z.object({
  type: z.string().min(1),
  uri: z.string().min(1),
});

const RenderableNodeTypeSchema = z.enum(renderableNodeTypes);

const RenderableMetadataSchema = z.object({
  fontFaces: z.array(z.object({
    family: z.string().min(1),
    uri: z.string().min(1),
    weight: z.union([z.number(), z.string()]).optional(),
    style: z.string().optional(),
  }).strict()).optional(),
  fallbackText: z.string().optional(),
  imageBaseSize: z.number().optional(),
  imageOffsetX: z.number().optional(),
  imageOffsetY: z.number().optional(),
  imageScale: z.number().optional(),
}).strict();

export const RenderableNodeSchema: z.ZodType<RenderableNode> = z.lazy(() =>
  z.object({
    id: z.string().min(1),
    type: RenderableNodeTypeSchema,
    frame: z.number().int().min(0).optional(),
    box: RenderableBoxSchema.optional(),
    transform: RenderableTransformSchema.optional(),
    style: z.record(z.string(), z.unknown()).optional(),
    text: z.string().optional(),
    asset: RenderableAssetSchema.optional(),
    children: z.array(RenderableNodeSchema).optional(),
    metadata: RenderableMetadataSchema.optional(),
  }),
);
