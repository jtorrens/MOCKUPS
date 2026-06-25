import { AvatarModule } from "./atomic/AvatarModule.js";
import { ChatHeaderModule } from "./atomic/ChatHeaderModule.js";
import { MessageBubbleModule } from "./atomic/MessageBubbleModule.js";
import { StatusBarModule } from "./atomic/StatusBarModule.js";
import { NavigationBarModule } from "./atomic/NavigationBarModule.js";
import { ChatScreenModule } from "./screens/ChatScreenModule.js";

export const visualModuleRegistry = {
  chat_screen: ChatScreenModule,
  message_bubble: MessageBubbleModule,
  status_bar: StatusBarModule,
  navigation_bar: NavigationBarModule,
  chat_header: ChatHeaderModule,
  avatar: AvatarModule,
} as const;

export type VisualModuleName = keyof typeof visualModuleRegistry;

export function getVisualModule<Name extends VisualModuleName>(
  name: Name,
): (typeof visualModuleRegistry)[Name] {
  return visualModuleRegistry[name];
}
