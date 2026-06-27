import type { ResolvedChatScreenProps } from "../../../domain/schemas/index.js";
import {
  readNumber,
  readObject,
  readString,
} from "../../renderable/helpers.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";

export interface TextInputBarModuleInput {
  frame: number;
  textInputBar: ResolvedChatScreenProps["textInputBar"];
  tokens: ResolvedChatScreenProps["theme"];
  viewport: ResolvedChatScreenProps["viewport"];
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function items(value: unknown) {
  return Array.isArray(value)
    ? value.map(asRecord).sort((left, right) => {
        return readNumber(left, "order", 0) - readNumber(right, "order", 0);
      })
    : [];
}

function maskUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

function itemNode({
  item,
  frame,
  color,
  iconSize,
}: {
  item: Record<string, unknown>;
  frame: number;
  color: string;
  iconSize: number;
}): RenderableNode {
  const token = readString(item, "token", readString(item, "label", ""));
  const iconUri = readString(item, "iconUri", "");
  const itemColor = readString(item, "color", color);
  return {
    id: `text-input-bar:item:${readString(item, "id", token)}`,
    type: "text_input_bar_item",
    role: "icon",
    frame,
    text: token,
    style: {
      color: itemColor,
      fontSize: iconSize,
      lineHeight: iconSize,
      ...(iconUri
        ? {
            maskImage: maskUrl(iconUri),
            WebkitMaskImage: maskUrl(iconUri),
          }
        : {}),
    },
    metadata: {
      ...item,
    },
  };
}

function cursorBlinkOpacity(frame: number, blinkFrames: number) {
  const cycle = Math.max(1, blinkFrames) * 4;
  return frame % cycle < Math.max(1, blinkFrames) * 3 ? 1 : 0.28;
}

export const TextInputBarModule: VisualModule<TextInputBarModuleInput> = {
  type: "text_input_bar",
  version: 1,
  render(input) {
    const config = asRecord(input.textInputBar);
    const layout = asRecord(config.layout);
    const colors = readObject(input.tokens, "colors");
    const fonts = readObject(input.tokens, "fonts");
    const cursorTokens = readObject(input.tokens, "cursor");
    const keyboardTokens = readObject(input.tokens, "keyboard");
    const height = readNumber(layout, "height", 56);
    const paddingX = readNumber(layout, "paddingX", 8);
    const paddingY = readNumber(layout, "paddingY", 6);
    const gap = readNumber(layout, "gap", 8);
    const fieldHeight = readNumber(layout, "fieldHeight", 40);
    const fieldPaddingX = readNumber(layout, "fieldPaddingX", 14);
    const fieldPaddingY = readNumber(layout, "fieldPaddingY", 6);
    const fieldRadius = readNumber(layout, "fieldRadius", 20);
    const iconSize = readNumber(layout, "iconSize", 24);
    const fontSize = readNumber(layout, "fontSize", 17);
    const lineHeight = readNumber(layout, "lineHeight", fontSize * 1.25);
    const cursorWidth = readNumber(
      config,
      "cursorWidth",
      readNumber(cursorTokens, "width", readNumber(layout, "cursorWidth", 2)),
    );
    const blinkFrames = Math.max(
      1,
      readNumber(
        config,
        "cursorBlinkFrames",
        readNumber(cursorTokens, "blinkFrames", 15),
      ),
    );
    const cursorOpacity = cursorBlinkOpacity(input.frame, blinkFrames);
    const foreground = readString(colors, "textPrimary", "#000000");
    const mutedForeground = readString(colors, "textSecondary", "#6B7280");
    const idleTextColor = readString(config, "idleTextColor", mutedForeground);
    const fieldBackground = readString(
      keyboardTokens,
      "keyBackground",
      readString(colors, "surface", "#FFFFFF"),
    );
    const fieldBorderColor = readString(
      keyboardTokens,
      "specialKeyBackground",
      "rgba(0,0,0,0.12)",
    );
    const barBackground = readString(
      keyboardTokens,
      "background",
      readString(colors, "background", "transparent"),
    );
    const text = readString(config, "text", "");
    const placeholder = readString(config, "placeholder", "");
    const cursorVisible = config.cursorVisible !== false;
    const leftItems = items(config.leftItems);
    const rightItems = items(config.rightItems);
    return {
      id: "text-input-bar",
      type: "text_input_bar",
      role: "composer",
      frame: input.frame,
      box: {
        x: input.viewport.x,
        y: input.viewport.y + input.viewport.height - height,
        width: input.viewport.width,
        height,
      },
      style: {
        background: barBackground,
        color: foreground,
        fontFamily: readString(fonts, "family", "system-ui"),
        fontSize,
        lineHeight: fontSize * 1.25,
        paddingX,
        paddingY,
        gap,
        fieldHeight,
      },
      children: [
        {
          id: "text-input-bar:left",
          type: "text_input_bar_icon_zone",
          role: "left",
          frame: input.frame,
          style: {
            gap,
          },
          children: leftItems.map((item) =>
            itemNode({
              item,
              frame: input.frame,
              color: mutedForeground,
              iconSize,
            }),
          ),
        },
        {
          id: "text-input-bar:field",
          type: "text_input_bar_field",
          role: "text_field",
          frame: input.frame,
          text: text || placeholder,
          style: {
            background: fieldBackground,
            color: text ? foreground : idleTextColor,
            borderRadius: fieldRadius,
            borderColor: fieldBorderColor,
            borderWidth: Math.max(1, cursorWidth * 0.5),
            paddingX: fieldPaddingX,
            paddingY: fieldPaddingY,
            height: fieldHeight,
            fontSize,
            lineHeight,
            whiteSpace: "pre-wrap",
            cursorColor: readString(
              config,
              "cursorColor",
              readString(cursorTokens, "color", foreground),
            ),
            cursorWidth,
            shadow:
              config.fieldShadowEnabled === false
                ? {}
                : {
                    color: "rgba(0,0,0,0.16)",
                    offsetX: 0,
                    offsetY: Math.max(1, cursorWidth * 0.5),
                    blur: Math.max(2, cursorWidth * 2),
                  },
          },
          metadata: {
            isPlaceholder: !text,
            cursorVisible,
          },
          children:
            cursorVisible && text
              ? [
                  {
                    id: "text-input-bar:field:cursor",
                    type: "text_input_bar_cursor",
                    role: "cursor",
                    frame: input.frame,
                    style: {
                      background: readString(
                        config,
                        "cursorColor",
                        readString(cursorTokens, "color", foreground),
                      ),
                      width: cursorWidth,
                      opacity: cursorOpacity,
                    },
                  },
                ]
              : undefined,
        },
        {
          id: "text-input-bar:right",
          type: "text_input_bar_icon_zone",
          role: "right",
          frame: input.frame,
          style: {
            gap,
          },
          children: rightItems.map((item) =>
            itemNode({
              item,
              frame: input.frame,
              color: mutedForeground,
              iconSize,
            }),
          ),
        },
      ],
      metadata: {
        state: readString(config, "state", text ? "typing" : "idle"),
        layout: "text_input_bar_v1",
      },
    };
  },
};
