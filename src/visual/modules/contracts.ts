import type { z } from "zod";
import type {
  JsonObject,
  ThemeMode,
} from "../../domain/schemas/index.js";
import type { RenderableNode } from "../renderable/types.js";

export interface ResolvedModuleActor {
  id: string;
  displayName: string;
  avatarUri?: string;
}

export interface ResolvedModuleDevice {
  id: string;
  osFamily: string;
  designSpace: { width: number; height: number };
  renderSize: { width: number; height: number };
  scaleToPixels: number;
}

export interface ResolvedModuleDeviceState {
  id: string;
  values: JsonObject;
}

export interface ResolvedModuleAsset {
  id: string;
  uri: string;
  mimeType: string;
}

export interface ResolvedModuleIcon {
  token: string;
  uri?: string;
  glyph?: string;
}

export type ResolvedAssetMap = Record<string, ResolvedModuleAsset>;
export type ResolvedIconMap = Record<string, ResolvedModuleIcon>;
export type ResolvedThemeTokens = JsonObject;

export interface ScreenModuleInput<TData, TConfig> {
  frame: number;
  fps: number;
  screenInstanceId: string;
  moduleId: string;
  moduleSchemaVersion: number;
  moduleData: TData;
  moduleConfig: TConfig;
  ownerActor?: ResolvedModuleActor;
  device: ResolvedModuleDevice;
  deviceState?: ResolvedModuleDeviceState;
  themeTokens: ResolvedThemeTokens;
  themeMode: ThemeMode;
  assets: ResolvedAssetMap;
  icons: ResolvedIconMap;
  props?: JsonObject;
}

export type ScreenModuleOutput = RenderableNode;

export interface ScreenModuleDefinition<TData, TConfig> {
  moduleId: string;
  moduleSchemaVersion: number;
  dataSchema: z.ZodType<TData>;
  configSchema: z.ZodType<TConfig>;
  render(input: ScreenModuleInput<TData, TConfig>): ScreenModuleOutput;
}
