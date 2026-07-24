// Generated from scaffolding/modules/*.json. Do not edit manually.
import type { ModuleRenderableFactory } from "./moduleRenderableRegistry.js";
import { chatListModuleToRenderable } from "./chatListModuleRenderable.js";

export const generatedModuleScaffoldFactories = {
  "module.core.chatList": chatListModuleToRenderable,
} satisfies Record<string, ModuleRenderableFactory>;
