import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { BadgeDesignContract } from "./badgeComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  boundedCenterBox,
  iconTokenStyle,
  numberToken,
  placeChild,
  renderScale,
  scalePlacement,
  selectedPaletteColor,
} from "./componentRenderableCommon.js";
import { resolveTypographyStyle } from "./previewTextHelpers.js";

export function badgeComponentToRenderable(payload: DesignPreviewPayload, badge: BadgeDesignContract): RenderableNode {
  const size = measureBadgeComponent(payload, badge);
  return badgeComponentToRenderableInBox(payload, badge, boundedCenterBox(payload, size, size));
}

export function badgeComponentToRenderableAt(
  payload: DesignPreviewPayload,
  badge: BadgeDesignContract,
  parentBox: RenderableBox,
): RenderableNode {
  const size = measureBadgeComponent(payload, badge);
  return badgeComponentToRenderableInBox(
    payload,
    badge,
    placeChild(parentBox, { width: size, height: size }, scalePlacement(badge.placement, renderScale(payload))),
  );
}

export function measureBadgeComponent(payload: DesignPreviewPayload, badge: BadgeDesignContract) {
  return Math.max(1, badge.size * renderScale(payload));
}

function badgeComponentToRenderableInBox(
  payload: DesignPreviewPayload,
  badge: BadgeDesignContract,
  box: RenderableBox,
): RenderableNode {
  const foreground = selectedPaletteColor(payload, badge.contentPaletteColor);
  const children: RenderableNode[] = [{
    id: `${badge.id}.surface`,
    type: "surface",
    frame: 0,
    box,
    style: {
      background: selectedPaletteColor(payload, badge.backgroundPaletteColor),
      borderRadius: box.width / 2,
      overflow: "hidden",
    },
  }];
  if (badge.contentMode === "icon") {
    const padding = numberToken(payload, badge.paddingToken) * renderScale(payload);
    const size = Math.max(1, box.width - padding * 2);
    children.push({
      id: `${badge.id}.icon`,
      type: "icon",
      frame: 0,
      box: { x: box.x + (box.width - size) / 2, y: box.y + (box.height - size) / 2, width: size, height: size },
      text: badge.iconToken,
      style: { ...iconTokenStyle(payload, badge.iconToken, foreground) },
    });
  } else {
    const typography = resolveTypographyStyle(payload, badge.textTypography, renderScale(payload));
    children.push({
      id: `${badge.id}.text`,
      type: "text",
      frame: 0,
      box,
      text: badge.text,
      style: {
        alignItems: "center",
        display: "flex",
        justifyContent: "center",
        textAlign: "center",
        textColor: foreground,
        fontFamily: typography.fontFamily,
        fontSize: typography.fontSize,
        fontStyle: typography.fontStyle,
        fontWeight: typography.fontWeight,
        lineHeight: typography.lineHeight,
        whiteSpace: "nowrap",
      },
    });
  }
  return { id: badge.id, type: "group", frame: 0, box, style: { overflow: "visible" }, children };
}
