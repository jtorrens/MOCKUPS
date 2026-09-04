export interface ModuleRowComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export type ModuleRowSlotKind = "none" | "avatar" | "icon" | "label";
export type ModuleRowVerticalAlignment = "top" | "center" | "bottom";

export interface ModuleRowSlot {
  index: number;
  kind: ModuleRowSlotKind;
  componentType?: "avatar" | "button" | "label";
  componentSlot?: ModuleRowComponentSlot;
  inputs: Record<string, unknown>;
}

export interface ModuleRow {
  id: "row1" | "row2";
  padding: string;
  verticalAlignment: ModuleRowVerticalAlignment;
  showSeparator: boolean;
  slots: ModuleRowSlot[];
}
