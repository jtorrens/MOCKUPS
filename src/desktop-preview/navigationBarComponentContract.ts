export type NavigationBarZone = "left" | "center" | "right";
export type NavigationBarItemZone = NavigationBarZone | "off";

export interface NavigationBarItemContract {
  id: string;
  label: string;
  kind: string;
  value: string | number | boolean;
  token: string;
  zone: NavigationBarItemZone;
  order: number;
  charging: boolean;
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
  zones: Record<NavigationBarZone, NavigationBarItemContract[]>;
}
