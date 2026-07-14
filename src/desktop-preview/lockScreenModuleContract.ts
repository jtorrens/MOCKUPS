export interface LockScreenModuleContract {
  id: "lockScreen";
  statusBarSlot: LockScreenComponentSlot;
  navigationBarSlot: LockScreenComponentSlot;
  stackSlot: {
    presetId: string;
    overrides: Record<string, unknown>;
  };
  stackInputs: Record<string, unknown>;
  showStatusBar: boolean;
  showNavigationBar: boolean;
}

export interface LockScreenComponentSlot {
  presetId: string;
  overrides: Record<string, unknown>;
}
