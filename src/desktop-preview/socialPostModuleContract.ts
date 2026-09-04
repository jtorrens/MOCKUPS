import type {
  ModuleRow,
  ModuleRowComponentSlot,
} from "./moduleRowSectionContract.js";

export type SocialPostComponentSlot = ModuleRowComponentSlot;

export interface SocialPostMessageContract {
  show: boolean;
  bubbleSlot: SocialPostComponentSlot;
  textInputBarSlot: SocialPostComponentSlot;
  keyboardSlot: SocialPostComponentSlot;
  padding: string;
  bubbleInputs: Record<string, unknown>;
  text: string;
  visibleText: string;
  writeOnDurationFrames: number;
  writeOnFrame: number;
  writeOnTrigger: boolean;
  currentCharacter: number;
  textInputVisible: boolean;
  keyboardVisible: boolean;
  bubbleVisible: boolean;
  bubbleWriteOnTrigger: boolean;
  showSeparator: boolean;
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
  rows: [ModuleRow, ModuleRow];
  showMedia: boolean;
  mediaSlot: SocialPostComponentSlot;
  mediaPadding: string;
  mediaInputs: Record<string, unknown>;
  mediaSources: string[];
  mediaHeightMode: "fixed" | "fill";
  mediaHeight: number;
  mediaScale: number;
  mediaOffset: string;
  mediaIsPlaying: boolean;
  mediaCurrentTimeSeconds: number;
  mediaDurationSeconds: number;
  mediaIsFullScreen: boolean;
  mediaFullScreenTransition: boolean;
  mediaMotionElapsedMs: number;
  showMediaSeparator: boolean;
  showGallery: boolean;
  gallerySlot: SocialPostComponentSlot;
  showGallerySeparator: boolean;
  galleryMode: "carousel" | "gallery";
  galleryDirectory: string;
  gallerySelectedIndex: number;
  galleryScrollRow: number;
  messageMinHeight: number;
  message: SocialPostMessageContract;
  footerHeight: number;
  footerSurfaceSlot: SocialPostComponentSlot;
  footerRowGapToken: string;
  footerRows: [ModuleRow, ModuleRow];
}
