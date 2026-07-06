export interface SystemBarItemContract {
  id: string;
  label: string;
  kind: string;
  value: string | number | boolean;
  token: string;
  zone: string;
  order: number;
  charging: boolean;
}

export type StatusBarZone = "left" | "right";
export type NavigationBarZone = "left" | "center" | "right";

export interface StatusBarDesignContract {
  id: "statusBar";
  layout: {
    height: number;
    itemSize: number;
    gap: number;
    sidePadding: number;
  };
  zones: Record<StatusBarZone, SystemBarItemContract[]>;
}

export interface NavigationBarDesignContract {
  id: "navigationBar";
  type: "buttons" | "gestureBar";
  layout: {
    height: number;
    itemSize: number;
    sidePadding: number;
    strokeWidth: number;
    cornerRadius: number;
    filled: boolean;
  };
  gesture: {
    width: number;
    height: number;
    cornerRadius: number;
  };
  zones: Record<NavigationBarZone, SystemBarItemContract[]>;
}
