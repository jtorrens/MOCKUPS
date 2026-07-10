import type { IconRowDesignContract } from "./iconRowComponentContract.js";

export type IconBarState = "idle" | "active";
export type IconBarZone = "left" | "center" | "right";

export interface IconBarDesignContract {
  id: string;
  state: IconBarState;
  size: {
    width: number;
    height: number;
  };
  edgePaddingToken: string;
  rows: Record<IconBarZone, IconRowDesignContract>;
}
