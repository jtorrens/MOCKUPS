import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type {
  ComponentMotionContract,
  ComponentMotionFrameContract,
} from "./previewComponentContracts.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { asRecord, parseObject } from "./previewJsonHelpers.js";
import { requiredNumberValue, stringValue } from "./previewValueHelpers.js";

interface MotionTiming {
  durationMs: number;
  delayMs: number;
  easing: string;
  intensity: number;
}

export function wrapMotionFrame(
  payload: DesignPreviewPayload,
  node: RenderableNode,
  motion: ComponentMotionContract,
  frame: ComponentMotionFrameContract,
  finalBox: RenderableBox,
  parentBox: RenderableBox,
): RenderableNode {
  if (!frame.trigger || (motion.transition === "none" && !motion.fade)) {
    return node;
  }

  const timing = motionTiming(payload, motion.transition === "none" ? "fade" : motion.transition);
  if (timing.durationMs <= 0) {
    return node;
  }

  const elapsedMs = frame.elapsedMs;
  const linearProgress = linearMotionProgress(elapsedMs, timing);
  const progress = easingProgress(timing.easing, linearProgress, timing.intensity);
  const startBox = motion.translate
    ? entranceStartBox(finalBox, parentBox, motion.direction)
    : finalBox;
  const currentBox = {
    x: lerp(startBox.x, finalBox.x, progress),
    y: lerp(startBox.y, finalBox.y, progress),
    width: finalBox.width,
    height: finalBox.height,
  };
  const translateX = currentBox.x - finalBox.x;
  const translateY = currentBox.y - finalBox.y;
  const currentScale = motion.scale
    ? lerp(0.92, 1, progress)
    : 1;
  const currentOpacity = motion.fade ? clampedProgress(progress) : 1;

  return {
    id: `${node.id}.motion`,
    type: "group",
    frame: node.frame ?? 0,
    box: parentBox,
    style: {
      overflow: "hidden",
    },
    children: [
      {
        ...node,
        box: finalBox,
        transform: {
          ...(node.transform ?? {}),
          x: translateX,
          y: translateY,
          opacity: currentOpacity,
          scale: currentScale,
        },
      },
    ],
  };
}

export function wrapExitMotionFrame(
  payload: DesignPreviewPayload,
  node: RenderableNode,
  motion: ComponentMotionContract,
  frame: ComponentMotionFrameContract,
  finalBox: RenderableBox,
  parentBox: RenderableBox,
): RenderableNode {
  if (!frame.trigger || (motion.transition === "none" && !motion.fade)) return node;
  const timing = motionTiming(payload, motion.transition === "none" ? "fade" : motion.transition);
  if (timing.durationMs <= 0) return node;
  const progress = easingProgress(
    timing.easing,
    linearMotionProgress(frame.elapsedMs, timing),
    timing.intensity,
  );
  const endBox = motion.translate ? entranceStartBox(finalBox, parentBox, motion.direction) : finalBox;
  return {
    id: `${node.id}.exit-motion`,
    type: "group",
    frame: node.frame ?? 0,
    box: parentBox,
    style: { overflow: "hidden" },
    children: [{
      ...node,
      box: finalBox,
      transform: {
        ...(node.transform ?? {}),
        x: lerp(0, endBox.x - finalBox.x, progress),
        y: lerp(0, endBox.y - finalBox.y, progress),
        opacity: motion.fade ? 1 - clampedProgress(progress) : 1,
        scale: motion.scale ? lerp(1, 0.92, progress) : 1,
      },
    }],
  };
}

export function motionTotalDurationMs(payload: DesignPreviewPayload, motion: ComponentMotionContract) {
  if (motion.transition === "none" && !motion.fade) return 0;
  const timing = motionTiming(payload, motion.transition === "none" ? "fade" : motion.transition);
  return Math.max(0, timing.delayMs + timing.durationMs);
}

export function motionFrameProgress(
  payload: DesignPreviewPayload,
  motion: ComponentMotionContract,
  frame: ComponentMotionFrameContract,
) {
  if (!frame.trigger || (motion.transition === "none" && !motion.fade)) {
    return 1;
  }

  const timing = motionTiming(payload, motion.transition === "none" ? "fade" : motion.transition);
  if (timing.durationMs <= 0) {
    return 1;
  }

  const elapsedMs = frame.elapsedMs;
  const linearProgress = linearMotionProgress(elapsedMs, timing);
  return easingProgress(timing.easing, linearProgress, timing.intensity);
}

export function requiredMotionContract(
  value: Record<string, unknown>,
  key: string,
  path: string,
): ComponentMotionContract {
  const raw = asRecord(value[key]);
  const transition = stringValue(raw.transition);
  const direction = stringValue(raw.direction);
  const bounds = stringValue(raw.bounds);
  if (!isTransition(transition)) {
    throw new Error(`Unsupported motion transition ${path}.transition`);
  }
  if (!isDirection(direction)) {
    throw new Error(`Unsupported motion direction ${path}.direction`);
  }
  if (bounds !== "parent" && bounds !== "screen") {
    throw new Error(`Unsupported motion bounds ${path}.bounds`);
  }

  return {
    transition,
    direction,
    bounds,
    fade: booleanValue(raw.fade, `${path}.fade`),
    translate: booleanValue(raw.translate, `${path}.translate`),
    scale: booleanValue(raw.scale, `${path}.scale`),
  };
}

