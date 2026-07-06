export interface PreviewFramePayload {
  canvasWidth: number;
  canvasHeight: number;
  screenX: number;
  screenY: number;
  screenWidth: number;
  screenHeight: number;
  scaleToPixels?: number;
}

export interface DesignPreviewPayload {
  kind: "componentClass";
  componentType?: string;
  componentBaseConfigsJson?: string;
  configJson: string;
  designPreviewJson?: string;
  previewFrame: PreviewFramePayload;
  iconAssetRoot?: string;
  iconMappingJson?: string;
  paletteColors?: Record<string, string>;
  paletteNeutralColors?: Record<string, boolean>;
  projectMediaRoot?: string;
  showMarks?: boolean;
  themeMode: string;
  themeTokensJson: string;
}
