import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  asRecord,
  parseObject,
  requiredAlpha,
  requiredNumberPair,
  requiredString,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

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
  const surface = asRecord(config.surface);
  const style = asRecord(config.style);
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
    surface: resolveSurfaceStyle(style),
  };
}
