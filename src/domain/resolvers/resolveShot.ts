import type { DomainRepository } from "../repository/types.js";
import type { Shot } from "../schemas/index.js";
import { requireRecord } from "./helpers.js";
import {
  resolveScreenInstance,
  type ResolvedScreenInstance,
} from "./resolveScreenInstance.js";

export interface ResolveShotInput {
  repository: DomainRepository;
  productionId: string;
  shotId: string;
  shotFrame: number;
}

export interface ResolvedShot {
  production_id: string;
  shot_id: string;
  shot_frame: number;
  fps: number;
  active_screen_instances: ResolvedScreenInstance[];
}

function validateShotFrame(shot: Shot, shotFrame: number): void {
  if (!Number.isInteger(shotFrame) || shotFrame < 0) {
    throw new Error("shotFrame must be a non-negative integer");
  }
  if (shotFrame >= shot.duration_frames) {
    throw new Error(
      `shotFrame ${shotFrame} is outside shot ${shot.id} duration`,
    );
  }
}

export function resolveShot({
  repository,
  productionId,
  shotId,
  shotFrame,
}: ResolveShotInput): ResolvedShot {
  requireRecord(
    repository.getProduction(productionId),
    "Production",
    productionId,
  );
  const shot = requireRecord(repository.getShot(shotId), "Shot", shotId);
  if (shot.production_id !== productionId) {
    throw new Error(`Shot ${shotId} does not belong to production ${productionId}`);
  }
  validateShotFrame(shot, shotFrame);

  const activeScreenInstances = repository
    .getScreenInstancesForShot(shot.id)
    .filter(
      (instance) =>
        shotFrame >= instance.start_frame && shotFrame < instance.end_frame,
    )
    .map((screenInstance) =>
      resolveScreenInstance({
        repository,
        screenInstance,
        shotOwnerActorId: shot.owner_actor_id,
        shotFrame,
        fps: shot.fps,
      }),
    );

  return {
    production_id: productionId,
    shot_id: shot.id,
    shot_frame: shotFrame,
    fps: shot.fps,
    active_screen_instances: activeScreenInstances,
  };
}
