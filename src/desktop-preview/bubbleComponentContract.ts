import type {
  AlignmentPlacementContract,
  SpacingPairContract,
} from "./previewComponentContracts.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";

export type BubbleState = "incoming" | "system" | "outgoing";

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
}

export interface BubbleDesignContract {
  id: string;
  state: BubbleState;
  surface: SurfaceDesignContract;
  textBox: TextBoxDesignContract;
  actorLabelSlot: BubbleActorLabelSlotContract;
  colors: Record<BubbleState, BubbleStateColorContract>;
  maxWidth: number;
  padding: SpacingPairContract;
}
