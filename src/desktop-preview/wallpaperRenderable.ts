import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  parseObject,
  requiredNumber,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { optionalObject } from "./previewJsonHelpers.js";
import { selectedPaletteColor } from "./componentRenderableCommon.js";
import { mediaFrameUriForPath } from "./previewAssetResolver.js";

export function wallpaperRenderable(
  payload: DesignPreviewPayload,
  box: RenderableBox,
): RenderableNode | undefined {
  const appConfig = parseObject(payload.appConfigJson);
  const runtime = parseObject(payload.designPreviewJson);
  const appOwner = appWallpaperOwner(appConfig);
  const actorConfig = optionalObject(runtime, "actor", "wallpaper runtime");
  const ownerConfig = appOwner ?? actorWallpaperOwner(actorConfig);
  if (!ownerConfig) return undefined;

  const wallpaper = requiredRecord(ownerConfig, "wallpaper", "wallpaper owner");
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

function appWallpaperOwner(config: Record<string, unknown>) {
  if (!Object.hasOwn(config, "wallpaper") || config.wallpaper === null) return undefined;
  requiredRecord(config, "wallpaper", "App wallpaper");
  return config;
}

function actorWallpaperOwner(config: Record<string, unknown>) {
  if (Object.keys(config).length === 0) return undefined;
  requiredRecord(config, "wallpaper", "Actor wallpaper");
  return config;
}
