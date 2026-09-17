export interface SystemCompositionModuleContract {
  id: "systemComposition";
  statusBarSlot: SystemCompositionComponentSlot;
  navigationBarSlot: SystemCompositionComponentSlot;
  stackSlot: {
    variantReference: string;
    overrides: Record<string, unknown>;
  };
  stackInputs: Record<string, unknown>;
  showStatusBar: boolean;
  showNavigationBar: boolean;
}

export interface SystemCompositionComponentSlot {
  variantReference: string;
  overrides: Record<string, unknown>;
}
