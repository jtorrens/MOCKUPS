export interface SocialPostComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export type SocialPostHeaderSlotKind = "none" | "avatar" | "icon" | "label";
export type SocialPostVerticalAlignment = "top" | "center" | "bottom";

export interface SocialPostHeaderSlot {
  index: number;
  kind: SocialPostHeaderSlotKind;
  componentType?: "avatar" | "button" | "label";
  componentSlot?: SocialPostComponentSlot;
  inputs: Record<string, unknown>;
}

export interface SocialPostHeaderRow {
  id: "row1" | "row2";
  padding: string;
  verticalAlignment: SocialPostVerticalAlignment;
  showSeparator: boolean;
  slots: SocialPostHeaderSlot[];
}

export interface SocialPostModuleContract {
  id: "module.core.socialPost";
  useAppWallpaper: boolean;
  showHeader: boolean;
  headerHeight: number;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  headerSurfaceSlot: SocialPostComponentSlot;
  rowGapToken: string;
  rows: [SocialPostHeaderRow, SocialPostHeaderRow];
  mediaSlot: SocialPostComponentSlot;
  mediaPadding: string;
  mediaInputs: Record<string, unknown>;
  mediaSource: string;
  mediaHeight: number;
  showMediaSeparator: boolean;
}
