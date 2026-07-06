import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { VideoDesignContract } from "./videoComponentContract.js";
import {
  asRecord,
  parseObject,
  requiredAlpha,
  requiredBoolean,
  requiredIconSlots,
  requiredNumber,
  requiredString,
  resolveSurfaceStyle,
} from "./componentResolverCommon.js";

export function resolveVideoComponent(
  payload: DesignPreviewPayload,
): VideoDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const video = asRecord(config.video);
  const style = asRecord(config.style);

  return {
    id: "component.video",
    durationText: requiredString(preview, "sampleText", "component.video.preview.sampleText"),
    backgroundColorToken: requiredString(
      video,
      "backgroundColorToken",
      "component.video.backgroundColorToken",
    ),
    backgroundAlpha: requiredAlpha(
      video,
      "backgroundAlpha",
      "component.video.backgroundAlpha",
    ),
    statusVisible: requiredBoolean(
      video,
      "statusVisible",
      "component.video.statusVisible",
    ),
    statusHeight: requiredNumber(
      video,
      "statusHeight",
      "component.video.statusHeight",
    ),
    statusIconSlots: requiredIconSlots(
      video,
      "statusIconSlots",
      "component.video.statusIconSlots",
    ),
    statusTextColorToken: requiredString(
      video,
      "statusTextColorToken",
      "component.video.statusTextColorToken",
    ),
    playOverlayVisible: requiredBoolean(
      video,
      "playOverlayVisible",
      "component.video.playOverlayVisible",
    ),
    playColorToken: requiredString(
      video,
      "playColorToken",
      "component.video.playColorToken",
    ),
    surface: resolveSurfaceStyle(style),
  };
}
