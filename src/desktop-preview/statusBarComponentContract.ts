export type StatusBarZone = "left" | "right";

export interface StatusBarItemContract {
  id: string;
  label: string;
  kind: string;
  value: string | number | boolean;
  token: string;
  zone: StatusBarZone;
  order: number;
  charging: boolean;
}

export interface StatusBarDesignContract {
  id: "statusBar";
  layout: {
    height: number;
    itemSize: number;
    gap: number;
    sidePadding: number;
  };
  zones: Record<StatusBarZone, StatusBarItemContract[]>;
}
