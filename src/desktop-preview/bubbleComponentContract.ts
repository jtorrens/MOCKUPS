import type {
  AlignmentPlacementContract,
  SpacingPairContract,
} from "./previewComponentContracts.js";
import type { AudioDesignContract } from "./audioComponentContract.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { MediaDesignContract } from "./mediaComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";

export type BubbleState = "incoming" | "system" | "outgoing";
export type BubbleMediaType = "none" | "image" | "video" | "audio";
export type BubbleMediaPosition = "top" | "bottom" | "left" | "right";
export type BubbleStatusState = "none" | "sent" | "delivered" | "read";

export interface BubblePalettePairContract {
  light: string;
  dark: string;
}

export interface BubbleStateColorContract {
  background: BubblePalettePairContract;
  text: BubblePalettePairContract;
}

export interface BubbleActorLabelSlotContract {
  showLabel: boolean;
  placement: AlignmentPlacementContract;
  label?: LabelDesignContract;
  textColorOverride?: string;
}

export interface BubbleAvatarSlotContract {
  showAvatar: boolean;
  placement: AlignmentPlacementContract;
  avatar?: AvatarDesignContract;
}

export interface BubbleMediaSlotContract {
  mediaType: BubbleMediaType;
  position: BubbleMediaPosition;
  media?: MediaDesignContract;
  audio?: AudioDesignContract;
}

export interface BubbleStatusIconContract {
  iconToken: string;
  colorToken: string;
}

export interface BubbleStatusContract {
  text: string;
  state: BubbleStatusState;
  icons: Record<Exclude<BubbleStatusState, "none">, BubbleStatusIconContract>;
}

export interface BubbleDesignContract {
  id: string;
  state: BubbleState;
  surface: SurfaceDesignContract;
  textBox: TextBoxDesignContract;
  mediaSlot: BubbleMediaSlotContract;
  actorLabelSlot: BubbleActorLabelSlotContract;
  avatarSlot: BubbleAvatarSlotContract;
  status: BubbleStatusContract;
  colors: Record<BubbleState, BubbleStateColorContract>;
  maxWidth: number;
  padding: SpacingPairContract;
}
