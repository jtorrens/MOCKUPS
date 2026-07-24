export interface ChatListComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export interface ChatListModuleContract {
  id: "module.core.chatList";
  wallpaperEnabled: boolean;
  stackSlot: ChatListComponentSlot;
  topIconBarSlot: ChatListComponentSlot;
  bottomIconBarSlot: ChatListComponentSlot;
  listSlot: ChatListComponentSlot;
  statusBarSlot: ChatListComponentSlot;
  navigationBarSlot: ChatListComponentSlot;
  topIconBarInputs: Record<string, unknown>;
  bottomIconBarInputs: Record<string, unknown>;
  listInputs: Record<string, unknown>;
}
