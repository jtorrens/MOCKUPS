import { asRecord, optionalNumber, optionalString } from "./componentResolverCommon.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";

type JsonRecord = Record<string, unknown>;

export function resolveBehaviorTimingFrames(
  owner: JsonRecord,
  definition: JsonRecord,
  ownerFields: JsonRecord[],
  themeTokens: JsonRecord,
) {
  const value = asRecord(owner[optionalString(definition, "jsonKey")]);
  const mode = optionalString(value, "mode");
  if (mode === "fixed") return Math.max(0, Math.floor(optionalNumber(value, "fixedFrames", 0)));
  if (mode !== "natural") throw new Error(`Invalid behavior timing mode '${mode}'.`);

  const natural = asRecord(definition.naturalTiming);
  const sourceId = optionalString(natural, "sourceFieldId");
  const source = ownerFields.find((field) => optionalString(field, "id") === sourceId);
  if (!source) throw new Error(`Missing behavior timing source field '${sourceId}'.`);
  const unit = optionalString(natural, "unit");
  if (unit !== "grapheme") throw new Error(`Unsupported natural timing unit '${unit}'.`);
  const units = textGraphemes(optionalString(owner, optionalString(source, "jsonKey"))).length;
  const paceToken = optionalString(value, "paceToken");
  if (!paceToken.startsWith("theme.motion.naturalPace.")) {
    throw new Error(`Invalid natural pace token '${paceToken}'.`);
  }
  const multiplier = tokenNumber(themeTokens, paceToken);
  if (multiplier <= 0) throw new Error(`Natural pace token '${paceToken}' must be positive.`);
  return Math.max(0, Math.round(units * optionalNumber(natural, "baseFramesPerUnit", 0) * multiplier));
}

export function naturalWriteOnFrame(
  text: string,
  timingValue: unknown,
  elapsedFrame: number,
  durationFrames: number,
  seed: string,
) {
  const timing = asRecord(timingValue);
  if (optionalString(timing, "mode") !== "natural") return Math.max(0, elapsedFrame);
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
  let current: unknown = tokens;
  for (const segment of path) current = asRecord(current)[segment];
  return typeof current === "number" ? current : Number.NaN;
}

function stableUnit(value: string) {
  let hash = 2166136261;
  for (const character of value) {
    hash ^= character.codePointAt(0) ?? 0;
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0) / 0xffffffff;
}
