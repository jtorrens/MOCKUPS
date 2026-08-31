import type { RenderableNode } from "../visual/renderable/types.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import {
  numberToken,
  previewPayloadInBox,
  previewScreenBox,
  selectedColor,
} from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { renderScale, translateRenderableNode } from "./previewGeometryHelpers.js";
import type { SocialPostComponentSlot } from "./socialPostModuleContract.js";
import { resolveSocialPostModule } from "./socialPostModuleResolver.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";

const noMotion = {
  transition: "none",
  direction: "bottom",
  bounds: "parent",
  fade: false,
  translate: false,
  scale: false,
};

export function socialPostModuleToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  const contract = resolveSocialPostModule(payload);
  const screen = previewScreenBox(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const themeStatusBarVariantReference = payload.themeStatusBarVariantReference?.trim() ?? "";
  const themeNavigationBarVariantReference = payload.themeNavigationBarVariantReference?.trim() ?? "";
  const status = contract.showStatusBar && themeStatusBarVariantReference
    ? componentNode(payload, componentBaseConfigs, "status_bar", {
        variantReference: themeStatusBarVariantReference,
        overrides: {},
      }, {})
    : undefined;
  const navigation = contract.showNavigationBar && themeNavigationBarVariantReference
    ? componentNode(payload, componentBaseConfigs, "navigation_bar", {
        variantReference: themeNavigationBarVariantReference,
        overrides: {},
      }, {})
    : undefined;
  const keyboard = contract.showKeyboard
    ? componentNode(
        payload,
        componentBaseConfigs,
        "keyboard",
        contract.keyboardSlot,
        contract.keyboardInputs,
      )
    : undefined;
  const navigationHeight = navigation?.box?.height ?? 0;
  const keyboardTargetY = screen.y + screen.height
    - navigationHeight
    - (keyboard?.box?.height ?? 0);
  const keyboardNode = keyboard?.box
    ? translateRenderableNode(keyboard, { x: 0, y: keyboardTargetY - keyboard.box.y })
    : keyboard;
  const textInput = contract.showTextInputBar
    ? componentNode(
        payload,
        componentBaseConfigs,
        "textInputBar",
        contract.textInputBarSlot,
        {
          ...contract.textInputBarInputs,
          availableWidth: screen.width / renderScale(payload),
        },
      )
    : undefined;
  const composerBaseY = keyboardNode?.box?.y
    ?? screen.y + screen.height - navigationHeight;
  const textInputTargetY = composerBaseY - (textInput?.box?.height ?? 0);
  const textInputNode = textInput?.box
    ? translateRenderableNode(textInput, { x: 0, y: textInputTargetY - textInput.box.y })
    : textInput;
  const gutter = spacingPair(payload, contract.screenGutter);
  const contentTop = screen.y + (status?.box?.height ?? 0) + gutter.y;
  const contentBottom = textInputNode?.box?.y
    ?? keyboardNode?.box?.y
    ?? screen.y + screen.height - navigationHeight - gutter.y;
  const contentBox = {
    x: screen.x + gutter.x,
    y: contentTop,
    width: Math.max(0, screen.width - gutter.x * 2),
    height: Math.max(0, contentBottom - contentTop),
  };
  const zones = [
    ...(contract.showHeader
      ? [stackSlot("header", "content", contract.headerStackSlot, contract.headerStackInputs, contract.zoneGap)]
      : []),
    stackSlot("media", "fill", contract.mediaSlot, contract.mediaInputs, contract.zoneGap),
    stackSlot("message", "content", contract.bubbleSlot, contract.bubbleInputs, contract.zoneGap),
    stackSlot("actions", "content", contract.footerIconBarSlot, contract.footerIconBarInputs, contract.zoneGap),
  ];
  const stackPayload = previewPayloadInBox(
    {
      ...componentPayload(
        payload,
        componentBaseConfigs,
        "componentStack",
        contract.stackSlot,
        {},
      ),
      designPreviewJson: JSON.stringify({
        sizingMode: "fill",
        startGapToken: "theme.spacing.none",
        endGapToken: "theme.spacing.none",
        items: zones,
      }),
    },
    contentBox,
  );
  const backgroundNode = contract.useAppWallpaper
    ? wallpaperRenderable(payload, screen) ?? background(payload)
    : background(payload);
  const children: RenderableNode[] = [
    backgroundNode,
    componentClassToRenderable(stackPayload),
  ];
  if (status) children.push(status);
  if (textInputNode) children.push(withZIndex(textInputNode, 10));
  if (keyboardNode) children.push(withZIndex(keyboardNode, 20));
  if (navigation) children.push(withZIndex(navigation, 30));
  return {
    id: contract.id,
    type: "group",
    frame: 0,
    box: screen,
    style: { overflow: "hidden" },
    children,
  };
}

function componentNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: string,
  slot: SocialPostComponentSlot,
  inputs: Record<string, unknown>,
) {
  return componentClassToRenderable(componentPayload(
    payload,
    componentBaseConfigs,
    componentType,
    slot,
    inputs,
  ));
}

function componentPayload(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: string,
  slot: SocialPostComponentSlot,
  inputs: Record<string, unknown>,
): DesignPreviewPayload {
  return {
    ...payload,
    componentType,
    configJson: JSON.stringify(embeddedComponentConfig(
      componentBaseConfigs,
      slot,
      componentType,
      `module.core.socialPost.${componentType}`,
    )),
    designPreviewJson: JSON.stringify(inputs),
  };
}

function stackSlot(
  id: string,
  sizeMode: "content" | "fill",
  slot: SocialPostComponentSlot,
  inputs: Record<string, unknown>,
  gapToken: string,
) {
  return {
    id,
    sizeMode,
    gapBeforeMode: "fixed",
    gapBeforeToken: id === "header" ? "theme.spacing.none" : gapToken,
    gapBeforeWeight: 1,
    alternatives: [{
      id: `${id}.default`,
      variantReference: slot.variantReference,
      overrides: slot.overrides,
      inputs,
      active: true,
      behavior: "replace",
      placement: {
        mode: "center",
        alignX: 0.5,
        alignY: 0.5,
        offsetX: 0,
        offsetY: 0,
      },
      enterMotion: noMotion,
      exitMotion: noMotion,
    }],
  };
}

function spacingPair(payload: DesignPreviewPayload, value: string) {
  const [xToken = "theme.spacing.none", yToken = xToken] = value.split("|");
  const scale = renderScale(payload);
  return {
    x: numberToken(payload, xToken) * scale,
    y: numberToken(payload, yToken) * scale,
  };
}

function background(payload: DesignPreviewPayload): RenderableNode {
  return {
    id: "module.core.socialPost.background",
    type: "surface",
    frame: 0,
    box: previewScreenBox(payload),
    style: { background: selectedColor(payload, "theme.colors.background") },
    metadata: { paintRole: "moduleBackground" },
  };
}

function withZIndex(node: RenderableNode, zIndex: number): RenderableNode {
  return {
    ...node,
    style: { ...node.style, zIndex },
  };
}
