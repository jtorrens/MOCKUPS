import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";

export interface NotificationDesignContract {
  id: "component.notification";
  avatarPosition: "start" | "end";
  gapToken: string;
  avatar: AvatarDesignContract;
  label: LabelDesignContract;
}
