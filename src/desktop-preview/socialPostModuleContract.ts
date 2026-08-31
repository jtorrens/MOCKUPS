export interface SocialPostComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export interface SocialPostModuleContract {
  id: "module.core.socialPost";
  useAppWallpaper: boolean;
  screenGutter: string;
  zoneGap: string;
  showHeader: boolean;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  showTextInputBar: boolean;
  showKeyboard: boolean;
  stackSlot: SocialPostComponentSlot;
  headerStackSlot: SocialPostComponentSlot;
  headerPrimarySlot: SocialPostComponentSlot;
  headerSecondaryIconRowSlot: SocialPostComponentSlot;
  mediaSlot: SocialPostComponentSlot;
  bubbleSlot: SocialPostComponentSlot;
  footerIconBarSlot: SocialPostComponentSlot;
  textInputBarSlot: SocialPostComponentSlot;
  keyboardSlot: SocialPostComponentSlot;
  headerStackInputs: Record<string, unknown>;
  mediaInputs: Record<string, unknown>;
  bubbleInputs: Record<string, unknown>;
  footerIconBarInputs: Record<string, unknown>;
  textInputBarInputs: Record<string, unknown>;
  keyboardInputs: Record<string, unknown>;
}
