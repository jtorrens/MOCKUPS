import type {
  IconSlotsContract,
  SurfaceStyleContract,
} from "./previewComponentContracts.js";

export interface VideoDesignContract {
  id: string;
  durationText: string;
  backgroundColorToken: string;
  backgroundAlpha: number;
  statusVisible: boolean;
  statusHeight: number;
  statusIconSlots: IconSlotsContract;
  statusTextColorToken: string;
  playOverlayVisible: boolean;
  playColorToken: string;
  surface: SurfaceStyleContract;
}
