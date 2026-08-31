export interface SocialPostComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export interface SocialPostModuleContract {
  id: "module.core.socialPost";
  wallpaperEnabled: boolean;
  showStatusBar: boolean;
  showNavigationBar: boolean;
  showTextInputBar: boolean;
  showKeyboard: boolean;
  stackSlot: SocialPostComponentSlot;
  headerStackSlot: SocialPostComponentSlot;
  mediaSlot: SocialPostComponentSlot;
  bubbleSlot: SocialPostComponentSlot;
  footerIconBarSlot: SocialPostComponentSlot;
  textInputBarSlot: SocialPostComponentSlot;
  keyboardSlot: SocialPostComponentSlot;
  statusBarSlot: SocialPostComponentSlot;
  navigationBarSlot: SocialPostComponentSlot;
  headerStackInputs: Record<string, unknown>;
  mediaInputs: Record<string, unknown>;
  bubbleInputs: Record<string, unknown>;
  footerIconBarInputs: Record<string, unknown>;
  textInputBarInputs: Record<string, unknown>;
  keyboardInputs: Record<string, unknown>;
}
