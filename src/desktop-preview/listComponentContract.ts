import type { CollectionStackDesignContract } from "./collectionStackComponentContract.js";
import type { ComponentCollectionChildRenderer } from "./componentCollectionContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface ListDesignContract {
  id: "component.list";
  stack: CollectionStackDesignContract;
  surface: SurfaceDesignContract;
  itemsPlacement: "top" | "center" | "bottom";
  overflowMode: "clip";
}

export type ListChildRenderer = ComponentCollectionChildRenderer;
