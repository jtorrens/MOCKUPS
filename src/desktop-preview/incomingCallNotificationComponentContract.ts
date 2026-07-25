import type { RenderableBox } from "../visual/renderable/types.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import type {
  AlignmentPlacementContract,
  ComponentMotionContract,
  SpacingPairContract,
} from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface IncomingCallNotificationDesignContract {
  id: "component.incomingCallNotification";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  present: boolean;
  presenceTransition: boolean;
  presenceElapsedMs: number;
  boundaryMotion: ComponentMotionContract;
  surface: SurfaceDesignContract;
  avatar: AvatarDesignContract;
  avatarPlacement: AlignmentPlacementContract;
  iconRow: IconRowDesignContract;
  iconRowPlacement: AlignmentPlacementContract;
}

export type IncomingCallNotificationAssignedBox = RenderableBox;
