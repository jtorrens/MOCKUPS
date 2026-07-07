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
  size: number;
  icons: string[];
  highlight?: IconRowHighlightContract;
  buttons: ButtonIconDesignContract[];
}
