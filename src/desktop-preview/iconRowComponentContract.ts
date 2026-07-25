import type { ButtonDesignContract } from "./buttonComponentContract.js";

export interface IconRowItemContract {
  id: string;
  button: ButtonDesignContract;
}

export interface IconRowDesignContract {
  id: string;
  orientation: "horizontal" | "vertical";
  itemSizingMode: "content" | "fillParent";
  gapToken: string;
  items: IconRowItemContract[];
}
