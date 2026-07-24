import type { ComponentMotionContract } from "./previewComponentContracts.js";
import { requiredMotionContract } from "./previewMotionHelpers.js";

export function optionalComponentBoundaryMotion(
  config: Record<string, unknown>,
  path: string,
): ComponentMotionContract | undefined {
  return Object.hasOwn(config, "boundaryMotion")
    ? requiredMotionContract(config, "boundaryMotion", `${path}.boundaryMotion`)
    : undefined;
}

export function requiredComponentBoundaryMotion(
  config: Record<string, unknown>,
  path: string,
): ComponentMotionContract {
  return requiredMotionContract(config, "boundaryMotion", `${path}.boundaryMotion`);
}
