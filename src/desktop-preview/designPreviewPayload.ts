export interface PreviewFramePayload {
  canvasWidth: number;
  canvasHeight: number;
  screenX: number;
  screenY: number;
  screenWidth: number;
  screenHeight: number;
  scaleToPixels?: number;
}

export interface DesignPreviewFontFacePayload {
  fontId: string;
  family: string;
  category: string;
  relativePath: string;
  weight: number;
  style: string;
}

export interface DesignPreviewPayload {
  kind: "componentClass" | "module" | "moduleInstance";
  componentType?: string;
  componentBaseConfigsJson?: string;
  appConfigJson?: string;
  instanceJson?: string;
  frameRate: number;
  localFrame: number;
  configJson: string;
  designPreviewJson?: string;
  runtimeContractJson?: string;
  previewFrame: PreviewFramePayload;
  rootPreviewFrame?: PreviewFramePayload;
  iconAssetRoot?: string;
  iconMappingJson?: string;
  fontFaces?: DesignPreviewFontFacePayload[];
  paletteColors?: Record<string, string>;
  paletteNeutralColors?: Record<string, boolean>;
  projectMediaRoot?: string;
  showMarks?: boolean;
  themeMode: string;
  themeTokensJson: string;
  themeStatusBarPresetId?: string;
  themeNavigationBarPresetId?: string;
}
