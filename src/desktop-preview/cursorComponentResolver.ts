import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  parseObject,
  requiredAlpha,
  requiredNumber,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import type { CursorDesignContract } from "./cursorComponentContract.js";

export function resolveCursorComponent(
  payload: DesignPreviewPayload,
): CursorDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  return resolveCursorComponentFromRecords(
    config,
    preview,
    "component.cursor",
    payload,
  );
}

export function resolveCursorComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  id: string,
  frameContext: Pick<DesignPreviewPayload, "localFrame" | "frameRate">,
): CursorDesignContract {
  const cursor = requiredRecord(config, "cursor", "component.cursor");
  const minimumFade = requiredAlpha(
    cursor,
    "minimumFade",
    "component.cursor.minimumFade",
  );
  const fadeDurationMs = requiredNumber(
    cursor,
    "fadeDurationMs",
    "component.cursor.fadeDurationMs",
  );
  if (fadeDurationMs <= 0) {
    throw new Error("component.cursor.fadeDurationMs must be positive");
  }

  return {
    id,
    height: requiredNumber(inputs, "height", "component.cursor.input.height"),
    colorToken: requiredString(cursor, "colorToken", "component.cursor.colorToken"),
    width: requiredNumber(cursor, "width", "component.cursor.width"),
    opacity: cursorOpacity(
      frameContext.localFrame,
      frameContext.frameRate,
      fadeDurationMs,
      minimumFade,
    ),
  };
}

export function resolveCursorComponentAtHeight(
  config: Record<string, unknown>,
  height: number,
  id: string,
  frameContext: Pick<DesignPreviewPayload, "localFrame" | "frameRate">,
): CursorDesignContract {
  return resolveCursorComponentFromRecords(config, { height }, id, frameContext);
}

function cursorOpacity(
  localFrame: number,
  frameRate: number,
  fadeDurationMs: number,
  minimumFade: number,
) {
  if (!Number.isFinite(localFrame) || localFrame < 0) {
    throw new Error("component.cursor.localFrame must be a non-negative number");
  }
  if (!Number.isFinite(frameRate) || frameRate <= 0) {
    throw new Error("component.cursor.frameRate must be positive");
  }
  const elapsedMs = (localFrame / frameRate) * 1000;
  const cyclePosition = (elapsedMs % (fadeDurationMs * 2)) / fadeDurationMs;
  const fadeProgress = cyclePosition <= 1 ? cyclePosition : 2 - cyclePosition;
  return 1 - ((1 - minimumFade) * fadeProgress);
}
