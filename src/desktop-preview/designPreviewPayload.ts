export interface DevicePayload {
  canvasWidth: number;
  canvasHeight: number;
  screenX: number;
  screenY: number;
  screenWidth: number;
  screenHeight: number;
  statusBarHeight?: number;
  safeAreaBottom?: number;
  scaleToPixels?: number;
}

export interface DesignPreviewPayload {
  kind: "statusBar" | "navigationBar" | "componentClass";
  componentType?: string;
  configJson: string;
  designPreviewJson?: string;
  device: DevicePayload;
  iconAssetRoot?: string;
  iconMappingJson?: string;
  paletteColors?: Record<string, string>;
  paletteNeutralColors?: Record<string, boolean>;
  projectMediaRoot?: string;
  showMarks?: boolean;
  themeMode: string;
  themeTokensJson: string;
}
