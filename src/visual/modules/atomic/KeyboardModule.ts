import type { ResolvedChatScreenProps } from "../../../domain/schemas/index.js";
import {
  readNumber,
  readObject,
  readString,
} from "../../renderable/helpers.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";

export interface KeyboardModuleInput {
  frame: number;
  keyboard: ResolvedChatScreenProps["keyboard"];
  tokens: ResolvedChatScreenProps["theme"];
  viewport: ResolvedChatScreenProps["viewport"];
}

function rows(input: KeyboardModuleInput): unknown[][] {
  return Array.isArray(input.keyboard?.rows)
    ? input.keyboard.rows.filter(Array.isArray)
    : [];
}

function bottomItems(input: KeyboardModuleInput) {
  return Array.isArray(input.keyboard?.bottomItems)
    ? input.keyboard.bottomItems.map(asRecord).sort((left, right) => {
        return readNumber(left, "order", 0) - readNumber(right, "order", 0);
      })
    : [];
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

export const KeyboardModule: VisualModule<KeyboardModuleInput> = {
  type: "keyboard",
  version: 1,
  render(input) {
    const layout = asRecord(input.keyboard?.layout);
    const keyboardTokens = readObject(input.tokens, "keyboard");
    const colors = readObject(input.tokens, "colors");
    const fonts = readObject(input.tokens, "fonts");
    const height = readNumber(layout, "height", 0);
    const topPadding = readNumber(layout, "topPadding", 8);
    const sidePadding = readNumber(layout, "sidePadding", 6);
    const bottomPadding = readNumber(layout, "bottomPadding", 8);
    const bottomUtilityHeight = readNumber(layout, "bottomUtilityHeight", 0);
    const bottomUtilitySidePadding = readNumber(
      layout,
      "bottomUtilitySidePadding",
      sidePadding,
    );
    const rowGap = readNumber(layout, "rowGap", 8);
    const keyGap = readNumber(layout, "keyGap", 6);
    const keyHeight = readNumber(layout, "keyHeight", 42);
    const keyRadius = readNumber(layout, "keyRadius", 7);
    const fontSize = readNumber(layout, "fontSize", 18);
    const bottomIconSize = readNumber(layout, "bottomIconSize", fontSize);
    const background = readString(keyboardTokens, "background", "#D1D5DB");
    const keyBackground = readString(keyboardTokens, "keyBackground", "#FFFFFF");
    const specialKeyBackground = readString(
      keyboardTokens,
      "specialKeyBackground",
      "#AEB4BE",
    );
    const textColor = readString(
      keyboardTokens,
      "text",
      readString(colors, "textPrimary", "#000000"),
    );
    const keyboardRows = rows(input);
    const keyboardBottomItems = bottomItems(input);
    const pressedKey = readString(asRecord(input.keyboard), "pressedKey", "");
    return {
      id: "keyboard",
      type: "keyboard",
      role: "device_keyboard",
      frame: input.frame,
      box: {
        x: input.viewport.x,
        y: input.viewport.y + input.viewport.height - height,
        width: input.viewport.width,
        height,
      },
      style: {
        background,
        color: textColor,
        fontFamily: readString(fonts, "family", "system-ui"),
        fontSize,
        lineHeight: fontSize,
        paddingTop: topPadding,
        paddingX: sidePadding,
        paddingBottom: bottomPadding,
        bottomUtilityHeight,
        rowGap,
      },
      children: [
        ...keyboardRows.map((row, rowIndex) => ({
          id: `keyboard:row:${rowIndex}`,
          type: "keyboard_row",
          role: "row",
          frame: input.frame,
          style: {
            gap: keyGap,
            keyHeight,
          },
          children: row.map((rawKey, keyIndex) => {
            const key = asRecord(rawKey);
            const id = readString(key, "id", String(keyIndex));
            const kind = readString(key, "kind", "character");
            const label = readString(key, "label", "");
            const pressed = pressedKey !== "" && (pressedKey === id || pressedKey === label);
            return {
              id: `keyboard:row:${rowIndex}:key:${id}`,
              type: "keyboard_key",
              role: kind,
              frame: input.frame,
              text: label,
              style: {
                background:
                  kind === "character" || kind === "space"
                    ? keyBackground
                    : specialKeyBackground,
                color: textColor,
                borderRadius: keyRadius,
                fontSize,
                lineHeight: fontSize,
                weight: readNumber(key, "weight", 1),
              },
              metadata: {
                id,
                kind,
                pressed,
              },
              children:
                pressed && label
                  ? [
                      {
                        id: `keyboard:row:${rowIndex}:key:${id}:popover`,
                        type: "keyboard_key_popover",
                        role: kind,
                        frame: input.frame,
                        text: label,
                        style: {
                          background: readString(
                            keyboardTokens,
                            "popoverBackground",
                            readString(
                              keyboardTokens,
                              "keyBackground",
                              "#FFFFFF",
                            ),
                          ),
                          color: textColor,
                          borderRadius: keyRadius * 1.15,
                          fontSize: fontSize * 1.28,
                          lineHeight: fontSize * 1.28,
                          widthRatio: 0.86,
                        },
                      },
                    ]
                  : undefined,
            };
          }),
        })),
        {
          id: "keyboard:bottom-utility",
          type: "keyboard_bottom_utility",
          role: "reserved_icons_area",
          frame: input.frame,
          style: {
            height: bottomUtilityHeight,
            color: textColor,
            fontSize: bottomIconSize,
            lineHeight: bottomIconSize,
            paddingX: bottomUtilitySidePadding,
          },
          children: (["left", "right"] as const).map((zone) => ({
            id: `keyboard:bottom-utility:${zone}`,
            type: "keyboard_bottom_zone",
            role: zone,
            frame: input.frame,
            style: {
              justifyContent: zone === "left" ? "flex-start" : "flex-end",
              gap: bottomIconSize * 0.8,
            },
            children: keyboardBottomItems
              .filter((item) => readString(item, "zone", "off") === zone)
              .map((item) => {
                const id = readString(item, "id", readString(item, "token", "item"));
                const iconUri = readString(item, "iconUri", "");
                return {
                  id: `keyboard:bottom-utility:${zone}:${id}`,
                  type: "keyboard_bottom_item",
                  role: readString(item, "kind", "iconToken"),
                  frame: input.frame,
                  text: readString(item, "token", readString(item, "label", "")),
                  style: {
                    color: textColor,
                    fontSize: bottomIconSize,
                    lineHeight: bottomIconSize,
                    ...(iconUri
                      ? {
                          maskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
                          WebkitMaskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
                        }
                      : {}),
                  },
                  metadata: {
                    ...item,
                  },
                };
              }),
          })),
          metadata: {
            reservedFor: ["globe", "dictation", "homeIndicator"],
          },
        },
      ],
      metadata: {
        layout: "standard_multiline_keyboard_v1",
        mode: readString(asRecord(input.keyboard), "mode", "lowercase"),
      },
    };
  },
};
