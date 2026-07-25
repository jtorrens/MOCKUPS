import type { CollectionStackDesignContract } from "./collectionStackComponentContract.js";
import type { ComponentCollectionChildRenderer } from "./componentCollectionContract.js";
import type { BadgeDesignContract } from "./badgeComponentContract.js";
import type {
  ComponentMotionContract,
  ComponentMotionFrameContract,
} from "./previewComponentContracts.js";
import type { CollectionStackDistributionMode } from "./collectionStackComponentContract.js";

export interface NotificationsDesignContract {
  id: "component.notifications";
  stack: CollectionStackDesignContract;
  closedItemLimit: number;
  distributionMotion: ComponentMotionContract;
  distributionTransition?: {
    fromMode: CollectionStackDistributionMode;
    motionFrame: ComponentMotionFrameContract;
    fromStack: CollectionStackDesignContract;
  };
  badge?: BadgeDesignContract;
}

export type NotificationsChildRenderer = ComponentCollectionChildRenderer;
