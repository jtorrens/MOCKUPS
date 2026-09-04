import type { ButtonDesignContract } from "./buttonComponentContract.js";
import type { CallParticipantDesignContract } from "./callParticipantComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { AlignmentPlacementContract, SpacingPairContract } from "./previewComponentContracts.js";

export interface VideoCallComponentSlot extends Record<string, unknown> { variantReference: string; overrides: Record<string, unknown>; }
export interface VideoCallParticipant { id: string; isSelf: boolean; participant: CallParticipantDesignContract; }
export interface VideoCallControl { id: string; button: ButtonDesignContract; }
export interface VideoCallModuleContract {
  id: "module.core.videoCall";
  useAppWallpaper: boolean;
  visibility: Record<string, boolean>;
  layoutMode: "automatic" | "spotlight" | "grid";
  activeParticipantId: string;
  screenPadding: SpacingPairContract;
  participantGapToken: string;
  headerHeight: number;
  controlsHeight: number;
  selfViewSize: { width: number; height: number };
  selfViewPlacement: AlignmentPlacementContract;
  backgroundColorToken: string;
  title: LabelDesignContract;
  participantCount: LabelDesignContract;
  duration: LabelDesignContract;
  participants: VideoCallParticipant[];
  controls: VideoCallControl[];
  backButton?: ButtonDesignContract;
  addButton?: ButtonDesignContract;
  statusBarSlot: VideoCallComponentSlot;
  navigationBarSlot: VideoCallComponentSlot;
}
