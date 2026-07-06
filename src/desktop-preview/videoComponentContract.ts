import type {
  IconSlotsContract,
} from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface VideoDesignContract {
  id: string;
  durationText: string;
  statusVisible: boolean;
  statusHeight: number;
  statusIconSlots: IconSlotsContract;
  statusTextColorToken: string;
  playOverlayVisible: boolean;
  playColorToken: string;
  surface: SurfaceDesignContract;
}
