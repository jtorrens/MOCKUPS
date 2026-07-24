import type { RenderableBox } from "../visual/renderable/types.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type {
  ComponentMotionContract,
  SpacingPairContract,
} from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type IncomingCallNotificationLayout = "compact" | "stackedActions";

export interface IncomingCallNotificationDesignContract {
  id: "component.incomingCallNotification";
  layout: IncomingCallNotificationLayout;
  size: { width: number; height: number };
  padding: SpacingPairContract;
  contentGapToken: string;
  sectionGapToken: string;
  avatarSize: number;
  present: boolean;
  presenceTransition: boolean;
  presenceElapsedMs: number;
  boundaryMotion: ComponentMotionContract;
  surface: SurfaceDesignContract;
  avatar: AvatarDesignContract;
  label: LabelDesignContract;
  iconRow: IconRowDesignContract;
}

export type IncomingCallNotificationAssignedBox = RenderableBox;
