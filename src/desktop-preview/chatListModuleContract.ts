export type ChatListHorizontalAlignment = "left" | "center" | "right";

export interface ChatListComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export interface ChatListModuleContract {
  id: "module.core.chatList";
  listSlot: ChatListComponentSlot;
  listInputs: Record<string, unknown>;
  horizontalAlignment: ChatListHorizontalAlignment;
  topInsetToken: string;
}
