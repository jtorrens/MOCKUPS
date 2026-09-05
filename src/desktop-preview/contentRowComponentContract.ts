import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { ButtonDesignContract } from "./buttonComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";

export type ContentRowSlotKind = "none" | "avatar" | "icon" | "label";
export type ContentRowVerticalAlignment = "top" | "center" | "bottom";

export interface ContentRowSlotContract {
  index: number;
  kind: ContentRowSlotKind;
  content?: AvatarDesignContract | ButtonDesignContract | LabelDesignContract;
}

export interface ContentRowDesignContract {
  id: "component.contentRow";
  size: { width: number; height: number };
  padding: { xToken: string; yToken: string };
  verticalAlignment: ContentRowVerticalAlignment;
  showSeparator: boolean;
  slots: [
    ContentRowSlotContract,
    ContentRowSlotContract,
    ContentRowSlotContract,
    ContentRowSlotContract,
    ContentRowSlotContract,
  ];
}
