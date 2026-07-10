import type { RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { bubbleComponentToRenderable } from "./bubbleComponentRenderable.js";
import { resolveBubbleComponent } from "./bubbleComponentResolver.js";
import { componentPresetConfig } from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { keyboardComponentToRenderable } from "./keyboardComponentRenderable.js";
import { resolveKeyboardComponent } from "./keyboardComponentResolver.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import {
  numberToken,
  previewScreenBox,
  renderableVisualBounds,
  renderScale,
  selectedColor,
  selectedPaletteColor,
  translateRenderableNode,
} from "./componentRenderableCommon.js";
import { mediaFrameUriForPath } from "./previewAssetResolver.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { textInputBarComponentToRenderable } from "./textInputBarComponentRenderable.js";
import { resolveTextInputBarComponent } from "./textInputBarComponentResolver.js";

type JsonRecord = Record<string, unknown>;

export function conversationModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const config = parseObject(payload.configJson);
  const preview = runtimePreview(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const conversation = {
    ...asRecord(config.conversation),
    ...asRecord(parseObject(payload.instanceJson).behavior),
  };
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const children: RenderableNode[] = [];
  const wallpaper = requiredBoolean(
    conversation,
    "useAppWallpaper",
    "module.conversation.useAppWallpaper",
  )
    ? appWallpaperNode(payload, screen)
    : undefined;
  if (wallpaper) children.push(wallpaper);

  const status = requiredBoolean(
    conversation,
    "showStatusBar",
    "module.conversation.showStatusBar",
  )
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "status_bar",
        requiredString(conversation, "statusBarVariant", "module.conversation.statusBarVariant"),
        {},
        (childPayload) =>
          statusBarComponentToRenderable(childPayload, resolveStatusBarComponent(childPayload)),
      )
    : undefined;
  const navigation = requiredBoolean(
    conversation,
    "showNavigationBar",
    "module.conversation.showNavigationBar",
  )
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "navigation_bar",
        requiredString(
          conversation,
          "navigationBarVariant",
          "module.conversation.navigationBarVariant",
        ),
        {},
        (childPayload) =>
          navigationBarComponentToRenderable(
            childPayload,
            resolveNavigationBarComponent(childPayload),
          ),
      )
    : undefined;
  const keyboard = requiredBoolean(
    conversation,
    "showKeyboard",
    "module.conversation.showKeyboard",
  )
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "keyboard",
        requiredString(conversation, "keyboardVariant", "module.conversation.keyboardVariant"),
        {
          text: optionalString(preview, "inputText"),
          currentCharacter: 1,
        },
        (childPayload) =>
          keyboardComponentToRenderable(childPayload, resolveKeyboardComponent(childPayload)),
      )
    : undefined;
  const textInput = requiredBoolean(
    conversation,
    "showTextInputBar",
    "module.conversation.showTextInputBar",
  )
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "textInputBar",
        requiredString(
          conversation,
          "textInputBarVariant",
          "module.conversation.textInputBarVariant",
        ),
        {
          sampleText: optionalString(preview, "inputText"),
        },
        (childPayload) =>
          textInputBarComponentToRenderable(
            childPayload,
            resolveTextInputBarComponent(childPayload),
          ),
      )
    : undefined;

  const navHeight = navigation?.box?.height ?? 0;
  const keyboardTargetY = screen.y + screen.height - navHeight - (keyboard?.box?.height ?? 0);
  const keyboardNode = keyboard?.box
    ? translateRenderableNode(keyboard, { x: 0, y: keyboardTargetY - keyboard.box.y })
    : keyboard;
  const keyboardHeight = keyboardNode?.box?.height ?? 0;
  const textInputTargetY = screen.y + screen.height - navHeight - keyboardHeight - (textInput?.box?.height ?? 0);
  const textInputNode = textInput?.box
    ? translateRenderableNode(textInput, { x: 0, y: textInputTargetY - textInput.box.y })
    : textInput;

  const header = requiredBoolean(
    conversation,
    "showHeader",
    "module.conversation.showHeader",
  )
    ? headerNode(
        payload,
        componentBaseConfigs,
        conversation,
        preview,
        (status?.box?.height ?? 0),
        requiredNumber(conversation, "headerHeight", "module.conversation.headerHeight") * scale,
      )
    : undefined;
  if (header) children.push(header);
  // The header surface bleeds behind Status Bar, but its layout box remains below it.
  if (status) children.push(status);

  const top = screen.y + (status?.box?.height ?? 0) + (header?.box?.height ?? 0);
  const bottom = textInputNode?.box?.y ?? keyboardNode?.box?.y ?? (screen.y + screen.height - navHeight);
  children.push(...messageNodes(
    payload,
    componentBaseConfigs,
    conversation,
    preview,
    top,
    bottom,
  ));

  if (textInputNode) children.push(textInputNode);
  if (keyboardNode) children.push(keyboardNode);
  if (navigation) children.push(navigation);

  return {
    id: "module.conversation",
    type: "group",
    frame: 0,
    box: screen,
    style: {
      overflow: "hidden",
    },
    children,
  };
}

