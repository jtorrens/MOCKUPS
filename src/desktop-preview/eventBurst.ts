import { isRecord } from "./previewJsonHelpers.js";
import { requiredNumberValue } from "./previewValueHelpers.js";

export function eventBurst(value: unknown) {
  if (!isRecord(value)) throw new Error("Event track requires a prepared burst");
  const count = requiredNumberValue(value.count, "Event burst count");
  const interval = requiredNumberValue(value.intervalFrames, "Event burst interval");
  if (!Number.isInteger(count) || count < 1 || count > 1000
      || !Number.isInteger(interval) || interval < 0 || interval > 10000)
    throw new Error("Event burst count or interval is out of range");
  return { count, interval, lastOffset: (count - 1) * interval };
}
