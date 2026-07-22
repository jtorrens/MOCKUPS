export interface LockScreenModuleContract {
  id: "lockScreen";
  statusBarSlot: LockScreenComponentSlot;
  navigationBarSlot: LockScreenComponentSlot;
  stackSlot: {
    variantReference: string;
    overrides: Record<string, unknown>;
  };
  stackInputs: Record<string, unknown>;
  showStatusBar: boolean;
  showNavigationBar: boolean;
}

export interface LockScreenComponentSlot {
  variantReference: string;
  overrides: Record<string, unknown>;
}
