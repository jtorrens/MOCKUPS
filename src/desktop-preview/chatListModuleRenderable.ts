import type { RenderableNode } from "../visual/renderable/types.js";
import {
  embeddedComponentConfig,
} from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import {
  numberToken,
  previewScreenBox,
  renderScale,
  translateRenderableNode,
} from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveChatListModule } from "./chatListModuleResolver.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";

export function chatListModuleToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  const contract = resolveChatListModule(payload);
  const screen = previewScreenBox(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const list = componentClassToRenderable({
    ...payload,
    componentType: "list",
    configJson: JSON.stringify(embeddedComponentConfig(
      componentBaseConfigs,
      contract.listSlot,
      "list",
      "module.core.chatList.listSlot",
    )),
    designPreviewJson: JSON.stringify(contract.listInputs),
  });
  if (!list.box) {
    throw new Error("module.core.chatList List boundary must resolve a box");
  }

  const topInset = Math.max(
    0,
    numberToken(payload, contract.topInsetToken) * renderScale(payload),
  );
  const targetX = contract.horizontalAlignment === "left"
    ? screen.x
    : contract.horizontalAlignment === "right"
      ? screen.x + screen.width - list.box.width
      : screen.x + (screen.width - list.box.width) / 2;
  const translatedList = translateRenderableNode(list, {
    x: targetX - list.box.x,
    y: screen.y + topInset - list.box.y,
  });
  const children: RenderableNode[] = [];
  const wallpaper = wallpaperRenderable(payload, screen);
  if (wallpaper) children.push(wallpaper);
  children.push(translatedList);

  return {
    id: contract.id,
    type: "group",
    frame: 0,
    box: screen,
    style: { overflow: "hidden" },
    children,
  };
}
