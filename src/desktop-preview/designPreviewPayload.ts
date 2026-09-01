export interface PreviewFramePayload {
  canvasWidth: number;
  canvasHeight: number;
  screenX: number;
  screenY: number;
  screenWidth: number;
  screenHeight: number;
  moduleTransparency: DeviceModuleTransparencyPayload;
}

export interface DeviceModuleTransparencyPayload {
  enabled: boolean;
  mode: "fixed" | "variable";
  paletteColor: string;
  backgroundOpacity: number;
  fixedStart: number;
  minimumOpaqueExtent: number;
  gradientHeight: number;
  variableOffset: number;
}

export interface DesignPreviewFontFacePayload {
  fontId: string;
  family: string;
  category: string;
  relativePath: string;
  weight: number;
  style: string;
}

export interface ScreenTransitionPayload {
  outgoing: DesignPreviewPayload;
  incoming: DesignPreviewPayload;
  outgoingMotionJson: string;
  incomingMotionJson: string;
  elapsedMilliseconds: number;
  durationFrames: number;
}

export interface ScreenTimingPayload {
  screenFrame: number;
  transitionFrameCount: number;
  actionDelayFrames: number;
  actionDurationFrames: number;
  actionStartFrame: number;
}

export interface DesignPreviewPayload {
  kind: "componentClass" | "module" | "moduleInstance" | "screenTransition";
  authoringOwnerId?: string;
  authoringFocusFieldId?: string;
  authoringFocusItemId?: string;
  authoringRecordClassId?: string;
  authoringSlotFieldIds?: string[];
  componentType: string;
  componentBaseConfigsJson: string;
  appConfigJson: string;
  instanceJson: string;
  frameRate: number;
  localFrame: number;
  configJson: string;
  designPreviewJson: string;
  runtimeContractJson: string;
  runtimeRecordReferencesJson?: string;
  previewFrame: PreviewFramePayload;
  rootPreviewFrame?: PreviewFramePayload;
  iconAssetRoot?: string;
  iconMappingJson?: string;
  fontFaces?: DesignPreviewFontFacePayload[];
  paletteColors?: Record<string, string>;
  paletteNeutralColors?: Record<string, boolean>;
  projectMediaRoot?: string;
  projectMediaFiles?: string[];
  showMarks?: boolean;
  themeMode: string;
  themeTokensJson: string;
  themeStatusBarVariantReference?: string;
  themeNavigationBarVariantReference?: string;
  screenTiming?: ScreenTimingPayload;
  screenTransition?: ScreenTransitionPayload;
}
