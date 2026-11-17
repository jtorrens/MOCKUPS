import type { ModuleSectionLayout } from "./moduleSectionLayout.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { MediaDesignContract } from "./mediaComponentContract.js";
import type { ModuleRow } from "./moduleRowSectionContract.js";
import type { ContentRowDesignContract } from "./contentRowComponentContract.js";
import type { AlignmentPlacementContract, ComponentMotionContract, ComponentMotionFrameContract, SpacingPairContract } from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface VideoCallComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export type VideoCallParticipantRole = "main" | "pip" | "grid";

export interface VideoCallParticipant {
  id: string;
  present: boolean;
  role: VideoCallParticipantRole;
  videoPresent: boolean;
  showActorName: boolean;
  connectionText: string;
  padding: SpacingPairContract;
  avatarSize: number;
  showSurface: boolean;
  showMedia: boolean;
  showFallbackAvatar: boolean;
  showFallbackStatus: boolean;
  surface: SurfaceDesignContract;
  media: MediaDesignContract;
  avatar?: AvatarDesignContract;
  nameLabel?: LabelDesignContract;
  statusLabel: LabelDesignContract;
  presenceMotionKind?: "enter" | "exit";
  presenceMotionFrame?: ComponentMotionFrameContract;
  reflowStartFrame?: number;
  reflowFromPresent: boolean;
}

export interface VideoCallModuleContract {
  id: "module.core.videoCall";
  useAppWallpaper: boolean;
  backgroundColorToken: string;
  showHeader: boolean;
  headerLayout: ModuleSectionLayout;
  headerHeight: number;
  headerSurfaceSlot: VideoCallComponentSlot;
  headerRowGapToken: string;
  headerRows: ModuleRow<ContentRowDesignContract>[];
  showFooter: boolean;
  footerLayout: ModuleSectionLayout;
  footerHeight: number;
  footerSurfaceSlot: VideoCallComponentSlot;
  footerRowGapToken: string;
  footerRows: ModuleRow<ContentRowDesignContract>[];
  showMainVideo: boolean;
  participantOuterPadding: SpacingPairContract;
  mainRows: ModuleRow<ContentRowDesignContract>[];
  showPip: boolean;
  pipSize: { width: number; height: number };
  pipPlacement: AlignmentPlacementContract;
  showGridParticipants: boolean;
  gridGapToken: string;
  gridHeightMode: "percent" | "fill";
  gridHeightPercent: number;
  gridRows: number;
  participantNamePlacement: AlignmentPlacementContract;
  participantEnterMotion: ComponentMotionContract;
  participantExitMotion: ComponentMotionContract;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  participants: VideoCallParticipant[];
  statusBarSlot: VideoCallComponentSlot;
  navigationBarSlot: VideoCallComponentSlot;
  participantReflow?: {
    progress: number;
    fromParticipants: VideoCallParticipant[];
  };
}
