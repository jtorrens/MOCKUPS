import type { RenderableNode } from "../visual/renderable/types.js";
import {
  embeddedComponentConfig,
} from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import {
  previewPayloadInBox,
  previewScreenBox,
  selectedColor,
} from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ChatListComponentSlot } from "./chatListModuleContract.js";
import { resolveChatListModule } from "./chatListModuleResolver.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";

const noMotion = {
  transition: "none",
  direction: "bottom",
  bounds: "parent",
  fade: false,
  translate: false,
  scale: false,
};

export function chatListModuleToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  const contract = resolveChatListModule(payload);
  const screen = previewScreenBox(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const status = componentClassToRenderable(componentSlotPayload(
    payload,
    componentBaseConfigs,
    "status_bar",
    contract.statusBarSlot,
  ));
  const navigation = componentClassToRenderable(componentSlotPayload(
    payload,
    componentBaseConfigs,
    "navigation_bar",
    contract.navigationBarSlot,
  ));
  const contentTop = screen.y + (status.box?.height ?? 0);
  const contentBottom = navigation.box?.y ?? screen.y + screen.height;
  const contentBox = {
    x: screen.x,
    y: contentTop,
    width: screen.width,
    height: Math.max(0, contentBottom - contentTop),
  };
  const stackPayload = previewPayloadInBox(
    {
      ...componentSlotPayload(
        payload,
        componentBaseConfigs,
        "componentStack",
        contract.stackSlot,
      ),
      designPreviewJson: JSON.stringify({
        sizingMode: "fill",
        startGapToken: "theme.spacing.none",
        endGapToken: "theme.spacing.none",
        items: [
          stackSlot(
            "top",
            "content",
            contract.topIconBarSlot,
            contract.topIconBarInputs,
          ),
          stackSlot(
            "list",
            "fill",
            contract.listSlot,
            contract.listInputs,
          ),
          stackSlot(
            "bottom",
            "content",
            contract.bottomIconBarSlot,
            contract.bottomIconBarInputs,
          ),
        ],
      }),
    },
    contentBox,
  );
  const children: RenderableNode[] = [
    contract.wallpaperEnabled
      ? wallpaperRenderable(payload, screen) ?? background(payload)
      : background(payload),
    componentClassToRenderable(stackPayload),
    status,
    navigation,
  ];

  return {
    id: contract.id,
    type: "group",
    frame: 0,
    box: screen,
    style: { overflow: "hidden" },
    children,
  };
}

function componentSlotPayload(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: string,
  slot: ChatListComponentSlot,
): DesignPreviewPayload {
  return {
    ...payload,
    componentType,
    configJson: JSON.stringify(embeddedComponentConfig(
      componentBaseConfigs,
      slot,
      componentType,
      `module.core.chatList.${componentType}`,
    )),
  };
}

function stackSlot(
  id: string,
  sizeMode: "content" | "fill",
  slot: ChatListComponentSlot,
  inputs: Record<string, unknown>,
) {
  return {
    id,
    sizeMode,
    gapBeforeMode: "fixed",
    gapBeforeToken: "theme.spacing.none",
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

function background(payload: DesignPreviewPayload): RenderableNode {
  return {
    id: "module.core.chatList.background",
    type: "surface",
    frame: 0,
    box: previewScreenBox(payload),
    style: {
      background: selectedColor(payload, "theme.colors.background"),
    },
  };
}
