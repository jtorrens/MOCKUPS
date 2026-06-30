import type { ComponentType } from "react";
import type { AppRecord } from "../api/client.js";
import {
  APP_CONFIG_BINDINGS,
  APP_METADATA_BINDINGS,
} from "../../domain/fields/appFields.js";
import { TokenOverrideEditor } from "../components/json-editor/TokenOverrideEditor.js";
import { jsonUiHintsFromFieldBindings } from "../components/json-editor/fieldDefinitionHints.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";

interface AppNativeBridge {
  pickFile?: () => Promise<string[]>;
}

interface AvatarPreviewProps {
  filePath: string;
  mediaRoot: string;
  scale: number;
  offsetX: number;
  offsetY: number;
  useInitials: boolean;
  backgroundColor: string;
  textColor: string;
  initials: string;
}

interface MediaCoverPreviewProps {
  filePath: string;
  mediaRoot: string;
  fallbackLabel: string;
}

function appInitials(record: AppRecord | undefined, drafts: Record<string, string>) {
  const source = String(drafts.name ?? record?.name ?? "");
  const words = source
    .split(/\s+/)
    .map((word) => word.trim())
    .filter(Boolean);
  if (words.length === 0) return "?";
  return words
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? "")
    .join("");
}

interface AppIconFieldsProps {
  record: AppRecord | undefined;
  drafts: Record<string, string>;
  metadataRoot: Record<string, unknown>;
  mediaRoot: string;
  nativeBridge: AppNativeBridge | undefined;
  AvatarPreview: ComponentType<AvatarPreviewProps>;
  onMetadataRootChange: (nextRoot: JsonValue) => void;
}

function appIcon(metadataRoot: Record<string, unknown>) {
  const icon = metadataRoot.icon;
  return icon && typeof icon === "object" && !Array.isArray(icon)
    ? (icon as Record<string, unknown>)
    : {};
}

const appIconHints = jsonUiHintsFromFieldBindings(
  APP_METADATA_BINDINGS.filter((binding) => binding.outputPath[0] === "icon"),
);

const appWallpaperHints = jsonUiHintsFromFieldBindings(APP_CONFIG_BINDINGS);

export function AppIconFields({
  record,
  drafts,
  metadataRoot,
  mediaRoot,
  nativeBridge,
  AvatarPreview,
  onMetadataRootChange,
}: AppIconFieldsProps) {
  const icon = appIcon(metadataRoot);
  const filePath = typeof icon.filePath === "string" ? icon.filePath : "";
  const scale = typeof icon.scale === "number" ? icon.scale : 1;
  const offsetX = typeof icon.offsetX === "number" ? icon.offsetX : 0;
  const offsetY = typeof icon.offsetY === "number" ? icon.offsetY : 0;

  return (
    <>
      <TokenOverrideEditor
        rootValue={metadataRoot as JsonValue}
        inheritedRoot={{}}
        hints={appIconHints}
        mediaRoot={mediaRoot}
        nativeBridge={nativeBridge}
        onRootChange={onMetadataRootChange}
      />
      <div className="actor-avatar-frame app-icon-frame" aria-label="App icon crop frame">
        <AvatarPreview
          filePath={filePath}
          mediaRoot={mediaRoot}
          scale={scale}
          offsetX={offsetX}
          offsetY={offsetY}
          useInitials={false}
          backgroundColor="#f2f4f7"
          textColor="#475467"
          initials={appInitials(record, drafts)}
        />
        <small>Base icon frame: 640×640</small>
      </div>
    </>
  );
}

function appWallpaper(appTokenRoot: Record<string, unknown>) {
  const wallpaper = appTokenRoot.wallpaper;
  return wallpaper && typeof wallpaper === "object" && !Array.isArray(wallpaper)
    ? (wallpaper as Record<string, unknown>)
    : {};
}

interface AppWallpaperEditorProps {
  appTokenRoot: Record<string, unknown>;
  mediaRoot: string;
  nativeBridge: AppNativeBridge | undefined;
  MediaCoverPreview: ComponentType<MediaCoverPreviewProps>;
  paletteCatalog?: PaletteColorCatalog;
  onTokenRootChange: (nextValue: JsonValue) => void;
}

export function AppWallpaperEditor({
  appTokenRoot,
  mediaRoot,
  nativeBridge,
  MediaCoverPreview,
  paletteCatalog,
  onTokenRootChange,
}: AppWallpaperEditorProps) {
  const wallpaper = appWallpaper(appTokenRoot);
  const kind = String(wallpaper.kind ?? "solid");
  const image =
    wallpaper.image && typeof wallpaper.image === "object" && !Array.isArray(wallpaper.image)
      ? (wallpaper.image as Record<string, unknown>)
      : {};
  const filePath = typeof image.filePath === "string" ? image.filePath : "";

  return (
    <div className="record-editor-field-stack record-editor-single-column wallpaper-editor">
      <TokenOverrideEditor
        rootValue={appTokenRoot as JsonValue}
        inheritedRoot={{}}
        hints={appWallpaperHints}
        mediaRoot={mediaRoot}
        nativeBridge={nativeBridge}
        paletteCatalog={paletteCatalog}
        onRootChange={onTokenRootChange}
      />
      {kind === "image" ? (
        <div className="wallpaper-preview-frame">
          <MediaCoverPreview
            filePath={filePath}
            mediaRoot={mediaRoot}
            fallbackLabel="No wallpaper image"
          />
          <small>Fit: cover · Position: center</small>
        </div>
      ) : null}
    </div>
  );
}
