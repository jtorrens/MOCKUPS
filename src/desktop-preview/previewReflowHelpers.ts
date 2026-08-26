import {
  optionalNumber,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import { easingProgress } from "./previewMotionHelpers.js";

export interface ReflowTimingContract {
  durationMs: number;
  easing: string;
  intensity: number;
}

export function requiredReflowTiming(
  value: Record<string, unknown>,
  path: string,
): ReflowTimingContract {
  const durationMs = requiredNumber(value, "durationMs", `${path}.durationMs`);
  if (durationMs < 0) throw new Error(`${path}.durationMs must be non-negative`);
  return {
    durationMs,
    easing: requiredString(value, "easing", `${path}.easing`),
    intensity: optionalNumber(value, "intensity", 1),
  };
}

export function requiredThemeReflowTiming(
  value: Record<string, unknown>,
): ReflowTimingContract {
  const durationMs = requiredNumber(
    value,
    "reflowDurationMs",
    "theme.motion.reflowDurationMs",
  );
  if (durationMs < 0) {
    throw new Error("theme.motion.reflowDurationMs must be non-negative");
  }
  return {
    durationMs,
    easing: requiredString(value, "reflowEasing", "theme.motion.reflowEasing"),
    intensity: optionalNumber(value, "reflowIntensity", 1),
  };
}

export function resolveReflowProgress(
  timing: ReflowTimingContract,
  elapsedMs: number,
) {
  if (timing.durationMs <= 0) return 1;
  return easingProgress(
    timing.easing,
    Math.max(0, elapsedMs) / timing.durationMs,
    timing.intensity,
  );
}
