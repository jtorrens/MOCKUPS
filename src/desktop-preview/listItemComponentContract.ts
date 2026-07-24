import type { RenderableBox } from "../visual/renderable/types.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type ListItemState = "normal" | "pressed" | "inactive";
export type ListItemVerticalAlignment = "start" | "center" | "end";

interface ListItemElementBase {
  id: string;
  verticalAlignment: ListItemVerticalAlignment;
}

export interface ListItemAvatarElement extends ListItemElementBase {
  componentType: "avatar";
  sizeMode: "auto" | "fixed";
  fixedSize: number;
  component: AvatarDesignContract;
}

export interface ListItemLabelElement extends ListItemElementBase {
  componentType: "label";
  sizeMode: "fill" | "fixed";
  fixedSize: { width: number; height: number };
  component: LabelDesignContract;
}

export interface ListItemIconRowElement extends ListItemElementBase {
  componentType: "iconRow";
  sizeMode: "content" | "fixed";
  fixedSize: { width: number; height: number };
  component: IconRowDesignContract;
}

export type ListItemElement =
  | ListItemAvatarElement
  | ListItemLabelElement
  | ListItemIconRowElement;

export interface ListItemDesignContract {
  id: "component.listItem";
  size: { width: number; height: number };
  state: ListItemState;
  activeSet: number;
  padding: { xToken: string; yToken: string };
  gapToken: string;
  surface: SurfaceDesignContract;
  elementsOpacity: number;
  elements: ListItemElement[];
}

export type ListItemAssignedBox = RenderableBox;
