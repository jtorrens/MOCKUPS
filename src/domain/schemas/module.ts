import { z } from "zod";
import {
  IdSchema,
  JsonObjectSchema,
  NonNegativeIntegerSchema,
} from "./common.js";

export const ThemeModeSchema = z.enum(["light", "dark"]);

export const MediaWindowSchema = z.object({
  width: z.number().positive(),
  height: z.number().positive(),
  offsetX: z.number(),
  offsetY: z.number(),
});

export const AssetTransformSchema = z.object({
  scale: z.number().positive(),
  translateX: z.number(),
  translateY: z.number(),
  rotationDegrees: z.number(),
});

export const ModuleMediaReferenceSchema = z.object({
  assetId: IdSchema,
  window: MediaWindowSchema,
  transform: AssetTransformSchema,
});

export const TextRevealSchema = z.object({
  mode: z.enum([
    "none",
    "simple_write_on",
    "natural_write_on",
    "waiting_dots",
  ]),
  startFrame: NonNegativeIntegerSchema.default(0),
  durationFrames: NonNegativeIntegerSchema,
});

export const ChatModuleMessageSchema = z
  .object({
    id: IdSchema,
    actorId: IdSchema.optional(),
    direction: z.enum(["incoming", "outgoing", "system"]).optional(),
    /**
     * Message family. Media is not exclusive with text: a text message may also
     * include mediaAssetId/media to represent an attachment with a caption.
     */
    type: z.enum(["text", "media", "system"]),
    text: z.string().optional(),
    mediaAssetId: IdSchema.optional(),
    media: z
      .object({
        type: z.enum(["none", "image", "video", "audio"]).optional(),
        filePath: z.string().optional(),
        durationSeconds: z.number().positive().optional(),
        playMode: z.enum(["once", "loop"]).optional(),
        playStartFrame: NonNegativeIntegerSchema.optional(),
        window: MediaWindowSchema,
        transform: AssetTransformSchema,
      })
      .partial({ window: true, transform: true })
      .optional(),
    showBubbleBackground: z.boolean().default(true),
    textScale: z.number().positive().default(1),
    status: z
      .object({
        text: z.string().optional(),
        deliveryStatus: z
          .enum(["none", "sent", "delivered", "read", "failed"])
          .default("none"),
      })
      .optional(),
    /**
     * Authoring timing: frames to wait after the previous message write-on has
     * finished. Message order is the source of truth for sequencing.
     */
    delayAfterPreviousFrames: NonNegativeIntegerSchema.optional(),
    /**
     * Legacy absolute timing kept as an optional import/migration fallback.
     * The resolver converts it into delayAfterPreviousFrames when the new field
     * is absent.
     */
    startFrame: NonNegativeIntegerSchema.optional(),
    enterDurationFrames: NonNegativeIntegerSchema.default(0),
    exitFrame: NonNegativeIntegerSchema.optional(),
    textReveal: TextRevealSchema.optional(),
    styleOverride: JsonObjectSchema.optional(),
    layoutOverride: JsonObjectSchema.optional(),
    animation: JsonObjectSchema.optional(),
    metadata: JsonObjectSchema.optional(),
  })
  .superRefine((value, context) => {
    if (value.type === "text" && value.text === undefined) {
      context.addIssue({
        code: "custom",
        message: "text messages require text",
        path: ["text"],
      });
    }
    if (
      value.media &&
      value.media.type !== "none" &&
      value.media.type !== "audio" &&
      !value.media.filePath &&
      !value.mediaAssetId
    ) {
      context.addIssue({
        code: "custom",
        message: "media requires mediaAssetId or media.filePath",
        path: ["mediaAssetId"],
      });
    }
  });

export const ChatModuleDataSchema = z
  .object({
    schemaVersion: z.literal(1),
    header: z.object({
      title: z.string().min(1),
      subtitle: z.string().optional(),
      actorId: IdSchema.optional(),
      iconToken: z.string().min(1).optional(),
      useContactColor: z.boolean().optional(),
    }),
    messages: z.array(ChatModuleMessageSchema),
  })
  .superRefine((value, context) => {
    value.messages.forEach((message, index) => {
      if (message.type !== "system" && message.direction !== "system" && !message.actorId) {
        context.addIssue({
          code: "custom",
          message: "non-system messages require actorId",
          path: ["messages", index, "actorId"],
        });
      }
    });
  });

export const ChatModuleConfigSchema = z.object({
  showHeader: z.boolean().default(true),
  showStatusBar: z.boolean().default(true),
  showNavigationBar: z.boolean().default(true),
  statusBar: JsonObjectSchema.optional(),
  keyboard: JsonObjectSchema.optional(),
  showKeyboard: z.boolean().default(false),
  textInputBar: JsonObjectSchema.optional(),
  showTextInputBar: z.boolean().default(false),
  showIncomingActorLabels: z.boolean().default(false),
  initialScroll: z.enum(["top", "bottom", "preserve"]).default("bottom"),
  messageGrouping: z.enum(["none", "bySender"]).default("bySender"),
  debugShowBounds: z.boolean().default(false),
  behaviorDefaults: JsonObjectSchema.optional(),
});

export type ThemeMode = z.infer<typeof ThemeModeSchema>;
export type ModuleMediaReference = z.infer<typeof ModuleMediaReferenceSchema>;
export type ChatModuleMessage = z.infer<typeof ChatModuleMessageSchema>;
export type ChatModuleData = z.infer<typeof ChatModuleDataSchema>;
export type ChatModuleConfig = z.infer<typeof ChatModuleConfigSchema>;
