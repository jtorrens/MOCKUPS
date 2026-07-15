import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import type { AlignmentPlacementContract, SpacingPairContract } from "./previewComponentContracts.js";

export interface NotificationDesignContract {
  id: "component.notification";
  maxWidth: number;
  dimensionMode: "fixed" | "content";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  gapToken: string;
  avatarPlacement: AlignmentPlacementContract;
  labelPlacement: AlignmentPlacementContract;
  surface: SurfaceDesignContract;
  avatar: AvatarDesignContract;
  label: LabelDesignContract;
  reflow?: {
    progress: number;
    fromLabel: LabelDesignContract;
  };
}
