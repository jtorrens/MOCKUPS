import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { isRecord, optionalObject, parseObject } from "./previewJsonHelpers.js";
import {
  numberValue,
  requiredNumberValue,
  requiredRecord,
} from "./previewValueHelpers.js";

export function variants(payload: DesignPreviewPayload) {
  const tokens = parseObject(payload.themeTokensJson);
  const modes = requiredRecord(tokens, "modes", "theme.modes");
  const names = Object.keys(modes);
  if (names.length === 0) {
    throw new Error("Theme modes must contain at least one explicit mode");
  }
  return names;
}

function tokenValueForMode(
  payload: DesignPreviewPayload,
  token: string,
  mode: string,
) {
  if (!token.startsWith("theme.")) {
    throw new Error(`Unsupported theme token ${token}`);
  }

  const root = parseObject(payload.themeTokensJson);
  const modes = requiredRecord(root, "modes", "theme.modes");
  const modeRoot = requiredRecord(modes, mode, `theme.modes.${mode}`);
  const semanticKey = token.replace(/^theme\./, "");
  const parts = semanticKey.split(".");
  for (const [source, path] of [
    [modeRoot, `theme.modes.${mode}`],
    [root, "theme"],
  ] as const) {
    const current = optionalPathValue(source, parts, path);
    if (current !== undefined) return current;

    const colors = optionalObject(source, "colors", path);
    if (colors[semanticKey] !== undefined) return colors[semanticKey];
    if (colors[token] !== undefined) return colors[token];
  }

  throw new Error(`Missing theme token ${token} for mode ${mode}`);
}

export function resolvePaletteColor(payload: DesignPreviewPayload, value: unknown) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error("Missing palette color value");
  }
  if (/^#|^rgb|^hsl|^transparent$/i.test(value)) return value;
  const resolved = payload.paletteColors?.[value];
  if (!resolved) throw new Error(`Missing palette color ${value}`);
  return payload.paletteNeutralColors?.[value]
    ? applyNeutralTint(payload, resolved)
    : resolved;
}

export function colorForMode(
  payload: DesignPreviewPayload,
  token: string,
  mode: string,
  alpha = 1,
) {
  const raw = tokenValueForMode(payload, token, mode);
  const color =
    isRecord(raw)
      ? resolvePaletteColor(payload, raw.color)
      : resolvePaletteColor(payload, raw);
  const rawAlpha =
    isRecord(raw)
      ? numberValue(raw.alpha, 1)
      : 1;
  return cssColorWithAlpha(color, rawAlpha * alpha);
}

export function selectedColor(payload: DesignPreviewPayload, token: string, alpha = 1) {
  return colorForMode(payload, token, requiredThemeMode(payload), alpha);
}

export function selectedPaletteColor(payload: DesignPreviewPayload, token: string, alpha = 1) {
  return cssColorWithAlpha(resolvePaletteColor(payload, token), alpha);
}

export function numberToken(payload: DesignPreviewPayload, token: string) {
  const raw = tokenValueForMode(payload, token, requiredThemeMode(payload));
  const value = numberValue(raw, NaN);
  if (Number.isFinite(value)) return value;
  throw new Error(`Theme token ${token} is not numeric`);
}

export function numberOrThemeToken(payload: DesignPreviewPayload, value: number | string) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.startsWith("theme.")) {
    return numberToken(payload, value);
  }
  if (typeof value === "string") {
    const parsed = Number(value.replace(",", "."));
    if (Number.isFinite(parsed)) return parsed;
  }
  throw new Error(`Value ${value} is not numeric`);
}

export function stringThemeToken(payload: DesignPreviewPayload, token: string) {
  const raw = tokenValueForMode(payload, token, requiredThemeMode(payload));
  if (typeof raw === "string" && raw.trim()) return raw;
  throw new Error(`Theme token ${token} is not a string`);
}

export function stringOrThemeToken(payload: DesignPreviewPayload, value: string) {
  return value.startsWith("theme.") ? stringThemeToken(payload, value) : value;
}

