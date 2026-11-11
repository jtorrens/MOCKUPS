import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { RenderableNode, RenderableTransform } from "../visual/renderable/types.js";
import { requiredBoolean, requiredString } from "./componentResolverCommon.js";

export type VisualAccentClock = Pick<DesignPreviewPayload, "localFrame" | "frameRate">;
export type VisualAccentFrame = Required<Pick<RenderableTransform, "scale" | "x" | "rotation">>;

/** A decorative cycle samples the existing owner clock; it never owns duration. */
export function resolveVisualAccent(inputs: Record<string, unknown>, clock: VisualAccentClock): VisualAccentFrame {
  const type = requiredString(inputs, "accentType", "visual accent");
  const active = requiredBoolean(inputs, "accentActive", "visual accent");
  if (type !== "pulse" && type !== "vibrate" && type !== "rock") {
    throw new Error(`Unsupported visual accent '${type}'`);
  }
  if (!Number.isFinite(clock.localFrame) || !Number.isFinite(clock.frameRate) || clock.frameRate <= 0) {
    throw new Error("Visual accent requires a finite owner frame and positive frame rate");
  }
  const neutral = { scale: 1, x: 0, rotation: 0 };
  if (!active) return neutral;
  const seconds = Math.max(0, clock.localFrame) / clock.frameRate;
  const phase = (seconds % 1.2) / 1.2;
  if (type === "pulse") return { ...neutral, scale: 1 + 0.08 * (1 - Math.cos(phase * Math.PI * 2)) / 2 };
  // A short ringing burst followed by a quiet interval, with neutral endpoints.
  if (phase >= 0.5) return neutral;
  const burst = phase * 2;
  const wave = Math.sin(burst * Math.PI * 8) * Math.sin(burst * Math.PI);
  return type === "vibrate"
    ? { ...neutral, x: 3 * wave }
    : { ...neutral, rotation: 10 * wave };
}

/** Separate transform layer preserves pressed scale, Motion and child placement. */
export function wrapVisualAccent(node: RenderableNode, accent: VisualAccentFrame, unitScale: number): RenderableNode {
  if (accent.scale === 1 && accent.x === 0 && accent.rotation === 0) return node;
  return {
    id: `${node.id}.accent`, type: "group", box: node.box, frame: node.frame,
    transform: { scale: accent.scale, x: accent.x * unitScale, rotation: accent.rotation },
    style: { overflow: "visible" }, children: [node],
  };
}
