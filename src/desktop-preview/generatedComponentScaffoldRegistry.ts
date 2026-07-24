// Generated from scaffolding/components/*.json. Do not edit manually.
import type { ComponentRenderableFactory } from "./componentClassRenderableRegistry.js";
import { listComponentToRenderable } from "./listComponentRenderable.js";
import { resolveListComponent } from "./listComponentResolver.js";
import { listItemComponentToRenderable } from "./listItemComponentRenderable.js";
import { resolveListItemComponent } from "./listItemComponentResolver.js";

export const generatedComponentScaffoldFactories = {
  list: (payload, assignedBox, renderChild) =>
    listComponentToRenderable(payload, resolveListComponent(payload), assignedBox, renderChild),
  listItem: (payload, assignedBox) =>
    listItemComponentToRenderable(payload, resolveListItemComponent(payload), assignedBox),
} satisfies Record<string, ComponentRenderableFactory>;
