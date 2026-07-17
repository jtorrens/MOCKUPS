export type NavigationBarZone = "left" | "center" | "right";
export type NavigationBarItemZone = NavigationBarZone | "off";

export interface NavigationBarItemContract {
  id: string;
  label: string;
  kind: "generatedBack" | "generatedHome" | "generatedRecents";
  zone: NavigationBarItemZone;
  order: number;
}

export interface NavigationBarDesignContract {
  id: "navigationBar";
  type: "buttons" | "gestureBar";
  foregroundColorToken: string;
  backgroundColorToken: string;
  backgroundAlpha: number;
  layout: {
    height: number;
    itemSize: number;
    sidePaddingToken: string;
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
