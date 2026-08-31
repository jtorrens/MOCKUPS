// Generated from scaffolding/modules/*.json. Do not edit manually.
import type { ModuleRenderableFactory } from "./moduleRenderableRegistry.js";
import { conversationModuleToRenderable } from "./conversationModuleRenderable.js";
import { chatListModuleToRenderable } from "./chatListModuleRenderable.js";
import { lockScreenModuleToRenderable } from "./lockScreenModuleRenderable.js";
import { socialPostModuleToRenderable } from "./socialPostModuleRenderable.js";

export const generatedModuleScaffoldFactories = {
  "module.core.chat": conversationModuleToRenderable,
  "module.core.chatList": chatListModuleToRenderable,
  "module.core.lockScreen": lockScreenModuleToRenderable,
  "module.core.socialPost": socialPostModuleToRenderable,
} satisfies Record<string, ModuleRenderableFactory>;
