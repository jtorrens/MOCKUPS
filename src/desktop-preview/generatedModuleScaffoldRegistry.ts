// Generated from scaffolding/modules/*.json. Do not edit manually.
import type { ModuleRenderableFactory } from "./moduleRenderableRegistry.js";
import { conversationModuleToRenderable } from "./conversationModuleRenderable.js";
import { chatListModuleToRenderable } from "./chatListModuleRenderable.js";
import { lockScreenModuleToRenderable } from "./lockScreenModuleRenderable.js";

export const generatedModuleScaffoldFactories = {
  "module.core.chat": conversationModuleToRenderable,
  "module.core.chatList": chatListModuleToRenderable,
  "module.core.lockScreen": lockScreenModuleToRenderable,
} satisfies Record<string, ModuleRenderableFactory>;
