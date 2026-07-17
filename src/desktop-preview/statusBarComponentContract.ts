export type StatusBarZone = "left" | "right";
export type StatusBarItemZone = StatusBarZone | "off";

interface StatusBarItemBaseContract {
  id: string;
  label: string;
  zone: StatusBarItemZone;
  order: number;
}

export type StatusBarItemContract =
  | StatusBarItemBaseContract & {
      kind: "text";
      value: string;
    }
  | StatusBarItemBaseContract & {
      kind: "iconToken";
      token: string;
    }
  | StatusBarItemBaseContract & {
      kind: "generatedSignal";
      value: number;
    }
  | StatusBarItemBaseContract & {
      kind: "generatedBattery";
      value: number;
      charging: boolean;
    };

export interface StatusBarDesignContract {
  id: "statusBar";
  fontFamilyId: "theme.system";
  foregroundColorToken: string;
  backgroundColorToken: string;
  backgroundAlpha: number;
  layout: {
    height: number;
    itemSize: number;
    gapToken: string;
    sidePaddingToken: string;
  };
  zones: Record<StatusBarZone, StatusBarItemContract[]>;
}
