import type { ResolvedChatScreenProps } from "../../../domain/schemas/index.js";
import {
  readNumber,
  readObject,
  readString,
} from "../../renderable/helpers.js";
import type { VisualModule } from "../types.js";
import { AvatarModule } from "./AvatarModule.js";

export interface ChatHeaderModuleInput {
  frame: number;
  viewport: ResolvedChatScreenProps["viewport"];
  statusBarHeight: number;
  header: ResolvedChatScreenProps["header"];
  colors: ResolvedChatScreenProps["theme"]["colors"];
  fonts: ResolvedChatScreenProps["theme"]["fonts"];
  typography?: ResolvedChatScreenProps["theme"]["typography"];
  headerTokens: ResolvedChatScreenProps["theme"]["header"];
  avatarTokens: ResolvedChatScreenProps["theme"]["avatars"];
  screenGutter: number;
}

export const ChatHeaderModule: VisualModule<ChatHeaderModuleInput> = {
  type: "chat_header",
  version: 1,
  render(input) {
    const textColor = readString(input.colors, "textPrimary", "#000000");
    const backgroundColor = readString(
      input.headerTokens,
      "background",
      readString(input.colors, "background", "#FFFFFF"),
    );
    const headerHeight = readNumber(input.headerTokens, "height", 96);
    const avatarSize = readNumber(input.avatarTokens, "headerSize", 56);
    const avatarGap = readNumber(input.avatarTokens, "gap", 8);
    const headerTitleTypography = readObject(
      input.typography ?? {},
      "headerTitle",
    );
    const headerSubtitleTypography = readObject(
      input.typography ?? {},
      "headerSubtitle",
    );
    const headerY = input.viewport.y + input.statusBarHeight;
    const avatarBox = {
      x: input.viewport.x + input.screenGutter,
      y: Math.round(headerY + (headerHeight - avatarSize) / 2),
      width: avatarSize,
      height: avatarSize,
    };
    const children = input.header.avatar
      ? [
          {
            ...AvatarModule.render({
              id: "chat_header:avatar",
              uri: input.header.avatar.uri,
              size: avatarSize,
              label: input.header.title,
              frame: input.frame,
            }),
            box: avatarBox,
          },
        ]
      : [];
    const titleX =
      input.viewport.x +
      input.screenGutter +
      (input.header.avatar ? avatarSize + avatarGap : 0);
    children.push({
      id: "chat_header:title",
      type: "text",
      role: "contact_name",
      frame: input.frame,
      box: {
        x: titleX,
        y: headerY,
        width: Math.max(
          0,
          input.viewport.width -
            (titleX - input.viewport.x) -
            input.screenGutter,
        ),
        height: headerHeight,
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
        lineHeight: readNumber(headerTitleTypography, "lineHeight", 22),
        fontWeight: readNumber(
          headerTitleTypography,
          "fontWeight",
          readNumber(input.fonts, "weightSemibold", 600),
        ),
      },
      metadata: {
        subtitle: input.header.subtitle ?? null,
        subtitleTypography: {
          fontFamily: readString(
            headerSubtitleTypography,
            "fontFamily",
            readString(input.fonts, "family", "system-ui"),
          ),
          fontSize: readNumber(headerSubtitleTypography, "fontSize", 13),
          lineHeight: readNumber(headerSubtitleTypography, "lineHeight", 16),
          fontWeight: readNumber(
            headerSubtitleTypography,
            "fontWeight",
            readNumber(input.fonts, "weightRegular", 400),
          ),
        },
      },
    });

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
        avatarGap,
      },
      children,
      metadata: {
        layout: "token_driven_header",
        tokenSource: "theme.tokens_json.header/avatars/typography",
      },
    };
  },
};