function runtimePreview(payload: DesignPreviewPayload): JsonRecord {
  const preview = parseObject(payload.designPreviewJson);
  if (payload.kind !== "moduleInstance") return preview;

  const instance = parseObject(payload.instanceJson);
  const content = asRecord(instance.content);
  const header = asRecord(content.header);
  const context = asRecord(instance.context);
  return {
    ...preview,
    actor: context.ownerActor,
    headerTitle: optionalString(header, "title"),
    headerSubtitle: optionalString(header, "subtitle"),
    instanceMessages: content.messages,
  };
}

function appWallpaperNode(
  payload: DesignPreviewPayload,
  screen: NonNullable<RenderableNode["box"]>,
): RenderableNode | undefined {
  const appConfig = parseObject(payload.appConfigJson ?? "{}");
  const wallpaper = asRecord(appConfig.wallpaper);
  const opacity = Math.max(0, Math.min(1, optionalNumber(wallpaper, "opacity", 1)));
  if (opacity <= 0) return undefined;

  const image = asRecord(wallpaper.image);
  const filePath = optionalString(image, "filePath");
  const kind = optionalString(wallpaper, "kind") || (filePath ? "image" : "solid");
  if (kind === "image") {
    const frame = mediaFrameUriForPath(payload, filePath, 0);
    if (frame.uri) {
      return {
        id: "module.conversation.wallpaper.image",
        type: "image",
        frame: 0,
        box: screen,
        asset: {
          type: "image",
          uri: frame.uri,
        },
        style: {
          objectFit: "cover",
          opacity,
        },
      };
    }
  }

  const modes = asRecord(appConfig.modes);
  const mode = asRecord(modes[payload.themeMode || "light"]);
  const modeWallpaper = asRecord(mode.wallpaper);
  const colorToken = optionalString(modeWallpaper, "color");
  if (!colorToken) return undefined;
  return {
    id: "module.conversation.wallpaper.color",
    type: "surface",
    frame: 0,
    box: screen,
    style: {
      background: selectedPaletteColor(payload, colorToken, opacity),
    },
  };
}

function messageNodes(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  conversation: JsonRecord,
  preview: JsonRecord,
  top: number,
  bottom: number,
) {
  const gap = numberToken(payload, optionalString(conversation, "messageGap") || "theme.spacing.m")
    * renderScale(payload);
  const gutter = spacingPair(payload, optionalString(conversation, "screenGutter") || "theme.spacing.l|theme.spacing.l");
  const bubbleVariant = requiredString(
    conversation,
    "bubbleVariant",
    "module.conversation.bubbleVariant",
  );
  const messages = instanceMessages(preview);
  const nodes = messages.map((message, index) => childRenderable(
    payload,
    componentBaseConfigs,
    "bubble",
    bubbleVariant,
    {
      state: message.state,
      sampleText: message.text,
      actor: preview.actor,
      mediaType: "none",
      maxWidth: optionalNumber(conversation, "bubbleMaxWidth", 66),
      writeOnTrigger: false,
      writeOnFrame: 0,
      statusState: message.statusState,
      statusText: message.statusText,
    },
    (childPayload) => bubbleComponentToRenderable(childPayload, resolveBubbleComponent(childPayload)),
  ));
  const entries = nodes.map((node) => ({
    node,
    bounds: renderableVisualBounds(node),
  }));
  const totalHeight = entries.reduce((sum, entry) => sum + entry.bounds.height, 0)
    + Math.max(0, nodes.length - 1) * gap;
  let y = Math.max(top + gutter.y, bottom - gutter.y - totalHeight);
  return entries.map((entry, index) => {
    const { node, bounds } = entry;
    const message = messages[index]!;
    const offsetX = message.state === "outgoing"
      ? payload.previewFrame.screenX + payload.previewFrame.screenWidth - gutter.x - (bounds.x + bounds.width)
      : message.state === "system"
        ? payload.previewFrame.screenX + payload.previewFrame.screenWidth / 2 - (bounds.x + bounds.width / 2)
        : payload.previewFrame.screenX + gutter.x - bounds.x;
    const translated = translateRenderableNode(node, { x: offsetX, y: y - bounds.y });
    y += bounds.height + gap;
    return translated;
  });
}

type ConversationPreviewMessage = {
  state: string;
  text: string;
  statusState: string;
  statusText: string;
};

