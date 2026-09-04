import type { RenderableBox } from "../visual/renderable/types.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { MediaDesignContract } from "./mediaComponentContract.js";
import type { AlignmentPlacementContract, ComponentMotionContract, SpacingPairContract } from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type CallParticipantMicrophoneState = "on" | "muted";
export type CallParticipantConnectionState = "connecting" | "connected" | "weak" | "lost";

export interface CallParticipantDesignContract {
  id: "component.callParticipant";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  boundaryMotion: ComponentMotionContract;
  showBackground: boolean;
  showMedia: boolean;
  showAvatarWhenVideoAbsent: boolean;
  showStatusWhenVideoAbsent: boolean;
  showName: boolean;
  showMicrophoneStatus: boolean;
  showConnectionStatus: boolean;
  showActiveSpeakerIndicator: boolean;
  videoPresent: boolean;
  microphoneState: CallParticipantMicrophoneState;
  connectionState: CallParticipantConnectionState;
  speaking: boolean;
  activeSpeakerColorToken: string;
  activeSpeakerBorderWidth: number;
  microphoneIconToken: string;
  connectionIconToken?: string;
  surface: SurfaceDesignContract;
  media: MediaDesignContract;
  avatar: AvatarDesignContract;
  statusLabel: LabelDesignContract;
  nameLabel: LabelDesignContract;
  statusPlacement: AlignmentPlacementContract;
  namePlacement: AlignmentPlacementContract;
  microphonePlacement: AlignmentPlacementContract;
  connectionPlacement: AlignmentPlacementContract;
}

export type CallParticipantAssignedBox = RenderableBox;
