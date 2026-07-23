import { isRecord } from "./previewJsonHelpers.js";
import {
  requiredNumberValue,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
} from "./previewValueHelpers.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";

type JsonRecord = Record<string, unknown>;

const NATURAL_PACE_TOKENS = new Set([
  "theme.motion.naturalPace.verySlow",
  "theme.motion.naturalPace.slow",
  "theme.motion.naturalPace.normal",
  "theme.motion.naturalPace.fast",
  "theme.motion.naturalPace.veryFast",
]);

export function resolveBehaviorTimingFrames(
  owner: JsonRecord,
  definition: JsonRecord,
  ownerFields: JsonRecord[],
  themeTokens: JsonRecord,
) {
  const fieldId = requiredString(definition, "id", "Behavior timing definition.id");
  const jsonKey = requiredString(definition, "jsonKey", `Behavior timing '${fieldId}'.jsonKey`);
  const value = requiredRecord(owner, jsonKey, `Behavior timing '${fieldId}' value`);
  const mode = requiredString(value, "mode", `Behavior timing '${fieldId}'.mode`);
  const fixedFrames = requiredInteger(value.fixedFrames, `Behavior timing '${fieldId}'.fixedFrames`);
  if (fixedFrames < 0) throw new Error(`Behavior timing '${fieldId}'.fixedFrames must be non-negative.`);
  const paceToken = requiredString(value, "paceToken", `Behavior timing '${fieldId}'.paceToken`);
  if (!NATURAL_PACE_TOKENS.has(paceToken)) {
    throw new Error(`Invalid natural pace token '${paceToken}'.`);
  }
  if (mode === "fixed") return fixedFrames;
  if (mode !== "natural") throw new Error(`Invalid behavior timing mode '${mode}'.`);

  const natural = requiredRecord(definition, "naturalTiming", `Behavior timing '${fieldId}'.naturalTiming`);
  const sourceId = requiredString(natural, "sourceFieldId", `Behavior timing '${fieldId}'.sourceFieldId`);
  const sources = ownerFields.filter((field, index) =>
    requiredString(field, "id", `Behavior timing owner field[${index}].id`) === sourceId);
  if (sources.length !== 1) throw new Error(`Missing or ambiguous behavior timing source field '${sourceId}'.`);
  const source = sources[0]!;
  if (!source) throw new Error(`Missing behavior timing source field '${sourceId}'.`);
  const unit = requiredString(natural, "unit", `Behavior timing '${fieldId}'.unit`);
  if (unit !== "grapheme") throw new Error(`Unsupported natural timing unit '${unit}'.`);
  const sourceJsonKey = requiredString(source, "jsonKey", `Behavior timing source '${sourceId}'.jsonKey`);
  const sourceText = requiredPossiblyEmptyString(owner, sourceJsonKey, `Behavior timing source '${sourceId}' value`);
  const units = textGraphemes(sourceText).length;
  const baseFramesPerUnit = requiredNumberValue(
    natural.baseFramesPerUnit,
    `Behavior timing '${fieldId}'.baseFramesPerUnit`,
  );
  if (baseFramesPerUnit <= 0) {
    throw new Error(`Behavior timing '${fieldId}'.baseFramesPerUnit must be positive.`);
  }
  const multiplier = tokenNumber(themeTokens, paceToken);
  if (!Number.isFinite(multiplier) || multiplier <= 0) {
    throw new Error(`Natural pace token '${paceToken}' must resolve to a positive finite number.`);
  }
  return Math.max(0, Math.round(units * baseFramesPerUnit * multiplier));
}

export function naturalWriteOnFrame(
  text: string,
  timingValue: unknown,
  elapsedFrame: number,
  durationFrames: number,
  seed: string,
) {
  if (!isRecord(timingValue)) {
    throw new Error("Natural Write On timing must be an object");
  }
  const mode = requiredString(timingValue, "mode", "Natural Write On timing.mode");
  if (mode === "fixed") return Math.max(0, elapsedFrame);
  if (mode !== "natural") throw new Error(`Invalid behavior timing mode '${mode}'.`);
  const graphemes = textGraphemes(text);
  if (graphemes.length === 0 || durationFrames <= 0) return durationFrames;
  const weights = graphemes.map((grapheme, index) => {
    const variance = 0.72 + stableUnit(`${seed}:${index}:${grapheme}`) * 0.56;
    const pause = /[.!?;:,]$/u.test(grapheme) ? 1.45 : /\s/u.test(grapheme) ? 1.12 : 1;
    return variance * pause;
  });
  const total = weights.reduce((sum, weight) => sum + weight, 0);
  const elapsed = Math.max(0, Math.min(durationFrames, elapsedFrame + 1));
  let cumulative = 0;
  let visible = 0;
  for (const weight of weights) {
    cumulative += weight;
    if ((cumulative / total) * durationFrames > elapsed) break;
    visible += 1;
  }
  if (elapsedFrame >= durationFrames) visible = graphemes.length;
  if (visible >= graphemes.length) return durationFrames;
  return Math.max(0, Math.ceil((visible * durationFrames) / graphemes.length) - 1);
}

function tokenNumber(tokens: JsonRecord, token: string) {
  const path = token.startsWith("theme.") ? token.slice("theme.".length).split(".") : token.split(".");
  if (path.length === 0) throw new Error(`Invalid Theme token path '${token}'.`);
  let current = tokens;
  const traversed = ["theme"];
  for (const segment of path.slice(0, -1)) {
    traversed.push(segment);
    current = requiredRecord(current, segment, traversed.join("."));
  }
  return requiredNumberValue(current[path.at(-1)!], token);
}

function requiredInteger(value: unknown, path: string) {
  const number = requiredNumberValue(value, path);
  if (Number.isInteger(number)) return number;
  throw new Error(`Missing integer value ${path}`);
}

function stableUnit(value: string) {
  let hash = 2166136261;
  for (const character of value) {
    hash ^= character.codePointAt(0) ?? 0;
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0) / 0xffffffff;
}