function instanceMessages(preview: JsonRecord): ConversationPreviewMessage[] {
  const messages = Array.isArray(preview.instanceMessages)
    ? preview.instanceMessages.map(asRecord)
    : [];
  if (messages.length > 0) {
    return messages.map((message) => {
      const status = asRecord(message.status);
      return {
        state: optionalString(message, "direction") || "incoming",
        text: optionalString(message, "text"),
        statusState: optionalString(status, "deliveryStatus") || "none",
        statusText: optionalString(status, "text"),
      };
    });
  }

  return [
    {
      state: "incoming",
      text: optionalString(preview, "message1Text"),
      statusState: "none",
      statusText: "",
    },
    {
      state: "outgoing",
      text: optionalString(preview, "message2Text"),
      statusState: optionalString(preview, "message2StatusState") || "read",
      statusText: optionalString(preview, "message2StatusText"),
    },
    {
      state: "system",
      text: optionalString(preview, "message3Text"),
      statusState: "none",
      statusText: "",
    },
  ];
}

function childRenderable(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  componentType: string,
  presetReference: string,
  designPreviewPatch: JsonRecord,
  render: (payload: DesignPreviewPayload) => RenderableNode,
) {
  const config = componentPresetConfig(componentBaseConfigs, componentType, presetReference);
  return render({
    ...payload,
    kind: "componentClass",
    componentType,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(designPreviewPatch),
  });
}

function headerNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  conversation: JsonRecord,
  preview: JsonRecord,
  offsetY: number,
  height: number,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const title = optionalString(preview, "headerTitle");
  const subtitle = optionalString(preview, "headerSubtitle");
  const titleHeight = subtitle ? height * 0.46 : height;
  const avatarSize = Math.max(0, height - 16 * scale);
  const avatar = avatarComponentToRenderableAt(
    payload,
    resolveAvatarComponentFromRecords(
      componentPresetConfig(
        componentBaseConfigs,
        "avatar",
        requiredString(
          conversation,
          "headerAvatarVariant",
          "module.conversation.headerAvatarVariant",
        ),
      ),
      {
        ...preview,
        sampleSubtext: "",
      },
      componentBaseConfigs,
      "module.conversation.header.avatar",
    ),
    {
      x: screen.x + 12 * scale,
      y: screen.y + offsetY + (height - avatarSize) / 2,
      width: avatarSize,
      height: avatarSize,
    },
  );
  const textLeft = screen.x + 24 * scale + avatarSize;
  const textWidth = Math.max(0, screen.width - (textLeft - screen.x) - 16 * scale);
  return {
    id: "module.conversation.header",
    type: "group",
    frame: 0,
    box: {
      x: screen.x,
      y: screen.y + offsetY,
      width: screen.width,
      height,
    },
    style: {
    },
    children: [
      {
        id: "module.conversation.header.bleed",
        type: "surface",
        frame: 0,
        box: {
          x: screen.x,
          y: screen.y,
          width: screen.width,
          height: offsetY + height,
        },
        style: {
          background: selectedColor(payload, "theme.colors.surface"),
        },
      },
      avatar,
      textNode(payload, `${title}`, textLeft, screen.y + offsetY + 10 * scale, textWidth, titleHeight, 19 * scale, 700),
      ...(subtitle
        ? [textNode(payload, subtitle, textLeft, screen.y + offsetY + 36 * scale, textWidth, height * 0.34, 12 * scale, 500, "theme.colors.textSecondary")]
        : []),
      {
        id: "module.conversation.header.separator",
        type: "surface",
        frame: 0,
        box: {
          x: screen.x,
          y: screen.y + offsetY + height - Math.max(1, scale),
          width: screen.width,
          height: Math.max(1, scale),
        },
        style: {
          background: selectedColor(payload, "theme.colors.divider"),
        },
      },
    ],
  };
}

function textNode(
  payload: DesignPreviewPayload,
  text: string,
  x: number,
  y: number,
  width: number,
  height: number,
  fontSize: number,
  fontWeight: number,
  colorToken = "theme.colors.textPrimary",
): RenderableNode {
  return {
    id: `module.conversation.header.text.${text}`,
    type: "text",
    frame: 0,
    text,
    box: { x, y, width, height },
    style: {
      alignItems: "center",
      color: selectedColor(payload, colorToken),
      display: "flex",
      fontSize,
      fontWeight,
      overflow: "hidden",
      whiteSpace: "nowrap",
    },
  };
}

function spacingPair(payload: DesignPreviewPayload, value: string) {
  const [xToken = "theme.spacing.l", yToken = xToken] = value.split("|");
  const scale = renderScale(payload);
  return {
    x: numberToken(payload, xToken) * scale,
    y: numberToken(payload, yToken) * scale,
  };
}
