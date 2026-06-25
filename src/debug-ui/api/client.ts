import type { RenderableNode } from "../../visual/renderable/types.js";

export type FieldKind = "string" | "number" | "json";

export interface AppFieldDefinition {
  column: string;
  label: string;
  kind: FieldKind;
  nullable?: boolean;
  readonly?: boolean;
}

export interface AppTableDefinition {
  id: string;
  label: string;
  table: string;
  titleColumn: string;
  fields: AppFieldDefinition[];
  jsonFields: string[];
}

export type AppRecord = Record<string, unknown> & { id: string };

export interface DebugOptions {
  productions: {
    id: string;
    name: string;
    slug?: string | null;
    defaultFps?: number | null;
  }[];
  episodes: {
    id: string;
    productionId: string;
    name: string;
    slug?: string | null;
    sortOrder?: number;
  }[];
  shots: {
    id: string;
    productionId: string;
    episodeId?: string | null;
    ownerActorId?: string | null;
    name: string;
    slug?: string | null;
    version?: number | null;
    durationFrames: number;
    fps: number;
  }[];
  screenInstances: {
    id: string;
    shotId: string;
    appId?: string;
    screenType: string;
    moduleId?: string;
    startFrame: number;
    endFrame: number;
    layerOrder: number;
  }[];
}

export interface AppState {
  tables: AppTableDefinition[];
  records: Record<string, AppRecord[]>;
  options: DebugOptions;
  inheritedJson: Record<
    string,
    Record<string, Record<string, Record<string, unknown>>>
  >;
}

export interface DebugSelection {
  productionId: string;
  shotId: string;
  screenInstanceId: string;
  frame: number;
}

export interface DebugPayload {
  selection: DebugSelection;
  previewContext?: {
    deviceId: string;
    deviceName: string;
    themeId: string;
    themeName: string;
    themeMode: "light" | "dark";
    ownerActorId?: string | null;
    ownerActorName?: string | null;
  };
  screenInstance: {
    id: string;
    screenType: string;
    moduleId?: string;
    moduleSchemaVersion?: number;
    startFrame: number;
    endFrame: number;
  };
  editable: Record<string, Record<string, unknown>>;
  resolvedScreen: unknown;
  renderable: RenderableNode | null;
  warnings: string[];
}

export interface RenderFrameResult {
  url: string;
  filePath: string;
  includeFrame: boolean;
  outputHeight: number;
  outputScale: number;
  outputWidth: number;
  relativeFilePath?: string;
  selection: DebugSelection;
}

export interface RenderFrameRequest extends DebugSelection {
  includeFrame?: boolean;
}

export interface AppCreateRequest {
  tableId:
    | "productions"
    | "episodes"
    | "shots"
    | "icon_themes"
    | "status_bars"
    | "navigation_bars"
    | "themes"
    | "devices"
    | "render_presets";
  parent?: {
    productionId?: string;
    episodeId?: string;
  };
  name?: string;
  family?: "ios" | "android";
}

export interface AppRecordActionRequest {
  tableId:
    | "shots"
    | "icon_themes"
    | "status_bars"
    | "navigation_bars"
    | "themes"
    | "devices"
    | "render_presets";
  recordId: string;
}

async function readResponse<T>(response: Response): Promise<T> {
  const value = (await response.json()) as T & { error?: string };
  if (!response.ok) {
    throw new Error(value.error ?? `Request failed (${response.status})`);
  }
  return value;
}

export async function getAppState(): Promise<AppState> {
  return readResponse(await fetch("/api/app"));
}

export async function updateAppRecord(
  tableId: string,
  recordId: string,
  patch: Record<string, unknown>,
): Promise<{ records: AppRecord[]; record: AppRecord; saved?: AppRecord }> {
  return readResponse(
    await fetch("/api/app/record", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tableId, recordId, patch }),
    }),
  );
}

export async function createAppRecord(
  request: AppCreateRequest,
): Promise<{ state: AppState; record: AppRecord; tableId: string }> {
  return readResponse(
    await fetch("/api/app/record", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    }),
  );
}

export async function duplicateAppRecord(
  request: AppRecordActionRequest,
): Promise<{ state: AppState; record: AppRecord; tableId: string }> {
  return readResponse(
    await fetch("/api/app/record/duplicate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    }),
  );
}

export async function deleteAppRecord(
  request: AppRecordActionRequest,
): Promise<{ state: AppState; tableId: string; deletedRecordId: string }> {
  const query = new URLSearchParams({
    tableId: request.tableId,
    recordId: request.recordId,
  });
  return readResponse(
    await fetch(`/api/app/record?${query}`, {
      method: "DELETE",
    }),
  );
}

export async function getPreviewPayload(
  selection: DebugSelection,
): Promise<DebugPayload> {
  const query = new URLSearchParams({
    productionId: selection.productionId,
    shotId: selection.shotId,
    screenInstanceId: selection.screenInstanceId,
    frame: String(selection.frame),
  });
  return readResponse(await fetch(`/api/app/preview?${query}`));
}

export async function renderPreviewFrame(
  request: RenderFrameRequest,
): Promise<RenderFrameResult> {
  return readResponse(
    await fetch("/api/app/render-frame", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    }),
  );
}
