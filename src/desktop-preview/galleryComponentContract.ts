import type { MediaDesignContract } from "./mediaComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type GalleryMode = "carousel" | "gallery";
export type GallerySizeMode = "fixed" | "fill";

export interface GalleryItemContract {
  id: string;
  sourceUri: string;
  selected: boolean;
  media: MediaDesignContract;
}

export interface GalleryDesignContract {
  id: "gallery";
  mode: GalleryMode;
  widthMode: GallerySizeMode;
  heightMode: GallerySizeMode;
  width: number;
  height: number;
  itemWidth: number;
  itemHeight: number;
  gapToken: string;
  containerPaddingXToken: string;
  containerPaddingYToken: string;
  paddingXToken: string;
  paddingYToken: string;
  fadeExtent: number;
  selectedScale: number;
  selectedIndex: number;
  scrollRow: number;
  surface: SurfaceDesignContract;
  items: GalleryItemContract[];
}
