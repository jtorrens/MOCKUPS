import type { CollectionStackDesignContract } from "./collectionStackComponentContract.js";
import type { ComponentCollectionChildRenderer } from "./componentCollectionContract.js";

export interface NotificationsDesignContract {
  id: "component.notifications";
  stack: CollectionStackDesignContract;
}

export type NotificationsChildRenderer = ComponentCollectionChildRenderer;