function motionTiming(payload: DesignPreviewPayload, transition: string): MotionTiming {
  const root = parseObject(payload.themeTokensJson);
  const timing = asRecord(asRecord(asRecord(root.motion).transitions)[transition]);
  return {
    durationMs: requiredNumberValue(timing.durationMs, `theme.motion.${transition}.durationMs`),
    delayMs: requiredNumberValue(timing.delayMs, `theme.motion.${transition}.delayMs`),
    easing: requiredEasing(timing.easing, `theme.motion.${transition}.easing`),
    intensity: requiredNumberValue(timing.intensity, `theme.motion.${transition}.intensity`),
  };
}

function requiredEasing(value: unknown, path: string) {
  const easing = stringValue(value);
  if (isEasing(easing)) {
    return easing;
  }

  throw new Error(`Missing easing value ${path}`);
}

function booleanValue(value: unknown, path: string) {
  if (typeof value === "boolean") return value;
  throw new Error(`Missing boolean value ${path}`);
}

function linearMotionProgress(elapsedMs: number, timing: MotionTiming) {
  const startMs = timing.delayMs;
  const endMs = timing.delayMs + timing.durationMs;
  const completionToleranceMs = 0.5;
  if (elapsedMs <= startMs) {
    return 0;
  }

  if (elapsedMs >= endMs - completionToleranceMs) {
    return 1;
  }

  return Math.max(0, Math.min(1, (elapsedMs - startMs) / timing.durationMs));
}

function entranceStartBox(
  finalBox: RenderableBox,
  parentBox: RenderableBox,
  direction: ComponentMotionContract["direction"],
): RenderableBox {
  switch (direction) {
    case "top":
      return { ...finalBox, y: parentBox.y - finalBox.height };
    case "bottom":
      return { ...finalBox, y: parentBox.y + parentBox.height };
    case "left":
      return { ...finalBox, x: parentBox.x - finalBox.width };
    case "right":
      return { ...finalBox, x: parentBox.x + parentBox.width };
  }
}

function easingProgress(easing: string, progress: number, intensity: number) {
  if (progress <= 0 || progress >= 1 || easing === "linear") {
    return progress;
  }

  if (easing === "spring") {
    return applyEasingIntensity(
      progress,
      1 - Math.exp(-6 * progress) * Math.cos(10 * progress),
      intensity,
    );
  }
  if (easing === "bounce") {
    return applyEasingIntensity(progress, bounceOut(progress), intensity);
  }

  const bezier = easingBezier(easing);
  if (!bezier) {
    throw new Error(`Unsupported easing ${easing}`);
  }
  return bezierYForX(bezier.x1, bezier.y1, bezier.x2, bezier.y2, progress);
}

function applyEasingIntensity(progress: number, eased: number, intensity: number) {
  const amount = Number.isFinite(intensity) ? Math.max(0, intensity) : 1;
  return progress + (eased - progress) * amount;
}

function easingBezier(easing: string) {
  if (easing === "ease-in") return { x1: 0.4, y1: 0, x2: 1, y2: 1 };
  if (easing === "ease-out") return { x1: 0, y1: 0, x2: 0.2, y2: 1 };
  if (easing === "ease") return { x1: 0.4, y1: 0, x2: 0.2, y2: 1 };
  return null;
}

function bounceOut(progress: number) {
  const n1 = 7.5625;
  const d1 = 2.75;
  if (progress < 1 / d1) {
    return n1 * progress * progress;
  }
  if (progress < 2 / d1) {
    const shifted = progress - 1.5 / d1;
    return n1 * shifted * shifted + 0.75;
  }
  if (progress < 2.5 / d1) {
    const shifted = progress - 2.25 / d1;
    return n1 * shifted * shifted + 0.9375;
  }
  const shifted = progress - 2.625 / d1;
  return n1 * shifted * shifted + 0.984375;
}

function isEasing(easing: string) {
  return easing === "linear"
    || easing === "ease-in"
    || easing === "ease-out"
    || easing === "ease"
    || easing === "spring"
    || easing === "bounce";
}

function clampedProgress(progress: number) {
  return Math.max(0, Math.min(1, progress));
}

function bezierYForX(x1: number, y1: number, x2: number, y2: number, x: number) {
  let lower = 0;
  let upper = 1;
  let t = x;
  for (let i = 0; i < 18; i++) {
    t = (lower + upper) / 2;
    const currentX = cubicBezier(0, x1, x2, 1, t);
    if (currentX < x) {
      lower = t;
    } else {
      upper = t;
    }
  }

  return cubicBezier(0, y1, y2, 1, t);
}

function cubicBezier(p0: number, p1: number, p2: number, p3: number, t: number) {
  const inverse = 1 - t;
  return inverse ** 3 * p0
    + 3 * inverse ** 2 * t * p1
    + 3 * inverse * t ** 2 * p2
    + t ** 3 * p3;
}

function lerp(start: number, end: number, amount: number) {
  return start + (end - start) * amount;
}

function isTransition(value: string): value is ComponentMotionContract["transition"] {
  return value === "none"
    || value === "slide"
    || value === "swipe"
    || value === "scale";
}

function isDirection(value: string): value is ComponentMotionContract["direction"] {
  return value === "top" || value === "bottom" || value === "left" || value === "right";
}
