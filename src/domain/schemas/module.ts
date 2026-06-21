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

export const ChatParticipantSchema = z.object({
  id: IdSchema,
  displayName: z.string().min(1).optional(),
  actorId: IdSchema.optional(),
  avatarAssetId: IdSchema.optional(),
  role: z.enum(["owner", "participant"]),
});

export const TextRevealSchema = z.object({
  mode: z.enum(["none", "simple_write_on"]),
  startFrame: NonNegativeIntegerSchema,
  durationFrames: NonNegativeIntegerSchema,
});

export const ChatModuleMessageSchema = z
  .object({
    id: IdSchema,
    senderParticipantId: IdSchema,
    /**
     * Message family. Media is not exclusive with text: a text message may also
     * include mediaAssetId/media to represent an attachment with a caption.
     */
    type: z.enum(["text", "media", "system"]),
    text: z.string().optional(),
    mediaAssetId: IdSchema.optional(),
    media: z
      .object({
        window: MediaWindowSchema,
        transform: AssetTransformSchema,
      })
      .optional(),
    startFrame: NonNegativeIntegerSchema,
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
    if (value.media && !value.mediaAssetId) {
      context.addIssue({
        code: "custom",
        message: "media window/transform requires mediaAssetId",
        path: ["mediaAssetId"],
      });
    }
  });

export const ChatModuleDataSchema = z
  .object({
    schemaVersion: z.literal(1),
    participants: z.array(ChatParticipantSchema).min(1),
    header: z.object({
      title: z.string().min(1),
      subtitle: z.string().optional(),
      avatarParticipantId: IdSchema.optional(),
      iconToken: z.string().min(1).optional(),
    }),
    messages: z.array(ChatModuleMessageSchema),
  })
  .superRefine((value, context) => {
    const participantIds = new Set(
      value.participants.map((participant) => participant.id),
    );
    const ownerParticipants = value.participants.filter(
      (participant) => participant.role === "owner",
    );
    if (ownerParticipants.length !== 1) {
      context.addIssue({
        code: "custom",
        message: "Chat module data requires exactly one owner participant",
        path: ["participants"],
      });
    }
    value.participants.forEach((participant, index) => {
      if (!participant.actorId && !participant.displayName) {
        context.addIssue({
          code: "custom",
          message: "participant requires actorId or displayName",
          path: ["participants", index],
        });
      }
    });
    if (
      value.header.avatarParticipantId &&
      !participantIds.has(value.header.avatarParticipantId)
    ) {
      context.addIssue({
        code: "custom",
        message: "header.avatarParticipantId must reference a participant",
        path: ["header", "avatarParticipantId"],
      });
    }
    value.messages.forEach((message, index) => {
      if (!participantIds.has(message.senderParticipantId)) {
        context.addIssue({
          code: "custom",
          message: "senderParticipantId must reference a participant",
          path: ["messages", index, "senderParticipantId"],
        });
      }
    });
  });

export const ChatModuleConfigSchema = z.object({
  showHeader: z.boolean().default(true),
  showStatusBar: z.boolean().default(true),
  showKeyboard: z.boolean().default(false),
  initialScroll: z.enum(["top", "bottom", "preserve"]).default("bottom"),
  messageGrouping: z.enum(["none", "bySender"]).default("bySender"),
  debugShowBounds: z.boolean().default(false),
  behaviorDefaults: JsonObjectSchema.optional(),
});

export type ThemeMode = z.infer<typeof ThemeModeSchema>;
export type ModuleMediaReference = z.infer<typeof ModuleMediaReferenceSchema>;
export type ChatParticipant = z.infer<typeof ChatParticipantSchema>;
export type ChatModuleMessage = z.infer<typeof ChatModuleMessageSchema>;
export type ChatModuleData = z.infer<typeof ChatModuleDataSchema>;
export type ChatModuleConfig = z.infer<typeof ChatModuleConfigSchema>;
