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
      scale: z.number().positive().optional(),
      offsetX: z.number().optional(),
      offsetY: z.number().optional(),
      baseSize: z.number().positive().optional(),
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
  status: JsonObjectSchema.optional(),
  sender: z.object({
    id: IdSchema,
    displayName: z.string().min(1),
    color: z.string().min(1).optional(),
    avatar: z
      .object({
        uri: z.string().min(1),
        scale: z.number().positive().optional(),
        offsetX: z.number().optional(),
        offsetY: z.number().optional(),
        baseSize: z.number().positive().optional(),
      })
      .optional(),
  }),
  media: z
    .object({
      assetId: IdSchema.optional(),
      uri: z.string().min(1).optional(),
      type: z.enum(["image", "video", "audio"]).optional(),
      durationSeconds: z.number().positive().optional(),
      playMode: z.enum(["once", "loop"]).optional(),
      playStartFrame: NonNegativeIntegerSchema.optional(),
      frame: NonNegativeIntegerSchema.optional(),
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
    systemFonts: JsonObjectSchema.optional(),
    colors: JsonObjectSchema,
    wallpaper: JsonObjectSchema.optional(),
    statusBar: JsonObjectSchema,
    navigationBar: JsonObjectSchema,
    keyboard: JsonObjectSchema.optional(),
    layout: JsonObjectSchema,
    header: JsonObjectSchema,
    messages: JsonObjectSchema,
    typography: JsonObjectSchema.optional(),
    chatBubbles: JsonObjectSchema,
    components: JsonObjectSchema.optional(),
    cursor: JsonObjectSchema,
    shadows: JsonObjectSchema.optional(),
    surfaceRelief: JsonObjectSchema.optional(),
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
  statusBar: JsonObjectSchema.optional(),
  navigationBar: JsonObjectSchema.optional(),
  keyboard: JsonObjectSchema.optional(),
  textInputBar: JsonObjectSchema.optional(),
  ownerActor: ResolvedActorSchema,
  header: z.object({
    title: z.string().min(1),
    subtitle: z.string().optional(),
    backgroundColor: z.string().min(1).optional(),
    avatar: z
      .object({
        assetId: IdSchema.optional(),
        uri: z.string().min(1),
        scale: z.number().positive().optional(),
        offsetX: z.number().optional(),
        offsetY: z.number().optional(),
        baseSize: z.number().positive().optional(),
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
  status: JsonObjectSchema.optional(),
  actor: z.object({
    id: IdSchema,
    displayName: z.string().min(1),
    avatarUri: z.string().min(1).optional(),
    avatarScale: z.number().positive().optional(),
    avatarOffsetX: z.number().optional(),
    avatarOffsetY: z.number().optional(),
    avatarBaseSize: z.number().positive().optional(),
    color: z.string().min(1).optional(),
  }),
  media: z
    .object({
      assetId: IdSchema.optional(),
      uri: z.string().min(1).optional(),
      type: z.enum(["image", "video", "audio"]).optional(),
      durationSeconds: z.number().positive().optional(),
      playMode: z.enum(["once", "loop"]).optional(),
      playStartFrame: NonNegativeIntegerSchema.optional(),
      frame: NonNegativeIntegerSchema.optional(),
      window: JsonObjectSchema.optional(),
      transform: JsonObjectSchema.optional(),
    })
    .optional(),
  style: z.object({
    backgroundColor: z.string().min(1),
    textColor: z.string().min(1),
    fontFamily: z.string().min(1),
    fontStyle: z.string().min(1).optional(),
    fontSize: z.number().positive(),
    lineHeight: z.number().positive(),
    fontWeight: z.union([z.string().min(1), z.number().positive()]).optional(),
    borderRadius: z.number().min(0),
    paddingX: z.number().min(0),
    paddingY: z.number().min(0),
    contentMetaGap: z.number().min(0).optional(),
    tailStyle: z.string().min(1),
    tailVerticalPosition: z.enum(["top", "bottom"]),
    tailWidth: z.number().min(0),
    tailHeight: z.number().min(0),
    tailScale: z.number().positive(),
    shadowEnabled: z.boolean(),
    shadow: JsonObjectSchema,
    surfaceRelief: JsonObjectSchema.optional(),
    avatarSize: z.number().min(0),
    avatar: JsonObjectSchema.optional(),
    audioMessage: JsonObjectSchema.optional(),
    videoMessage: JsonObjectSchema.optional(),
    label: JsonObjectSchema.optional(),
    media: JsonObjectSchema.optional(),
    status: JsonObjectSchema.optional(),
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
