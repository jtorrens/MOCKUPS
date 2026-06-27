import type { ResolvedChatScreenProps } from "../../../domain/schemas/index.js";
import {
  readFontWeight,
  readNumber,
  readObject,
  readString,
} from "../../renderable/helpers.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";
import { AvatarModule } from "./AvatarModule.js";

export interface ChatHeaderModuleInput {
  frame: number;
  viewport: ResolvedChatScreenProps["viewport"];
  statusBarHeight: number;
  header: ResolvedChatScreenProps["header"];
  colors: ResolvedChatScreenProps["theme"]["colors"];
  fonts: ResolvedChatScreenProps["theme"]["fonts"];
  shadows?: ResolvedChatScreenProps["theme"]["shadows"];
  avatarComponent?: Record<string, unknown>;
  typography?: ResolvedChatScreenProps["theme"]["typography"];
  headerTokens: ResolvedChatScreenProps["theme"]["header"];
  screenGutter: number;
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function sortedItems(value: unknown) {
  return Array.isArray(value)
    ? value.map(asRecord).sort((left, right) => {
        return readNumber(left, "order", 0) - readNumber(right, "order", 0);
      })
    : [];
}

function iconNode({
  item,
  idPrefix,
  frame,
  x,
  y,
  size,
  color,
}: {
  item: Record<string, unknown>;
  idPrefix: string;
  frame: number;
  x: number;
  y: number;
  size: number;
  color: string;
}): RenderableNode {
  const token = readString(item, "token", readString(item, "label", ""));
  const iconUri = readString(item, "iconUri", "");
  return {
    id: `${idPrefix}:${readString(item, "id", token)}`,
    type: "chat_header_icon",
    role: "icon",
    frame,
    text: token,
    box: { x, y, width: size, height: size },
    style: {
      color: readString(item, "color", color),
      fontSize: size,
      lineHeight: size,
      ...(iconUri
        ? {
            maskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
            WebkitMaskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
          }
        : {}),
    },
    metadata: { ...item },
  };
}

export const ChatHeaderModule: VisualModule<ChatHeaderModuleInput> = {
  type: "chat_header",
  version: 1,
  render(input) {
    const textColor = readString(input.colors, "textPrimary", "#000000");
    const resolvedHeaderBackground =
      typeof input.header.backgroundColor === "string" &&
      input.header.backgroundColor.trim()
        ? input.header.backgroundColor
        : undefined;
    const backgroundColor =
      resolvedHeaderBackground ??
      readString(
        input.headerTokens,
        "background",
        readString(input.colors, "background", "#FFFFFF"),
      );
    const headerHeight = readNumber(input.headerTokens, "height", 96);
    const elementGap = readNumber(
      input.headerTokens,
      "elementGap",
      8,
    );
    const sidePadding = readNumber(
      input.headerTokens,
      "sidePadding",
      elementGap,
    );
    const iconSize = readNumber(input.headerTokens, "iconSize", 24);
    const avatarSize = readNumber(
      input.headerTokens,
      "avatarSize",
      56,
    );
    const avatarComponent = input.avatarComponent ?? {};
    const avatarCornerRadius = readNumber(
      avatarComponent,
      "cornerRadius",
      Math.round(avatarSize * 0.22),
    );
    const avatarBorderWidth = readNumber(avatarComponent, "borderWidth", 0);
    const avatarBorderColor = readString(
      avatarComponent,
      "borderColor",
      textColor,
    );
    const avatarShadow =
      avatarComponent.shadowEnabled === true
        ? readObject(avatarComponent, "shadow")
        : {};
    const leftItems = sortedItems(input.headerTokens.leftItems);
    const rightItems = sortedItems(input.headerTokens.rightItems);
    const headerTitleTypography = readObject(
      input.typography ?? {},
      "headerTitle",
    );
    const headerSubtitleTypography = readObject(
      input.typography ?? {},
      "headerSubtitle",
    );
    const subtitleBottomPadding = readNumber(
      input.headerTokens,
      "subtitleBottomPadding",
      10,
    );
    const headerY = input.viewport.y + input.statusBarHeight;
    const iconY = Math.round(headerY + (headerHeight - iconSize) / 2);
    const avatarY = Math.round(headerY + (headerHeight - avatarSize) / 2);
    let cursorX = input.viewport.x + sidePadding;
    const children: RenderableNode[] = [];
    leftItems.forEach((item) => {
      children.push(
        iconNode({
          item,
          idPrefix: "chat_header:left",
          frame: input.frame,
          x: cursorX,
          y: iconY,
          size: iconSize,
          color: textColor,
        }),
      );
      cursorX += iconSize + elementGap;
    });
    const avatarBox = {
      x: cursorX,
      y: avatarY,
      width: avatarSize,
      height: avatarSize,
    };
    if (input.header.avatar) {
      children.push({
        ...AvatarModule.render({
          id: "chat_header:avatar",
          uri: input.header.avatar.uri,
          size: avatarSize,
          label: input.header.title,
          frame: input.frame,
          cornerRadius: avatarCornerRadius,
          borderWidth: avatarBorderWidth,
          borderColor: avatarBorderColor,
          shadow: avatarShadow,
          ...(typeof input.header.avatar.scale === "number"
            ? { imageScale: input.header.avatar.scale }
            : {}),
          ...(typeof input.header.avatar.offsetX === "number"
            ? { imageOffsetX: input.header.avatar.offsetX }
            : {}),
          ...(typeof input.header.avatar.offsetY === "number"
            ? { imageOffsetY: input.header.avatar.offsetY }
            : {}),
          ...(typeof input.header.avatar.baseSize === "number"
            ? { imageBaseSize: input.header.avatar.baseSize }
            : {}),
        }),
        box: avatarBox,
      });
      cursorX += avatarSize + elementGap;
    }
    const rightItemsWidth =
      rightItems.length > 0
        ? rightItems.length * iconSize + (rightItems.length - 1) * elementGap
        : 0;
    let rightX =
      input.viewport.x +
      input.viewport.width -
      sidePadding -
      rightItemsWidth;
    rightItems.forEach((item) => {
      children.push(
        iconNode({
          item,
          idPrefix: "chat_header:right",
          frame: input.frame,
          x: rightX,
          y: iconY,
          size: iconSize,
          color: textColor,
        }),
      );
      rightX += iconSize + elementGap;
    });
    const titleX = cursorX;
    const titleRight =
      rightItemsWidth > 0
        ? input.viewport.x + input.viewport.width - sidePadding - rightItemsWidth - elementGap
        : input.viewport.x + input.viewport.width - sidePadding;
    const titleWidth = Math.max(0, titleRight - titleX);
    const titleLineHeight = readNumber(headerTitleTypography, "lineHeight", 22);
    const subtitleLineHeight = readNumber(
      headerSubtitleTypography,
      "lineHeight",
      16,
    );
    const titleY = Math.round(headerY + (headerHeight - titleLineHeight) / 2);
    const subtitleY = Math.round(
      headerY + headerHeight - subtitleBottomPadding - subtitleLineHeight,
    );
    children.push({
      id: "chat_header:title",
      type: "text",
      role: "contact_name",
      frame: input.frame,
      box: {
        x: titleX,
        y: titleY,
        width: titleWidth,
        height: titleLineHeight,
      },
      text: input.header.title,
      style: {
        color: textColor,
        fontFamily: readString(
          headerTitleTypography,
          "fontFamily",
          readString(input.fonts, "family", "system-ui"),
        ),
        fontSize: readNumber(headerTitleTypography, "fontSize", 17),
        lineHeight: titleLineHeight,
        fontWeight: readFontWeight(
          headerTitleTypography,
          "fontWeight",
          readFontWeight(input.fonts, "weight", "Regular"),
        ),
      },
      metadata: {
        subtitle: input.header.subtitle ?? null,
      },
    });
    if (input.header.subtitle) {
      children.push({
        id: "chat_header:subtitle",
        type: "text",
        role: "contact_subtitle",
        frame: input.frame,
        box: {
          x: titleX,
          y: subtitleY,
          width: titleWidth,
          height: subtitleLineHeight,
        },
        text: input.header.subtitle,
        style: {
          color: textColor,
          fontFamily: readString(
            headerSubtitleTypography,
            "fontFamily",
            readString(input.fonts, "family", "system-ui"),
          ),
          fontSize: readNumber(headerSubtitleTypography, "fontSize", 13),
          lineHeight: subtitleLineHeight,
          fontWeight: readFontWeight(
            headerSubtitleTypography,
            "fontWeight",
            readFontWeight(input.fonts, "weight", "Regular"),
          ),
        },
      });
    }

    return {
      id: "chat_header",
      type: "chat_header",
      role: "conversation_header",
      frame: input.frame,
      box: {
        x: input.viewport.x,
        y: headerY,
        width: input.viewport.width,
        height: headerHeight,
      },
      style: {
        backgroundColor,
        textColor,
        separatorColor: readString(
          input.headerTokens,
          "separatorColor",
          "transparent",
        ),
        separatorWidth: readNumber(input.headerTokens, "separatorWidth", 0),
        elementGap,
        iconSize,
      },
      children,
      metadata: {
        layout: "token_driven_header",
        tokenSource: "theme.tokens_json.header/typography",
      },
    };
  },
};
