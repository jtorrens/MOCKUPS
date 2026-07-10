export type StatusBarZone = "left" | "right";
export type StatusBarItemZone = StatusBarZone | "off";

export interface StatusBarItemContract {
  id: string;
  label: string;
  kind: string;
  value: string | number | boolean;
  token: string;
  zone: StatusBarItemZone;
  order: number;
  charging: boolean;
}

export interface StatusBarDesignContract {
  id: "statusBar";
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
