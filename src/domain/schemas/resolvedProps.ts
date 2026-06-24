import { z } from "zod";
import {
  IdSchema,
  JsonObjectSchema,
  NonNegativeIntegerSchema,
  PositiveIntegerSchema,
} from "./common.js";

const RectSchema = z.object({
  x: z.number(),
  y: z.number(),
  width: z.number().positive(),
  height: z.number().positive(),
});

const SafeAreaSchema = z.object({
  top: z.number().min(0),
  right: z.number().min(0),
  bottom: z.number().min(0),
  left: z.number().min(0),
});

const ResolvedActorSchema = z.object({
  id: IdSchema,
  displayName: z.string().min(1),
  avatar: z
    .object({
      assetId: IdSchema.optional(),
      uri: z.string().min(1),
    })
    .optional(),
});

const ResolvedMessageTimingSchema = z.object({
  startFrame: NonNegativeIntegerSchema,
  enterDurationFrames: NonNegativeIntegerSchema,
  writeOnStartFrame: NonNegativeIntegerSchema.optional(),
  writeOnDurationFrames: NonNegativeIntegerSchema.optional(),
});

const ResolvedChatMessageSchema = z.object({
  id: IdSchema,
  direction: z.enum(["incoming", "outgoing", "system"]),
  text: z.string(),
  visibleText: z.string(),
  sender: z.object({
    id: IdSchema,
    participantId: IdSchema,
    displayName: z.string().min(1),
  }),
  media: z
    .object({
      assetId: IdSchema.optional(),
      uri: z.string().min(1),
      type: z.enum(["image", "video"]).optional(),
      window: JsonObjectSchema.optional(),
      transform: JsonObjectSchema.optional(),
    })
    .optional(),
  timing: ResolvedMessageTimingSchema,
  style: JsonObjectSchema.optional(),
  layout: JsonObjectSchema.optional(),
  animation: JsonObjectSchema.optional(),
});

const ResolvedScreenEventSchema = z.object({
  id: IdSchema,
  type: z.string().min(1),
  startFrame: NonNegativeIntegerSchema,
  durationFrames: NonNegativeIntegerSchema,
  targetId: IdSchema.nullable(),
  payload: JsonObjectSchema,
});

export const ResolvedChatScreenPropsSchema = z.object({
  frame: NonNegativeIntegerSchema,
  fps: PositiveIntegerSchema,
  screenInstanceId: IdSchema,
  themeMode: z.enum(["light", "dark"]),
  viewport: RectSchema.extend({ safeArea: SafeAreaSchema }),
  theme: z.object({
    id: IdSchema,
    fonts: JsonObjectSchema,
    colors: JsonObjectSchema,
    wallpaper: JsonObjectSchema.optional(),
    statusBar: JsonObjectSchema,
    layout: JsonObjectSchema,
    header: JsonObjectSchema,
    messages: JsonObjectSchema,
    typography: JsonObjectSchema.optional(),
    chatBubbles: JsonObjectSchema,
    avatars: JsonObjectSchema,
    cursor: JsonObjectSchema,
  }),
  device: z.object({
    id: IdSchema,
    osFamily: z.string().min(1),
    pixelRatio: z.number().positive(),
    statusBarHeight: z.number().min(0),
    cornerRadius: z.number().min(0),
    defaultScreenScale: z.number().positive(),
  }),
  deviceState: z.object({
    time: z.string().min(1),
    batteryLevel: z.number().min(0).max(1),
    batteryCharging: z.boolean(),
    signalBars: NonNegativeIntegerSchema,
    networkLabel: z.string(),
    wifiEnabled: z.boolean(),
    wifiIconState: z.string().min(1),
    orientation: z.enum(["portrait", "landscape"]),
    locked: z.boolean(),
  }),
  ownerActor: ResolvedActorSchema,
  header: z.object({
    title: z.string().min(1),
    subtitle: z.string().optional(),
    avatar: z
      .object({
        assetId: IdSchema.optional(),
        uri: z.string().min(1),
      })
      .optional(),
  }),
  messages: z.array(ResolvedChatMessageSchema),
  events: z.array(ResolvedScreenEventSchema),
  props: JsonObjectSchema,
});

export const ResolvedMessageBubblePropsSchema = z.object({
  frame: NonNegativeIntegerSchema,
  fps: PositiveIntegerSchema,
  id: IdSchema,
  direction: z.enum(["incoming", "outgoing", "system"]),
  text: z.string(),
  visibleText: z.string(),
  actor: z.object({
    id: IdSchema,
    displayName: z.string().min(1),
    avatarUri: z.string().min(1).optional(),
  }),
  style: z.object({
    backgroundColor: z.string().min(1),
    textColor: z.string().min(1),
    fontFamily: z.string().min(1),
    fontSize: z.number().positive(),
    lineHeight: z.number().positive(),
    fontWeight: z.union([z.string().min(1), z.number().positive()]).optional(),
    borderRadius: z.number().min(0),
    paddingX: z.number().min(0),
    paddingY: z.number().min(0),
    tailStyle: z.string().min(1),
    tailWidth: z.number().min(0),
    tailHeight: z.number().min(0),
    shadow: JsonObjectSchema,
    avatarSize: z.number().min(0),
  }),
  layout: z.object({
    maxWidth: z.number().positive(),
    alignment: z.enum(["left", "center", "right"]),
    showTail: z.boolean(),
    groupPosition: z.enum(["single", "first", "middle", "last"]),
    avatarGap: z.number().min(0),
  }),
  timing: z.object({
    startFrame: NonNegativeIntegerSchema,
    enterDurationFrames: NonNegativeIntegerSchema,
    writeOnStartFrame: NonNegativeIntegerSchema.nullable(),
    writeOnDurationFrames: NonNegativeIntegerSchema.nullable(),
    exitFrame: NonNegativeIntegerSchema.nullable(),
  }),
  animation: JsonObjectSchema,
});

export type ResolvedChatScreenProps = z.infer<
  typeof ResolvedChatScreenPropsSchema
>;
export type ResolvedMessageBubbleProps = z.infer<
  typeof ResolvedMessageBubblePropsSchema
>;
