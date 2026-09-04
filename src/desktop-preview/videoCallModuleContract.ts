import type { CallParticipantDesignContract } from "./callParticipantComponentContract.js";
import type { AlignmentPlacementContract, SpacingPairContract } from "./previewComponentContracts.js";
import type { SocialPostRow } from "./socialPostModuleContract.js";

export interface VideoCallComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export type VideoCallParticipantRole = "main" | "pip" | "grid";

export interface VideoCallParticipant {
  id: string;
  role: VideoCallParticipantRole;
  participant: CallParticipantDesignContract;
}

export interface VideoCallModuleContract {
  id: "module.core.videoCall";
  useAppWallpaper: boolean;
  conversationType: "individual" | "group";
  backgroundColorToken: string;
  showHeader: boolean;
  headerLayoutMode: "stack" | "float";
  headerHeight: number;
  headerSurfaceSlot: VideoCallComponentSlot;
  headerRowGapToken: string;
  headerRows: [SocialPostRow, SocialPostRow];
  showFooter: boolean;
  footerLayoutMode: "stack" | "float";
  footerHeight: number;
  footerSurfaceSlot: VideoCallComponentSlot;
  footerRowGapToken: string;
  footerRows: [SocialPostRow, SocialPostRow];
  showMainVideo: boolean;
  mainSizeMode: "fill" | "fixed";
  mainSize: { width: number; height: number };
  mainPlacement: AlignmentPlacementContract;
  mainPadding: SpacingPairContract;
  showPip: boolean;
  pipSize: { width: number; height: number };
  pipPlacement: AlignmentPlacementContract;
  pipPadding: SpacingPairContract;
  showGridParticipants: boolean;
  gridPadding: SpacingPairContract;
  gridGapToken: string;
  gridColumns: number;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  participants: VideoCallParticipant[];
  statusBarSlot: VideoCallComponentSlot;
  navigationBarSlot: VideoCallComponentSlot;
}
