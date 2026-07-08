import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  numberToken,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type {
  IconBarDesignContract,
  IconBarZone,
} from "./iconBarComponentContract.js";
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";

export function iconBarComponentToRenderable(
  payload: DesignPreviewPayload,
  iconBar: IconBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  return iconBarComponentToRenderableAt(
    payload,
    iconBar,
    boundedCenterBox(
      payload,
      iconBar.size.width * scale,
      iconBar.size.height * scale,
    ),
  );
}

export function iconBarComponentToRenderableAt(
  payload: DesignPreviewPayload,
  iconBar: IconBarDesignContract,
  box: RenderableBox,
): RenderableNode {
  const edgePadding = Math.max(0, numberToken(payload, iconBar.edgePaddingToken) * renderScale(payload));
  const zones: IconBarZone[] = ["left", "center", "right"];
  const children = zones.flatMap((zone) => {
    const row = iconBar.rows[zone];
    if (row.buttons.length === 0) return [];

    const size = measureIconRowComponent(payload, row);
    const rowBox = {
      x: zoneX(zone, box, size.width, edgePadding),
      y: box.y + Math.max(0, (box.height - size.height) * 0.5),
      width: size.width,
      height: size.height,
    };
    return [iconRowComponentToRenderableAt(payload, row, rowBox)];
  });

  return {
    id: iconBar.id,
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "visible",
    },
    children,
  };
}

function zoneX(
  zone: IconBarZone,
  box: RenderableBox,
  rowWidth: number,
  edgePadding: number,
) {
  if (zone === "left") return box.x + edgePadding;
  if (zone === "right") return box.x + box.width - edgePadding - rowWidth;
  return box.x + (box.width - rowWidth) * 0.5;
}
