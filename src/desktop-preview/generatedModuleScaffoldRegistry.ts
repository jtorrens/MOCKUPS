// Generated from scaffolding/modules/*.json. Do not edit manually.
import type { ModuleRenderableFactory } from "./moduleRenderableRegistry.js";
import { conversationModuleToRenderable } from "./conversationModuleRenderable.js";
import { chatListModuleToRenderable } from "./chatListModuleRenderable.js";
import { socialPostModuleToRenderable } from "./socialPostModuleRenderable.js";
import { videoCallModuleToRenderable } from "./videoCallModuleRenderable.js";
import { systemCompositionModuleToRenderable } from "./systemCompositionModuleRenderable.js";

export const generatedModuleScaffoldFactories = {
  "module.core.chat": conversationModuleToRenderable,
  "module.core.chatList": chatListModuleToRenderable,
  "module.core.socialPost": socialPostModuleToRenderable,
  "module.core.videoCall": videoCallModuleToRenderable,
  "module.system.composition": systemCompositionModuleToRenderable,
} satisfies Record<string, ModuleRenderableFactory>;
