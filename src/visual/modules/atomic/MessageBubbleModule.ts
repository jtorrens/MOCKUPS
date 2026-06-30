import type { ResolvedMessageBubbleProps } from "../../../domain/schemas/index.js";
import {
  layoutMessageBubble,
  type MessageBubbleLayout,
} from "../../layout/index.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";
import { AvatarModule } from "./AvatarModule.js";

function animationOpacity(
  animation: Record<string, unknown>,
): number | undefined {
  const enter = animation.enter;
  if (typeof enter !== "object" || enter === null || !("opacity" in enter)) {
    return undefined;
  }
  return typeof enter.opacity === "number" ? enter.opacity : undefined;
}

function cursorBlinkOpacity(frame: number, blinkFrames: number) {
  const cycle = Math.max(1, blinkFrames) * 4;
  return frame % cycle < Math.max(1, blinkFrames) * 3 ? 1 : 0.28;
}

function readRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function readString(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function readNumber(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function colorAlphaIsZero(value: string) {
  if (value.trim().toLowerCase() === "transparent") return true;
  return (
    value
      .trim()
      .match(
        /^rgba\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*(0|0?\.0+)\s*\)$/i,
      ) !== null
  );
}

function maskUrl(value: string) {
  return value ? `url("${value.replace(/"/g, '\\"')}")` : undefined;
}

function cssUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

function hashString(value: string) {
  let hash = 2166136261;
  for (const char of value) {
    hash ^= char.charCodeAt(0);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

function deterministicWaveformValue(seed: number, index: number) {
  const value = Math.sin((seed + index * 97.13) * 0.017) * 43758.5453;
  return value - Math.floor(value);
}

function formatDuration(seconds: number) {
  const safeSeconds = Math.max(0, Math.round(seconds));
  const minutes = Math.floor(safeSeconds / 60);
  const remainder = safeSeconds % 60;
  return `${minutes}:${String(remainder).padStart(2, "0")}`;
}

function formatRemainingDuration(seconds: number) {
  const safeSeconds = Math.max(0, Math.ceil(seconds));
  const minutes = Math.floor(safeSeconds / 60);
  const remainder = safeSeconds % 60;
  return `${minutes}:${String(remainder).padStart(2, "0")}`;
}

function unionBoxForNodes(nodes: RenderableNode[]) {
  const boxes = nodes
    .map((node) => node.box)
    .filter((box): box is NonNullable<RenderableNode["box"]> => Boolean(box));
  if (!boxes.length) return undefined;
  const left = Math.min(...boxes.map((box) => box.x));
  const top = Math.min(...boxes.map((box) => box.y));
  const right = Math.max(...boxes.map((box) => box.x + box.width));
  const bottom = Math.max(...boxes.map((box) => box.y + box.height));
  return {
    x: left,
    y: top,
    width: right - left,
    height: bottom - top,
  };
}

function messageAudioChildren(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode[] {
  if (!input.media || !layout.mediaBox) return [];
  const audioStyle = readRecord(input.style.audioMessage);
  const box = layout.mediaBox;
  const durationSeconds = Math.max(1, readNumber(input.media.durationSeconds, 8));
  const playFrame = readNumber(input.media.frame, 0);
  const totalPlayFrames = durationSeconds * input.fps;
  const progress = Math.max(
    0,
    Math.min(1, playFrame / totalPlayFrames),
  );
  const isPlaying = playFrame > 0 && playFrame < totalPlayFrames;
  const displayedDurationText = isPlaying
    ? formatRemainingDuration(durationSeconds - playFrame / input.fps)
    : progress >= 1
      ? "0:00"
      : formatDuration(durationSeconds);
  const avatarSize = Math.max(0, readNumber(audioStyle.avatarSize, 0));
  const avatarGap = Math.max(0, readNumber(audioStyle.avatarGap, 8));
  const avatarPosition = readString(audioStyle.avatarPosition, "left") === "right"
    ? "right"
    : "left";
  const playCircleSize = Math.min(
    box.height,
    Math.max(1, readNumber(audioStyle.playCircleSize, 32)),
  );
  const badgeSize = Math.max(1, readNumber(audioStyle.microphoneBadgeSize, 16));
  const sidePadding = Math.max(6, Math.round(box.height * 0.16));
  const verticalCenter = box.y + box.height / 2;
  const avatarX =
    avatarPosition === "right"
      ? box.x + box.width - sidePadding - avatarSize
      : box.x + sidePadding;
  const contentStart =
    avatarPosition === "right"
      ? box.x + sidePadding
      : avatarX + avatarSize + avatarGap;
  const contentEnd =
    avatarPosition === "right"
      ? avatarX - avatarGap
      : box.x + box.width - sidePadding;
  const playX = contentStart;
  const playY = Math.round(verticalCenter - playCircleSize / 2);
  const textSize = Math.max(1, readNumber(audioStyle.textSize, 11));
  const durationText = displayedDurationText;
  const durationWidth = Math.ceil(durationText.length * textSize * 0.58);
  const itemGap = avatarGap;
  const waveformStart = playX + playCircleSize + itemGap;
  const waveformEnd = Math.max(
    waveformStart + 1,
    contentEnd - durationWidth - itemGap,
  );
  const progressKnobSize = Math.max(
    5,
    readNumber(audioStyle.progressKnobSize, 9),
  );
  const barCount = Math.max(4, Math.round(readNumber(audioStyle.waveformBarCount, 28)));
  const waveformGap = Math.max(0, readNumber(audioStyle.waveformGap, 2));
  const availableWaveformWidth = Math.max(1, waveformEnd - waveformStart);
  const barWidth = Math.max(
    1,
    Math.floor((availableWaveformWidth - waveformGap * (barCount - 1)) / barCount),
  );
  const actualWaveformEnd =
    waveformStart + (barCount - 1) * (barWidth + waveformGap) + barWidth;
  const firstBarCenter = waveformStart + barWidth / 2;
  const lastBarCenter = actualWaveformEnd - barWidth / 2;
  const minHeight = Math.max(1, readNumber(audioStyle.waveformMinHeight, 4));
  const maxHeight = Math.max(minHeight, readNumber(audioStyle.waveformMaxHeight, 22));
  const playedBars = Math.floor(barCount * progress);
  const seed = hashString(input.id);
  const children: RenderableNode[] = [];

  if (input.actor.avatarUri && avatarSize > 0) {
    children.push({
      ...AvatarModule.render({
        id: `${input.id}:audio:avatar`,
        uri: input.actor.avatarUri,
        size: avatarSize,
        label: input.actor.displayName,
        frame: input.frame,
        cornerRadius: Math.round(avatarSize * 0.5),
        ...(input.actor.avatarScale !== undefined
          ? { imageScale: input.actor.avatarScale }
          : {}),
        ...(input.actor.avatarOffsetX !== undefined
          ? { imageOffsetX: input.actor.avatarOffsetX }
          : {}),
        ...(input.actor.avatarOffsetY !== undefined
          ? { imageOffsetY: input.actor.avatarOffsetY }
          : {}),
        ...(input.actor.avatarBaseSize !== undefined
          ? { imageBaseSize: input.actor.avatarBaseSize }
          : {}),
      }),
      box: {
        x: Math.round(avatarX),
        y: Math.round(verticalCenter - avatarSize / 2),
        width: Math.round(avatarSize),
        height: Math.round(avatarSize),
      },
    });
    children.push({
      id: `${input.id}:audio:mic-badge`,
      type: "message_bubble_audio_badge",
      role: "microphone_badge",
      frame: input.frame,
      box: {
        x: Math.round(avatarX + avatarSize - badgeSize * 0.85),
        y: Math.round(verticalCenter + avatarSize / 2 - badgeSize * 0.95),
        width: Math.round(badgeSize),
        height: Math.round(badgeSize),
      },
      style: {
        backgroundColor: readString(audioStyle.playCircleColor, "#007AFF"),
        borderRadius: Math.round(badgeSize / 2),
      },
      children: [
        {
          id: `${input.id}:audio:mic-badge:icon`,
          type: "message_bubble_audio_badge_icon",
          role: "microphone",
          frame: input.frame,
          box: {
            x: Math.round(avatarX + avatarSize - badgeSize * 0.69),
            y: Math.round(verticalCenter + avatarSize / 2 - badgeSize * 0.79),
            width: Math.round(badgeSize * 0.68),
            height: Math.round(badgeSize * 0.68),
          },
          text: "MIC",
          style: {
            color: readString(audioStyle.playIconColor, "#FFFFFF"),
            ...(readString(audioStyle.microphoneBadgeIconUri)
              ? {
                  maskImage: maskUrl(readString(audioStyle.microphoneBadgeIconUri)),
                  WebkitMaskImage: maskUrl(readString(audioStyle.microphoneBadgeIconUri)),
                }
              : {}),
          },
          metadata: {
            token: readString(audioStyle.microphoneBadgeIconToken, "media_mic"),
          },
        },
      ],
    });
  }

  children.push({
    id: `${input.id}:audio:play-circle`,
    type: "message_bubble_audio_play",
    role: "play",
    frame: input.frame,
    box: {
      x: Math.round(playX),
      y: playY,
      width: Math.round(playCircleSize),
      height: Math.round(playCircleSize),
    },
    text: isPlaying ? "Ⅱ" : "▶",
    style: {
      alignItems: "center",
      backgroundColor: readString(audioStyle.playCircleColor, "#007AFF"),
      borderRadius: Math.round(playCircleSize / 2),
      color: readString(audioStyle.playIconColor, "#FFFFFF"),
      display: "flex",
      fontSize: Math.round(playCircleSize * 0.44),
      justifyContent: "center",
      lineHeight: playCircleSize,
      paddingLeft: isPlaying ? 0 : Math.round(playCircleSize * 0.07),
    },
  });

  for (let index = 0; index < barCount; index += 1) {
    const normalized = deterministicWaveformValue(seed, index);
    const height = minHeight + normalized * (maxHeight - minHeight);
    const x = waveformStart + index * (barWidth + waveformGap);
    children.push({
      id: `${input.id}:audio:waveform:${index}`,
      type: "message_bubble_audio_waveform_bar",
      role: index < playedBars ? "played" : "unplayed",
      frame: input.frame,
      box: {
        x: Math.round(x),
        y: Math.round(verticalCenter - height / 2),
        width: Math.round(barWidth),
        height: Math.round(height),
      },
      style: {
        backgroundColor:
          index < playedBars
            ? readString(audioStyle.waveformPlayedColor, "#007AFF")
            : readString(audioStyle.waveformColor, "#8E8E93"),
        borderRadius: Math.max(1, Math.round(barWidth / 2)),
      },
    });
  }

  children.push({
    id: `${input.id}:audio:progress-knob`,
    type: "message_bubble_audio_progress_knob",
    role: "progress_knob",
    frame: input.frame,
    box: {
      x: Math.round(
        firstBarCenter +
          (lastBarCenter - firstBarCenter) * progress -
          progressKnobSize / 2,
      ),
      y: Math.round(verticalCenter - progressKnobSize / 2),
      width: progressKnobSize,
      height: progressKnobSize,
    },
    style: {
      backgroundColor: readString(audioStyle.playCircleColor, "#007AFF"),
      borderRadius: Math.round(progressKnobSize / 2),
    },
  });

  children.push({
    id: `${input.id}:audio:duration`,
    type: "message_bubble_audio_duration",
    role: "duration",
    frame: input.frame,
    box: {
      x: Math.round(contentEnd - durationWidth),
      y: Math.round(verticalCenter - textSize * 0.6),
      width: durationWidth,
      height: Math.round(textSize * 1.25),
    },
    text: durationText,
    style: {
      color: readString(audioStyle.textColor, "#8E8E93"),
      fontFamily: input.style.fontFamily,
      fontSize: textSize,
      lineHeight: Math.round(textSize * 1.25),
      whiteSpace: "nowrap",
    },
  });

  return children;
}

function messageMediaNode(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode | undefined {
  if (!input.media || !layout.mediaBox) return undefined;
  const mediaStyle = readRecord(input.style.media);
  const transform = readRecord(input.media.transform);
  const mediaType = readString(input.media.type, "image");
  const videoMessageStyle = readRecord(input.style.videoMessage);
  const scale = Math.max(0.01, readNumber(transform.scale, 1));
  const translateX = readNumber(transform.translateX, 0);
  const translateY = readNumber(transform.translateY, 0);
  const borderWidth = Math.max(0, readNumber(mediaStyle.borderWidth, 0));
  const borderColor = readString(mediaStyle.borderColor, "transparent");
  const cornerRadius = Math.max(
    0,
    readNumber(mediaStyle.cornerRadius, input.style.borderRadius),
  );
  const shadowEnabled = mediaStyle.shadowEnabled === true;
  const mediaShadow = readRecord(mediaStyle.shadow);
  const surfaceReliefEnabled = mediaStyle.surfaceReliefEnabled === true;
  const mediaSurfaceRelief = surfaceReliefEnabled
    ? readRecord(mediaStyle.surfaceRelief)
    : {};
  const mediaFrame = Math.max(0, readNumber(input.media.frame, input.frame));
  const componentContainerShadow =
    (mediaType === "audio" || mediaType === "video") && shadowEnabled
      ? Object.keys(mediaShadow).length
        ? mediaShadow
        : input.style.shadow
      : {};
  const componentContainerRelief =
    mediaType === "audio" || mediaType === "video" ? mediaSurfaceRelief : {};
  const showVideoPlayOverlay =
    mediaType === "video" &&
    videoMessageStyle.playOverlayEnabled !== false &&
    mediaFrame <= 0;
  const videoPlayCircleSize = Math.max(
    1,
    readNumber(videoMessageStyle.playCircleSize, 44),
  );
  const videoDurationSeconds = Math.max(
    1,
    readNumber(input.media.durationSeconds, 8),
  );
  const videoTotalPlayFrames = Math.max(1, videoDurationSeconds * input.fps);
  const videoProgress = Math.max(
    0,
    Math.min(1, mediaFrame / videoTotalPlayFrames),
  );
  const isVideoPlaying =
    mediaType === "video" &&
    mediaFrame > 0 &&
    mediaFrame < videoTotalPlayFrames;
  const videoStatusSize = Math.max(
    1,
    readNumber(videoMessageStyle.statusSize, 12),
  );
  const videoStatusLineHeight = Math.round(videoStatusSize * 1.22);
  const videoStatusPaddingX = Math.max(
    0,
    readNumber(videoMessageStyle.statusPaddingX, 8),
  );
  const videoStatusPaddingY = Math.max(
    0,
    readNumber(videoMessageStyle.statusPaddingY, 7),
  );
  const videoStatusGap = Math.max(
    0,
    readNumber(videoMessageStyle.statusGap, 4),
  );
  const videoStatusColor = readString(
    videoMessageStyle.statusColor,
    readString(videoMessageStyle.playCircleColor, "#007AFF"),
  );
  const videoDurationText =
    mediaType === "video"
      ? isVideoPlaying
        ? formatRemainingDuration(videoDurationSeconds - mediaFrame / input.fps)
        : videoProgress >= 1
          ? "0:00"
          : formatDuration(videoDurationSeconds)
      : "";
  const videoDurationWidth = Math.ceil(
    videoDurationText.length * videoStatusSize * 0.58,
  );
  const mediaImage: RenderableNode = {
    id: `${input.id}:media:image`,
    type: "message_bubble_media_image",
    role: mediaType,
    frame: mediaFrame,
    box: layout.mediaBox,
    style: {
      backgroundColor: "#000000",
      backgroundImage:
        mediaType === "video" || mediaType === "audio" || !input.media.uri
          ? undefined
          : cssUrl(input.media.uri),
      backgroundPosition: `calc(50% + ${translateX}px) calc(50% + ${translateY}px)`,
      backgroundRepeat: "no-repeat",
      backgroundSize: `${scale * 100}%`,
      borderRadius: cornerRadius,
      overflow: "hidden",
      zIndex: 1,
      shadow:
        mediaType !== "audio" && mediaType !== "video" && shadowEnabled
          ? Object.keys(mediaShadow).length
            ? mediaShadow
            : input.style.shadow
          : {},
    },
    metadata: {
      uri: input.media.uri,
      type: mediaType,
      playMode: input.media.playMode ?? "once",
      playStartFrame: input.media.playStartFrame ?? 0,
      frame: mediaFrame,
      fps: input.fps,
      scale,
      translateX,
      translateY,
      transform: input.media.transform,
      window: input.media.window,
    },
  };
  const mediaBorder: RenderableNode | undefined =
    borderWidth > 0 && borderColor !== "transparent"
      ? {
          id: `${input.id}:media:border`,
          type: "message_bubble_media_border",
          role: "border",
          frame: input.frame,
          box: layout.mediaBox,
          style: {
            borderColor,
            borderRadius: cornerRadius,
            borderWidth,
            pointerEvents: "none",
            zIndex: 4,
          },
        }
      : undefined;
  const videoStatusNode: RenderableNode | undefined =
    mediaType === "video" && videoMessageStyle.statusVisible !== false
      ? {
          id: `${input.id}:media:video-status`,
          type: "message_bubble_video_status",
          role: "video_status",
          frame: input.frame,
          box: {
            x: layout.mediaBox.x + videoStatusPaddingX,
            y: layout.mediaBox.y + videoStatusPaddingY,
            width: Math.max(1, layout.mediaBox.width - videoStatusPaddingX * 2),
            height: videoStatusLineHeight,
          },
          style: {
            color: videoStatusColor,
            fontFamily: input.style.fontFamily,
            fontSize: videoStatusSize,
            lineHeight: videoStatusLineHeight,
            gap: videoStatusGap,
            zIndex: 2,
          },
          children: [
            {
              id: `${input.id}:media:video-status:icon`,
              type: "message_bubble_video_status_icon",
              role: "video_icon",
              frame: input.frame,
              box: {
                x: layout.mediaBox.x + videoStatusPaddingX,
                y: Math.round(
                  layout.mediaBox.y +
                    videoStatusPaddingY +
                    (videoStatusLineHeight - videoStatusSize) / 2,
                ),
                width: videoStatusSize,
                height: videoStatusSize,
              },
              text: "VIDEO",
              style: {
                color: videoStatusColor,
                ...(readString(videoMessageStyle.statusIconUri)
                  ? {
                      maskImage: maskUrl(
                        readString(videoMessageStyle.statusIconUri),
                      ),
                      WebkitMaskImage: maskUrl(
                        readString(videoMessageStyle.statusIconUri),
                      ),
                    }
                  : {}),
              },
              metadata: {
                token: readString(
                  videoMessageStyle.statusIconToken,
                  "media_video",
                ),
              },
            },
            {
              id: `${input.id}:media:video-status:duration`,
              type: "message_bubble_video_status_duration",
              role: "duration",
              frame: input.frame,
              box: {
                x: Math.round(
                  layout.mediaBox.x +
                    layout.mediaBox.width -
                    videoStatusPaddingX -
                    videoDurationWidth,
                ),
                y: layout.mediaBox.y + videoStatusPaddingY,
                width: videoDurationWidth,
                height: videoStatusLineHeight,
              },
              text: videoDurationText,
              style: {
                color: videoStatusColor,
                fontFamily: input.style.fontFamily,
                fontSize: videoStatusSize,
                lineHeight: videoStatusLineHeight,
                textAlign: "right",
                whiteSpace: "nowrap",
              },
            },
          ],
        }
      : undefined;

  return {
    id: `${input.id}:media`,
    type: "message_bubble_media",
    role: mediaType,
    frame: input.frame,
    box: layout.mediaBox,
    style: {
      backgroundColor:
        mediaType === "audio"
          ? input.style.backgroundColor
          : mediaType === "video"
            ? "#000000"
            : "transparent",
      borderRadius: cornerRadius,
      overflow: mediaType === "video" ? "hidden" : "visible",
      shadow: componentContainerShadow,
      surfaceRelief: componentContainerRelief,
    },
    children:
      mediaType === "audio"
        ? [
            ...messageAudioChildren(input, layout),
            ...(mediaBorder ? [mediaBorder] : []),
          ]
        : [
            mediaImage,
            ...(videoStatusNode ? [videoStatusNode] : []),
            ...(showVideoPlayOverlay
              ? [
                  {
                    id: `${input.id}:media:video-play-overlay`,
                    type: "message_bubble_video_play_overlay",
                    role: "play_overlay",
                    frame: input.frame,
                    box: {
                      x: Math.round(
                        layout.mediaBox.x +
                          layout.mediaBox.width / 2 -
                          videoPlayCircleSize / 2,
                      ),
                      y: Math.round(
                        layout.mediaBox.y +
                          layout.mediaBox.height / 2 -
                          videoPlayCircleSize / 2,
                      ),
                      width: Math.round(videoPlayCircleSize),
                      height: Math.round(videoPlayCircleSize),
                    },
                    text: "▶",
                    style: {
                      backgroundColor: readString(
                        videoMessageStyle.playCircleColor,
                        "rgba(0, 0, 0, 0.55)",
                      ),
                      borderRadius: Math.round(videoPlayCircleSize / 2),
                      color: readString(
                        videoMessageStyle.playIconColor,
                        "#FFFFFF",
                      ),
                      fontSize: Math.round(videoPlayCircleSize * 0.44),
                      lineHeight: Math.round(videoPlayCircleSize),
                      paddingLeft: Math.round(videoPlayCircleSize * 0.07),
                      zIndex: 3,
                    },
                  },
                ]
              : []),
            ...(mediaBorder ? [mediaBorder] : []),
          ],
  };
}

function messageStatusNode(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode | undefined {
  const status = readRecord(input.status);
  const statusStyle = readRecord(input.style.status);
  const deliveryStatus = readString(status.deliveryStatus, "none");
  const text = readString(status.text);
  const showText = statusStyle.showText !== false && text.trim().length > 0;
  const showTicks =
    input.direction === "outgoing" &&
    statusStyle.showTicks !== false &&
    deliveryStatus !== "none";
  if (!showText && !showTicks) return undefined;
  if (!layout.statusBox) return undefined;

  const size = readNumber(statusStyle.size, 11);
  const gap = readNumber(statusStyle.gap, 3);
  const offsetX = readNumber(statusStyle.offsetX, -8);
  const offsetY = readNumber(statusStyle.offsetY, -5);
  const lineHeight = Math.round(size * 1.15);
  const textWidth = showText ? Math.ceil(Array.from(text).length * size * 0.54) : 0;
  const isDoubleTick = deliveryStatus === "delivered" || deliveryStatus === "read";
  const tickWidth = showTicks ? Math.ceil(size * (isDoubleTick ? 1.45 : 1)) : 0;
  const totalWidth =
    textWidth + tickWidth + (showText && showTicks ? Math.max(0, gap) : 0);
  const statusBox = layout.statusBox;
  const children: RenderableNode[] = [];
  let cursorX = statusBox.x;
  if (showText) {
    children.push({
      id: `${input.id}:status:text`,
      type: "message_bubble_status_text",
      role: "status_text",
      frame: input.frame,
      box: {
        x: cursorX,
        y: statusBox.y,
        width: Math.max(1, textWidth),
        height: lineHeight,
      },
      text,
      style: {
        color: readString(statusStyle.textColor, input.style.textColor),
        fontFamily: input.style.fontFamily,
        fontStyle: input.style.fontStyle,
        fontSize: size,
        lineHeight,
        fontWeight: input.style.fontWeight,
      },
    });
    cursorX += textWidth + (showTicks ? Math.max(0, gap) : 0);
  }
  if (showTicks) {
    const iconUri = readString(
      isDoubleTick ? statusStyle.tickDoubleIconUri : statusStyle.tickSingleIconUri,
    );
    const token = readString(
      isDoubleTick ? statusStyle.tickDoubleIconToken : statusStyle.tickSingleIconToken,
      isDoubleTick ? "message_done_all" : "message_check",
    );
    children.push({
      id: `${input.id}:status:ticks`,
      type: "message_bubble_status_icon",
      role: deliveryStatus,
      frame: input.frame,
      box: {
        x: cursorX,
        y: statusBox.y + Math.round((lineHeight - size) / 2),
        width: Math.max(1, tickWidth),
        height: size,
      },
      text: isDoubleTick ? "✓✓" : "✓",
      style: {
        color: readString(statusStyle.tickColor, input.style.textColor),
        fontSize: size,
        lineHeight: size,
        ...(iconUri
          ? {
              maskImage: maskUrl(iconUri),
              WebkitMaskImage: maskUrl(iconUri),
            }
          : {}),
      },
      metadata: {
        token,
        deliveryStatus,
      },
    });
  }
  return {
    id: `${input.id}:status`,
    type: "message_bubble_status",
    role: deliveryStatus,
    frame: input.frame,
    box: statusBox,
    style: {
      backgroundColor: "transparent",
      overflow: "visible",
    },
    children,
    metadata: {
      anchor: "bubble.bottomRight",
      offsetX,
      offsetY,
      deliveryStatus,
    },
  };
}

function tailNode(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode | undefined {
  if (!input.layout.showTail || input.direction === "system") return undefined;
  if (input.style.tailStyle === "none") return undefined;
  const width = Math.round(input.style.tailWidth);
  const height = Math.round(input.style.tailHeight);
  if (width <= 0 || height <= 0) return undefined;
  const side = input.direction === "outgoing" ? "right" : "left";
  const vertical = input.style.tailVerticalPosition;
  const alignTailToBubbleEdge = input.style.tailStyle === "cut_corner";
  const x =
    side === "right"
      ? layout.bubbleBox.x + layout.bubbleBox.width - Math.ceil(width * 0.34)
      : layout.bubbleBox.x - Math.floor(width * 0.66);
  const y =
    vertical === "top"
      ? alignTailToBubbleEdge
        ? layout.bubbleBox.y
        : layout.bubbleBox.y + Math.round(input.style.borderRadius * 0.35)
      : alignTailToBubbleEdge
        ? layout.bubbleBox.y + layout.bubbleBox.height - height
        : layout.bubbleBox.y +
          layout.bubbleBox.height -
          height -
          Math.round(input.style.borderRadius * 0.18);
  return {
    id: `${input.id}:tail`,
    type: "message_bubble_tail",
    role: `${side}_${vertical}`,
    frame: input.frame,
    box: { x, y, width, height },
        style: {
          backgroundColor: input.style.backgroundColor,
          tailStyle: input.style.tailStyle,
          side,
          vertical,
          borderRadius: Math.max(1, Math.round(input.style.borderRadius * 0.35)),
        },
    metadata: {
      side,
      vertical,
      style: input.style.tailStyle,
    },
  };
}

function tailCornerExtensionNode(
  input: ResolvedMessageBubbleProps,
  tail: RenderableNode | undefined,
  layout: MessageBubbleLayout,
): RenderableNode | undefined {
  if (!tail) return undefined;
  const tailBox = tail.box;
  if (!tailBox) return undefined;
  const side = input.direction === "outgoing" ? "right" : "left";
  const radius = Math.max(0, Math.round(input.style.borderRadius));
  if (radius <= 0) return undefined;
  const targetLeft = layout.bubbleBox.x + layout.bubbleBox.width - radius;
  const targetRight = layout.bubbleBox.x + radius;
  const tailLeft = tailBox.x;
  const tailRight = tailBox.x + tailBox.width;
  const box =
    side === "right"
      ? tailLeft > targetLeft
        ? {
            x: targetLeft,
            y: tailBox.y,
            width: tailLeft - targetLeft,
            height: tailBox.height,
          }
        : undefined
      : tailRight < targetRight
        ? {
            x: tailRight,
            y: tailBox.y,
            width: targetRight - tailRight,
            height: tailBox.height,
          }
        : undefined;
  if (!box || box.width <= 0 || box.height <= 0) return undefined;
  return {
    id: `${input.id}:tail:corner-extension`,
    type: "message_bubble_tail_extension",
    role: tail.role,
    frame: input.frame,
    box,
    style: {
      backgroundColor: input.style.backgroundColor,
    },
  };
}

function messageLabelNode(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode | undefined {
  const labelStyle = readRecord(input.style.label);
  if (labelStyle.visible !== true || !layout.labelBox) return undefined;
  const borderWidth = readNumber(labelStyle.borderWidth, 0);
  const paddingX = readNumber(labelStyle.paddingX, 8);
  const paddingY = readNumber(labelStyle.paddingY, 4);
  const text = input.actor.displayName;
  const fontSize = readNumber(labelStyle.fontSize, input.style.fontSize * 0.78);
  return {
    id: `${input.id}:label`,
    type: "message_bubble_label",
    role: "actor_label",
    frame: input.frame,
    box: layout.labelBox,
    text,
    style: {
      backgroundColor:
        labelStyle.backgroundVisible === false
          ? "transparent"
          : readString(labelStyle.backgroundColor, input.style.backgroundColor),
      borderRadius: readNumber(labelStyle.cornerRadius, 0),
      borderColor: readString(labelStyle.borderColor, "transparent"),
      borderWidth,
      shadow: labelStyle.shadowEnabled === true ? readRecord(labelStyle.shadow) : {},
      surfaceRelief:
        labelStyle.surfaceReliefEnabled === true
          ? readRecord(labelStyle.surfaceRelief)
          : {},
      overflow: "visible",
      textColor: readString(labelStyle.textColor, input.style.textColor),
      fontFamily: readString(labelStyle.fontFamily, input.style.fontFamily),
      fontStyle: readString(labelStyle.fontStyle, input.style.fontStyle),
      fontSize,
      lineHeight: readNumber(labelStyle.lineHeight, Math.round(fontSize * 1.2)),
      fontWeight: labelStyle.fontWeight ?? 400,
      textAlign: "center",
      paddingX,
      paddingY,
    },
  };
}

export function renderMessageBubbleWithLayout(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode {
    const hasShapeShadow =
      input.style.shadowEnabled && !colorAlphaIsZero(input.style.backgroundColor);
    const cursorConfig =
      typeof input.animation.cursor === "object" && input.animation.cursor !== null
        ? (input.animation.cursor as Record<string, unknown>)
        : {};
    const cursorVisible = cursorConfig.visible === true;
    const cursorBlinkFrames =
      typeof cursorConfig.blinkFrames === "number" && cursorConfig.blinkFrames > 0
        ? cursorConfig.blinkFrames
        : 15;
    const cursorOpacity = cursorBlinkOpacity(input.frame, cursorBlinkFrames);
    const tail = tailNode(input, layout);
    const shapeChildren: RenderableNode[] = [
      {
        id: `${input.id}:shape:body`,
        type: "message_bubble_body",
        role: input.direction,
        frame: input.frame,
        box: {
          x: layout.bubbleBox.x,
          y: layout.bubbleBox.y,
          width: layout.bubbleBox.width,
          height: layout.bubbleBox.height,
        },
        style: {
          backgroundColor: input.style.backgroundColor,
          borderRadius: input.style.borderRadius,
        },
      },
    ];
    const tailExtension = tailCornerExtensionNode(input, tail, layout);
    if (tailExtension) {
      shapeChildren.push(tailExtension);
    }
    if (tail) {
      shapeChildren.push(tail);
    }
    const shapeBox = unionBoxForNodes(shapeChildren) ?? layout.bubbleBox;
    const children: RenderableNode[] = [
      {
        id: `${input.id}:shape`,
        type: "message_bubble_shape",
        role: input.direction,
        frame: input.frame,
        box: shapeBox,
        style: {
          shadow: hasShapeShadow ? input.style.shadow : {},
          surfaceRelief: input.style.surfaceRelief,
          borderColor: readString(input.style.borderColor, "transparent"),
          borderWidth: readNumber(input.style.borderWidth, 0),
        },
        children: shapeChildren,
      },
    ];
    if (input.actor.avatarUri && layout.avatarBox) {
      const avatarStyle = readRecord(input.style.avatar);
      const avatarShadowEnabled = avatarStyle.shadowEnabled === true;
      children.push(
        {
          ...AvatarModule.render({
            id: `${input.id}:avatar`,
            uri: input.actor.avatarUri,
            size: input.style.avatarSize,
            label: input.actor.displayName,
            frame: input.frame,
            cornerRadius: readNumber(
              avatarStyle.cornerRadius,
              Math.round(input.style.avatarSize * 0.22),
            ),
            borderWidth: readNumber(avatarStyle.borderWidth, 0),
            borderColor: readString(avatarStyle.borderColor, "transparent"),
            ...(input.actor.avatarScale !== undefined
              ? { imageScale: input.actor.avatarScale }
              : {}),
            ...(input.actor.avatarOffsetX !== undefined
              ? { imageOffsetX: input.actor.avatarOffsetX }
              : {}),
            ...(input.actor.avatarOffsetY !== undefined
              ? { imageOffsetY: input.actor.avatarOffsetY }
              : {}),
            ...(input.actor.avatarBaseSize !== undefined
              ? { imageBaseSize: input.actor.avatarBaseSize }
              : {}),
            ...(avatarShadowEnabled
              ? { shadow: readRecord(avatarStyle.shadow) }
              : {}),
            ...(avatarStyle.surfaceRelief
              ? { surfaceRelief: readRecord(avatarStyle.surfaceRelief) }
              : {}),
          }),
          box: layout.avatarBox,
        },
      );
    }
    const media = messageMediaNode(input, layout);
    if (media) {
      children.push(media);
    }
    const status = messageStatusNode(input, layout);
    if (status) {
      children.push(status);
    }
    children.push({
      id: `${input.id}:text`,
      type: "text",
      role: "message_text",
      frame: input.frame,
      box: layout.textBox,
      text: input.visibleText,
      style: {
        color: input.style.textColor,
        fontFamily: input.style.fontFamily,
        fontStyle: input.style.fontStyle,
        fontSize: input.style.fontSize,
        lineHeight: input.style.lineHeight,
        fontWeight: input.style.fontWeight,
        ...(input.direction === "system" ? { textAlign: "center" } : {}),
      },
      children:
        cursorVisible && input.direction === "outgoing"
          ? [
              {
                id: `${input.id}:text:cursor`,
                type: "message_text_cursor",
                role: "cursor",
                frame: input.frame,
                style: {
                  background:
                    typeof cursorConfig.color === "string"
                      ? cursorConfig.color
                      : input.style.textColor,
                  width:
                    typeof cursorConfig.width === "number"
                      ? cursorConfig.width
                      : 2,
                  opacity: cursorOpacity,
                },
              },
            ]
          : undefined,
    });
    const label = messageLabelNode(input, layout);
    if (label) {
      children.push(label);
    }

    return {
      id: input.id,
      type: "message_bubble",
      role: input.direction,
      frame: input.frame,
      box: layout.bubbleBox,
      transform: {
        opacity: animationOpacity(input.animation),
      },
      style: {
        backgroundColor: "transparent",
        textColor: input.style.textColor,
        fontFamily: input.style.fontFamily,
        fontStyle: input.style.fontStyle,
        fontSize: input.style.fontSize,
        lineHeight: input.style.lineHeight,
        fontWeight: input.style.fontWeight,
        borderRadius: input.style.borderRadius,
        paddingX: input.style.paddingX,
        paddingY: input.style.paddingY,
        showTail: input.layout.showTail,
        tailStyle: input.style.tailStyle,
        tailVerticalPosition: input.style.tailVerticalPosition,
        tailWidth: input.style.tailWidth,
        tailHeight: input.style.tailHeight,
        shadow: {},
      },
      text: input.visibleText,
      children,
      metadata: {
        actorId: input.actor.id,
        fullText: input.text,
        timing: input.timing,
        animation: input.animation,
        layoutIntent: input.layout,
        avatarGap: input.layout.avatarGap,
        tailGeometry: {
          style: input.style.tailStyle,
          width: input.style.tailWidth,
          height: input.style.tailHeight,
          verticalPosition: input.style.tailVerticalPosition,
          path: "not_computed_renderer_agnostic_stub",
        },
        measurement: layout.measurement,
        maxBubbleWidth: layout.maxBubbleWidth,
        alignment: layout.alignment,
        tokenSources: {
          style: "theme.tokens_json.chatBubbles/typography",
          layout: "theme.tokens_json.chatBubbles",
        },
      },
    };
}

export const MessageBubbleModule: VisualModule<ResolvedMessageBubbleProps> = {
  type: "message_bubble",
  version: 1,
  render(input): RenderableNode {
    const avatarReserve =
      input.direction === "incoming" && input.actor.avatarUri
        ? input.style.avatarSize + input.layout.avatarGap
        : 0;
    const layout = layoutMessageBubble({
      props: input,
      messageArea: {
        x: 0,
        y: 0,
        width: input.layout.maxWidth + avatarReserve,
        height: Number.MAX_SAFE_INTEGER,
      },
      y: 0,
    });
    return renderMessageBubbleWithLayout(input, layout);
  },
};
