import type { CollectionStackDesignContract } from "./collectionStackComponentContract.js";
import type { ComponentCollectionChildRenderer } from "./componentCollectionContract.js";

export interface ListDesignContract {
  id: "component.list";
  stack: CollectionStackDesignContract;
}

export type ListChildRenderer = ComponentCollectionChildRenderer;
