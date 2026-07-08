import type { ButtonIconDesignContract } from "./buttonIconComponentContract.js";

export interface IconRowHighlightContract {
  index: number;
  backgroundAlpha?: number;
  backgroundPaletteColor?: string;
  iconPaletteColor?: string;
}

export interface IconRowDesignContract {
  id: string;
  orientation: "horizontal" | "vertical";
  gapToken: string;
  sizeToken: string;
  icons: string[];
  highlight?: IconRowHighlightContract;
  buttons: ButtonIconDesignContract[];
}
