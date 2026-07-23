import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  parseObject,
  requiredAlpha,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredRecord,
  requiredString,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";
import type {
  SurfaceDesignContract,
  SurfaceTailContract,
} from "./surfaceComponentContract.js";

export function resolveSurfaceComponent(
  payload: DesignPreviewPayload,
): SurfaceDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  return resolveSurfaceComponentFromRecords(config, preview, "component.surface");
}

export function resolveSurfaceComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  id: string,
): SurfaceDesignContract {
  const surface = requiredRecord(config, "surface", "component.surface");
  const tail = requiredRecord(surface, "tail", "component.surface.tail");
  const style = requiredRecord(config, "style", "component.surface.style");
  const size = requiredNumberPair(inputs, "size", "component.surface.input.size");

  return {
    id,
    width: size.first,
    height: size.second,
    backgroundColorToken: requiredString(
      surface,
      "backgroundColorToken",
      "component.surface.backgroundColorToken",
    ),
    backgroundAlpha: requiredAlpha(
      surface,
      "backgroundAlpha",
      "component.surface.backgroundAlpha",
    ),
    borderAlpha: requiredAlpha(
      surface,
      "borderAlpha",
      "component.surface.borderAlpha",
    ),
    tail: resolveSurfaceTail(tail),
    surface: resolveSurfaceStyle(style),
  };
}

export function resolveSurfaceComponentAtSize(
  config: Record<string, unknown>,
  size: { width: number; height: number },
  id: string,
): SurfaceDesignContract {
  return resolveSurfaceComponentFromRecords(
    config,
    { size: `${size.width}|${size.height}` },
    id,
  );
}

function resolveSurfaceTail(
  tail: Record<string, unknown>,
): SurfaceTailContract {
  const style = requiredString(tail, "style", "component.surface.tail.style");
  if (
    style !== "rounded_wedge" &&
    style !== "curved_hook" &&
    style !== "simple_triangle" &&
    style !== "cut_corner"
  ) {
    throw new Error(`Unsupported surface tail style ${style}`);
  }

  const side = requiredString(tail, "side", "component.surface.tail.side");
  if (side !== "left" && side !== "right") {
    throw new Error(`Unsupported surface tail side ${side}`);
  }

  const vertical = requiredString(
    tail,
    "vertical",
    "component.surface.tail.vertical",
  );
  if (vertical !== "top" && vertical !== "bottom") {
    throw new Error(`Unsupported surface tail vertical ${vertical}`);
  }

  const size = requiredNumberPair(tail, "size", "component.surface.tail.size");
  return {
    enabled: requiredBoolean(
      tail,
      "enabled",
      "component.surface.tail.enabled",
    ),
    style,
    side,
    vertical,
    width: Math.max(0, size.first),
    height: Math.max(0, size.second),
    outerCornerRadius: Math.max(
      0,
      requiredNumber(
        tail,
        "outerCornerRadius",
        "component.surface.tail.outerCornerRadius",
      ),
    ),
  };
}
