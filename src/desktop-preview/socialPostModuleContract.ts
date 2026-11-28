import type { ModuleSectionLayout } from "./moduleSectionLayout.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import type {
  ModuleRow,
  ModuleRowComponentSlot,
} from "./moduleRowSectionContract.js";
import type { ContentRowDesignContract } from "./contentRowComponentContract.js";

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
  headerLayout: ModuleSectionLayout;
  footerLayout: ModuleSectionLayout;
  messageLayout: ModuleSectionLayout;
  galleryLayout: ModuleSectionLayout;
  actionsLayout: ModuleSectionLayout;
  textInputLayout: ModuleSectionLayout;
  keyboardLayout: ModuleSectionLayout;
  showFooter: boolean;
  showActions: boolean;
  showReactionEmitter: boolean;
  reactionEmitterSlot: SocialPostComponentSlot;
  reactionCatalog: Record<string, unknown>[];
  actions: IconRowDesignContract;
  galleryFloatHeight: number;
  useAppWallpaper: boolean;
  showHeader: boolean;
  headerHeight: number;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  headerSurfaceSlot: SocialPostComponentSlot;
  rowGapToken: string;
  rows: ModuleRow<ContentRowDesignContract>[];
  showMedia: boolean;
  mediaSlot: SocialPostComponentSlot;
  mediaPadding: string;
  mediaInputs: Record<string, unknown>;
  mediaSource: string;
  mediaHeightMode: "fixed" | "fill" | "screen";
  mediaHeight: number;
  mediaCutout: boolean;
  galleryMediaCutout: boolean;
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
  footerRows: ModuleRow<ContentRowDesignContract>[];
}
