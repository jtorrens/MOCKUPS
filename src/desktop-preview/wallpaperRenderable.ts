import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  asRecord,
  parseObject,
  requiredNumber,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { selectedPaletteColor } from "./componentRenderableCommon.js";
import { mediaFrameUriForPath } from "./previewAssetResolver.js";

export function wallpaperRenderable(
  payload: DesignPreviewPayload,
  box: RenderableBox,
): RenderableNode | undefined {
  const appConfig = parseObject(payload.appConfigJson);
  const runtime = parseObject(payload.designPreviewJson);
  const actorConfig = asRecord(runtime.actor);
  const ownerConfig = isWallpaperOwner(appConfig) ? appConfig : actorConfig;
  if (!isWallpaperOwner(ownerConfig)) return undefined;

  const wallpaper = asRecord(ownerConfig.wallpaper);
  const opacity = Math.max(0, Math.min(1, requiredNumber(wallpaper, "opacity", "wallpaper.opacity")));
  if (opacity <= 0) return undefined;

  const modeId = payload.themeMode === "dark" ? "dark" : "light";
  const kind = requiredString(wallpaper, "kind", "wallpaper.kind");
  if (kind === "image") {
    const images = requiredRecord(wallpaper, "images", "wallpaper.images");
    const modeImage = requiredRecord(images, modeId, `wallpaper.images.${modeId}`);
    const filePath = requiredPossiblyEmptyString(modeImage, "filePath", `wallpaper.images.${modeId}.filePath`);
    if (filePath) {
      const frame = mediaFrameUriForPath(payload, filePath, 0);
      if (frame.uri) {
        return {
          id: "wallpaper.image",
          type: "image",
          frame: 0,
          box,
          asset: { type: "image", uri: frame.uri },
          style: { objectFit: "cover", opacity },
        };
      }
    }
  }

  if (kind !== "solid" && kind !== "image") return undefined;
  const modes = requiredRecord(ownerConfig, "modes", "modes");
  const mode = requiredRecord(modes, modeId, `modes.${modeId}`);
  const modeWallpaper = requiredRecord(mode, "wallpaper", `modes.${modeId}.wallpaper`);
  const colorToken = requiredString(modeWallpaper, "color", `modes.${modeId}.wallpaper.color`);
  return {
    id: "wallpaper.color",
    type: "surface",
    frame: 0,
    box,
    style: { background: selectedPaletteColor(payload, colorToken, opacity) },
  };
}

function isWallpaperOwner(config: Record<string, unknown>): boolean {
  return config.wallpaper !== null
    && typeof config.wallpaper === "object"
    && !Array.isArray(config.wallpaper);
}
