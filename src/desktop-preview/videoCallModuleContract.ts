import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { MediaDesignContract } from "./mediaComponentContract.js";
import type { ModuleRow } from "./moduleRowSectionContract.js";
import type { ContentRowDesignContract } from "./contentRowComponentContract.js";
import type { AlignmentPlacementContract, SpacingPairContract } from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface VideoCallComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export type VideoCallParticipantRole = "main" | "pip" | "grid";
export type VideoCallConnectionState = "connecting" | "connected" | "weak" | "lost";

export interface VideoCallParticipant {
  id: string;
  role: VideoCallParticipantRole;
  videoPresent: boolean;
  connectionState: VideoCallConnectionState;
  padding: SpacingPairContract;
  avatarSize: number;
  showSurface: boolean;
  showMedia: boolean;
  showFallbackAvatar: boolean;
  showFallbackStatus: boolean;
  surface: SurfaceDesignContract;
  media: MediaDesignContract;
  avatar?: AvatarDesignContract;
  statusLabel: LabelDesignContract;
}

export interface VideoCallModuleContract {
  id: "module.core.videoCall";
  useAppWallpaper: boolean;
  backgroundColorToken: string;
  showHeader: boolean;
  headerLayoutMode: "stack" | "float";
  headerFloatHorizontalPaddingToken: string;
  headerFloatOffsetY: number;
  headerHeight: number;
  headerSurfaceSlot: VideoCallComponentSlot;
  headerRowGapToken: string;
  headerRows: [ModuleRow<ContentRowDesignContract>, ModuleRow<ContentRowDesignContract>];
  showFooter: boolean;
  footerLayoutMode: "stack" | "float";
  footerFloatHorizontalPaddingToken: string;
  footerFloatOffsetY: number;
  footerHeight: number;
  footerSurfaceSlot: VideoCallComponentSlot;
  footerRowGapToken: string;
  footerRows: [ModuleRow<ContentRowDesignContract>, ModuleRow<ContentRowDesignContract>];
  showMainVideo: boolean;
  mainPadding: SpacingPairContract;
  mainRows: [ModuleRow<ContentRowDesignContract>, ModuleRow<ContentRowDesignContract>, ModuleRow<ContentRowDesignContract>];
  showPip: boolean;
  pipSize: { width: number; height: number };
  pipPlacement: AlignmentPlacementContract;
  pipPadding: SpacingPairContract;
  showGridParticipants: boolean;
  gridPadding: SpacingPairContract;
  gridGapToken: string;
  gridHeightMode: "fixed" | "fill";
  gridHeight: number;
  gridRows: number;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  participants: VideoCallParticipant[];
  statusBarSlot: VideoCallComponentSlot;
  navigationBarSlot: VideoCallComponentSlot;
}