export function cssColorWithAlpha(color: string, alpha: number) {
  if (color === "transparent") return color;
  const clamped = Math.max(0, Math.min(1, alpha));
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(color.trim());
  if (!match) return color;
  const sourceAlpha = match[2]
    ? Number.parseInt(match[2], 16) / 255
    : 1;
  const resolvedAlpha = Math.max(0, Math.min(1, clamped * sourceAlpha));
  const hex = match[1];
  if (resolvedAlpha >= 1) return `#${hex}`;
  const red = Number.parseInt(hex.slice(0, 2), 16);
  const green = Number.parseInt(hex.slice(2, 4), 16);
  const blue = Number.parseInt(hex.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${formatAlpha(resolvedAlpha)})`;
}

function formatAlpha(value: number) {
  return Number(value.toFixed(3)).toString();
}

function applyNeutralTint(payload: DesignPreviewPayload, color: string) {
  const root = parseObject(payload.themeTokensJson);
  const tint = requiredRecord(root, "neutralTint", "theme.neutralTint");
  const hueDeg = requiredNumberValue(tint.hueDeg, "theme.neutralTint.hueDeg");
  const saturation = Math.max(
    0,
    Math.min(
      1,
      requiredNumberValue(tint.saturation, "theme.neutralTint.saturation"),
    ),
  );
  if (saturation <= 0) return color;
  const parsed = parseHex(color);
  if (!parsed) return color;
  const [, , lightness] = rgbToHsl(parsed.red / 255, parsed.green / 255, parsed.blue / 255);
  const [red, green, blue] = hslToRgb(normalizeHue(hueDeg) / 360, saturation, lightness);
  const rgb = `#${byteHex(red * 255)}${byteHex(green * 255)}${byteHex(blue * 255)}`;
  return parsed.alpha === 255 ? rgb : `${rgb}${byteHex(parsed.alpha)}`;
}

function requiredThemeMode(payload: DesignPreviewPayload) {
  if (payload.themeMode === "light" || payload.themeMode === "dark") {
    return payload.themeMode;
  }
  throw new Error(`Unsupported Preview Theme mode ${payload.themeMode || "<empty>"}`);
}

function optionalPathValue(
  source: Record<string, unknown>,
  parts: string[],
  path: string,
) {
  let current: unknown = source;
  const traversed: string[] = [];
  for (const part of parts) {
    if (!isRecord(current)) {
      throw new Error(`Theme token path ${path}.${traversed.join(".")} must be an object`);
    }
    if (!Object.hasOwn(current, part)) return undefined;
    current = current[part];
    traversed.push(part);
  }
  return current;
}

function parseHex(color: string) {
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(color.trim());
  if (!match) return null;
  const hex = match[1];
  return {
    red: Number.parseInt(hex.slice(0, 2), 16),
    green: Number.parseInt(hex.slice(2, 4), 16),
    blue: Number.parseInt(hex.slice(4, 6), 16),
    alpha: match[2] ? Number.parseInt(match[2], 16) : 255,
  };
}

function byteHex(value: number) {
  return Math.max(0, Math.min(255, Math.round(value)))
    .toString(16)
    .padStart(2, "0")
    .toUpperCase();
}

function normalizeHue(value: number) {
  const normalized = value % 360;
  return normalized < 0 ? normalized + 360 : normalized;
}

function rgbToHsl(red: number, green: number, blue: number) {
  const max = Math.max(red, green, blue);
  const min = Math.min(red, green, blue);
  const lightness = (max + min) / 2;
  if (Math.abs(max - min) < 0.000001) return [0, 0, lightness] as const;
  const delta = max - min;
  const saturation =
    lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
  const hue =
    max === red
      ? (green - blue) / delta + (green < blue ? 6 : 0)
      : max === green
        ? (blue - red) / delta + 2
        : (red - green) / delta + 4;
  return [hue / 6, saturation, lightness] as const;
}

function hslToRgb(hue: number, saturation: number, lightness: number) {
  if (saturation <= 0) return [lightness, lightness, lightness] as const;
  const q =
    lightness < 0.5
      ? lightness * (1 + saturation)
      : lightness + saturation - lightness * saturation;
  const p = 2 * lightness - q;
  return [
    hueToRgb(p, q, hue + 1 / 3),
    hueToRgb(p, q, hue),
    hueToRgb(p, q, hue - 1 / 3),
  ] as const;
}

function hueToRgb(p: number, q: number, t: number) {
  let value = t;
  if (value < 0) value += 1;
  if (value > 1) value -= 1;
  if (value < 1 / 6) return p + (q - p) * 6 * value;
  if (value < 1 / 2) return q;
  if (value < 2 / 3) return p + (q - p) * (2 / 3 - value) * 6;
  return p;
}
