import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { parseObject, requiredObjectArray } from "./previewJsonHelpers.js";
import { requiredBoolean, requiredNumberValue, requiredRecord, requiredString } from "./previewValueHelpers.js";
import type { ReactionEmitterDesignContract } from "./reactionEmitterComponentContract.js";

function integer(owner: Record<string, unknown>, key: string, min: number, max: number) {
  const value = requiredNumberValue(owner[key], key);
  if (!Number.isInteger(value) || value < min || value > max) throw new Error(`Invalid emitter ${key}`);
  return value;
}

function unitSample(id: string) {
  let hash = 2166136261;
  for (const char of id) hash = Math.imul(hash ^ char.charCodeAt(0), 16777619);
  hash = Math.imul(hash ^ (hash >>> 16), 0x85ebca6b);
  hash = Math.imul(hash ^ (hash >>> 13), 0xc2b2ae35);
  return ((hash ^ (hash >>> 16)) >>> 0) / 0x100000000;
}

// Sample one destination per equal-width zone, then shuffle the birth order.
function burstDestinations(triggerId: string, count: number) {
  const destinations = Array.from({ length: count }, (_, zone) =>
    2 * (zone + unitSample(`${triggerId}:point:${zone}`)) / count - 1);
  for (let last = count - 1; last > 0; last--) {
    const pick = Math.floor(unitSample(`${triggerId}:shuffle:${last}`) * (last + 1));
    [destinations[last], destinations[pick]] = [destinations[pick]!, destinations[last]!];
  }
  return destinations;
}

export function resolveReactionEmitterComponent(payload: DesignPreviewPayload): ReactionEmitterDesignContract {
  const config = requiredRecord(parseObject(payload.configJson), "reactionEmitter", "Reaction Emitter");
  const runtime = parseObject(payload.designPreviewJson);
  const glyphs = new Map<string, string>();
  for (const item of requiredObjectArray(config, "reactions", "Reaction Emitter")) {
    const id = requiredString(item, "id", "Reaction id");
    if (glyphs.has(id)) throw new Error(`Duplicate reaction '${id}'`);
    glyphs.set(id, requiredString(item, "glyph", "Reaction emoticon"));
  }
  const duration = integer(config, "duration", 1, 1000);
  const entryFrames = integer(config, "entryFrames", 0, 1000);
  const initialScale = integer(config, "entryScale", 0, 100) / 100;
  const initialOpacity = integer(config, "entryOpacity", 0, 100) / 100;
  const fadeStart = integer(config, "fadeStart", 0, 100) / 100;
  const particles: ReactionEmitterDesignContract["particles"] = [];
  const catalog = requiredObjectArray(runtime, "catalog", "Reaction Emitter");
  for (const item of requiredBoolean(runtime, "showEmoticons", "Reaction Emitter") ? catalog : []) {
    const itemId = requiredString(item, "id", "Emission item");
    const glyph = glyphs.get(itemId);
    if (!glyph) throw new Error(`Unknown emission reaction '${itemId}'`);
    const destinationsByTrigger = new Map<string, number[]>();
    for (const event of requiredObjectArray(item, "emissions", "Emission item")) {
      const id = requiredString(event, "id", "Emission id");
      const age = integer(event, "age", 0, Number.MAX_SAFE_INTEGER);
      if (age >= duration) continue;
      const triggerId = requiredString(event, "triggerId", "Emission trigger");
      const count = integer(event, "burstCount", 1, 1000);
      const index = integer(event, "burstIndex", 0, count - 1);
      let destinations = destinationsByTrigger.get(triggerId);
      if (!destinations) {
        destinations = burstDestinations(triggerId, count);
        destinationsByTrigger.set(triggerId, destinations);
      }
      const progress = age / duration;
      const enter = entryFrames === 0 ? 1 : Math.min(1, age / entryFrames);
      const fade = progress <= fadeStart ? 1 : (1 - progress) / (1 - fadeStart);
      const rise = 1 - (1 - progress) ** 2;
      particles.push({ id, glyph, rise, drift: rise * destinations[index]!,
        scale: initialScale + (1 - initialScale) * enter,
        opacity: (initialOpacity + (1 - initialOpacity) * enter) * fade });
    }
  }
  return {
    size: integer(config, "size", 1, 512),
    riseHeightPercent: integer(config, "riseHeightPercent", 0, 100),
    dispersionWidth: integer(config, "dispersionWidth", 0, 2000),
    particles,
  };
}
