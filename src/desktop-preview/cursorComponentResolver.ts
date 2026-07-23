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
  return resolveCursorComponentFromRecords(config, preview, "component.cursor");
}

export function resolveCursorComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  id: string,
): CursorDesignContract {
  const cursor = requiredRecord(config, "cursor", "component.cursor");

  return {
    id,
    height: requiredNumber(inputs, "height", "component.cursor.input.height"),
    colorToken: requiredString(cursor, "colorToken", "component.cursor.colorToken"),
    width: requiredNumber(cursor, "width", "component.cursor.width"),
    minimumFade: requiredAlpha(cursor, "minimumFade", "component.cursor.minimumFade"),
    fadeDurationMs: requiredNumber(
      cursor,
      "fadeDurationMs",
      "component.cursor.fadeDurationMs",
    ),
  };
}

export function resolveCursorComponentAtHeight(
  config: Record<string, unknown>,
  height: number,
  id: string,
): CursorDesignContract {
  return resolveCursorComponentFromRecords(config, { height }, id);
}
