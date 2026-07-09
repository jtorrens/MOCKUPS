import type { RenderableNode } from "../visual/renderable/types.js";
import { bubbleComponentToRenderable } from "./bubbleComponentRenderable.js";
import { resolveBubbleComponent } from "./bubbleComponentResolver.js";
import { componentPresetConfig } from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalBoolean,
  optionalNumber,
  optionalString,
  parseObject,
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
  translateRenderableNode,
} from "./componentRenderableCommon.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { textInputBarComponentToRenderable } from "./textInputBarComponentRenderable.js";
import { resolveTextInputBarComponent } from "./textInputBarComponentResolver.js";

type JsonRecord = Record<string, unknown>;

export function conversationModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const conversation = asRecord(config.conversation);
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const children: RenderableNode[] = [];

  const status = optionalBooleanDefault(conversation, "showStatusBar", true)
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
  const navigation = optionalBooleanDefault(conversation, "showNavigationBar", true)
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
  const keyboard = optionalBooleanDefault(conversation, "showKeyboard", true)
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
  const textInput = optionalBooleanDefault(conversation, "showTextInputBar", true)
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

  if (status) children.push(status);
  const header = optionalBooleanDefault(conversation, "showHeader", true)
    ? headerNode(payload, preview, (status?.box?.height ?? 0), optionalNumber(conversation, "headerHeight", 64) * scale)
    : undefined;
  if (header) children.push(header);

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
  const messages = [
    { state: "incoming", text: optionalString(preview, "message1Text") },
    { state: "outgoing", text: optionalString(preview, "message2Text") },
    { state: "system", text: optionalString(preview, "message3Text") },
  ] as const;
  const nodes = messages.map((message, index) => childRenderable(
    payload,
    componentBaseConfigs,
    "bubble",
    bubbleVariant,
    {
      state: message.state,
      sampleText: message.text,
      mediaType: "none",
      writeOnTrigger: false,
      writeOnFrame: 0,
      statusState: message.state === "outgoing" ? "read" : "none",
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
  preview: JsonRecord,
  offsetY: number,
  height: number,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const title = optionalString(preview, "headerTitle") || "Conversation";
  const subtitle = optionalString(preview, "headerSubtitle") || "";
  const titleHeight = subtitle ? height * 0.46 : height;
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
      background: selectedColor(payload, "theme.colors.surface"),
    },
    children: [
      textNode(payload, `${title}`, screen.x + 72 * scale, screen.y + offsetY + 10 * scale, screen.width - 144 * scale, titleHeight, 19 * scale, 700),
      ...(subtitle
        ? [textNode(payload, subtitle, screen.x + 72 * scale, screen.y + offsetY + 36 * scale, screen.width - 144 * scale, height * 0.34, 12 * scale, 500, "theme.colors.textSecondary")]
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

function optionalBooleanDefault(value: Record<string, unknown>, key: string, fallback: boolean) {
  return Object.hasOwn(value, key) ? optionalBoolean(value, key) : fallback;
}
