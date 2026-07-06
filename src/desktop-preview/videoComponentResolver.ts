import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import type { VideoDesignContract } from "./videoComponentContract.js";
import {
  asRecord,
  parseObject,
  requiredBoolean,
  requiredIconSlots,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveVideoComponent(
  payload: DesignPreviewPayload,
): VideoDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const video = asRecord(config.video);
  const surfaceSlot = asRecord(video.surfaceSlot);
  const surfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", surfaceSlot.presetId),
    asRecord(surfaceSlot.overrides),
  );

  return {
    id: "component.video",
    durationText: requiredString(preview, "sampleText", "component.video.preview.sampleText"),
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
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      { width: 520, height: 293 },
      "component.video.surface",
    ),
  };
}
