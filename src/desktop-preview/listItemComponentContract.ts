import type { RenderableBox } from "../visual/renderable/types.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { AlignmentPlacementContract } from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type ListItemState = "normal" | "pressed" | "inactive";

interface ListItemElementBase {
  id: string;
  placement: AlignmentPlacementContract;
  size: { width: number; height: number };
}

export interface ListItemAvatarElement extends ListItemElementBase {
  componentType: "avatar";
  component: AvatarDesignContract;
}

export interface ListItemLabelElement extends ListItemElementBase {
  componentType: "label";
  component: LabelDesignContract;
  textColorToken: string;
  subtextColorToken: string;
}

export interface ListItemIconRowElement extends ListItemElementBase {
  componentType: "iconRow";
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
  selectedSetId: string;
  surface: SurfaceDesignContract;
  elementsOpacity: number;
  elements: ListItemElement[];
}

export type ListItemAssignedBox = RenderableBox;
