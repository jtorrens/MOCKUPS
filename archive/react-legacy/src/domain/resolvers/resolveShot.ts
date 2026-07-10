import type { DomainRepository } from "../repository/types.js";
import type { Shot } from "../schemas/index.js";
import { requireRecord } from "./helpers.js";
import {
  resolveScreenInstance,
  type ResolvedScreenInstance,
} from "./resolveScreenInstance.js";
import {
  applyTimelineToScreenInstance,
  computeScreenTimeline,
  computedShotDuration,
  timelineScreenForFrame,
} from "../timeline/screenTimeline.js";

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

function validateShotFrame(
  shot: Shot,
  shotFrame: number,
  computedDurationFrames?: number,
): void {
  if (!Number.isInteger(shotFrame) || shotFrame < 0) {
    throw new Error("shotFrame must be a non-negative integer");
  }
  const durationFrames =
    computedDurationFrames && computedDurationFrames > 0
      ? computedDurationFrames
      : shot.duration_frames;
  if (shotFrame >= durationFrames) {
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
  const screenTimeline = computeScreenTimeline(
    repository.getScreenInstancesForShot(shot.id),
  );
  validateShotFrame(
    shot,
    shotFrame,
    computedShotDuration(screenTimeline.map((entry) => entry.screen)),
  );

  const activeScreenInstances = screenTimeline
    .filter((entry) => timelineScreenForFrame(entry, shotFrame))
    .map((entry) =>
      resolveScreenInstance({
        repository,
        screenInstance: {
          ...applyTimelineToScreenInstance(entry),
          start_frame:
            shotFrame < entry.startFrame ? shotFrame : entry.startFrame,
        },
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
