export interface ModuleRowComponentSlot extends Record<string, unknown> {
  variantReference: string;
  overrides: Record<string, unknown>;
}

export interface ModuleRow<TContent> {
  id: string;
  content: TContent;
}
