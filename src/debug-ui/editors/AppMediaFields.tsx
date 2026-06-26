import type { ComponentType } from "react";
import type { AppRecord } from "../api/client.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import {
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

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
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  AvatarPreview: ComponentType<AvatarPreviewProps>;
  onMetadataRootChange: (nextRoot: JsonValue) => void;
}

function appIcon(metadataRoot: Record<string, unknown>) {
  const icon = metadataRoot.icon;
  return icon && typeof icon === "object" && !Array.isArray(icon)
    ? (icon as Record<string, unknown>)
    : {};
}

export function AppIconFields({
  record,
  drafts,
  metadataRoot,
  mediaRoot,
  nativeBridge,
  relativePathFromRoot,
  AvatarPreview,
  onMetadataRootChange,
}: AppIconFieldsProps) {
  const icon = appIcon(metadataRoot);
  const filePath = typeof icon.filePath === "string" ? icon.filePath : "";
  const scale = typeof icon.scale === "number" ? icon.scale : 1;
  const offsetX = typeof icon.offsetX === "number" ? icon.offsetX : 0;
  const offsetY = typeof icon.offsetY === "number" ? icon.offsetY : 0;

  function patchIcon(patch: Record<string, unknown>) {
    onMetadataRootChange({
      ...metadataRoot,
      icon: {
        baseSize: 640,
        scale: 1,
        offsetX: 0,
        offsetY: 0,
        ...icon,
        ...patch,
      },
    });
  }

  async function chooseAppIconFile() {
    const [selectedPath] = await (nativeBridge?.pickFile?.() ?? Promise.resolve([]));
    if (selectedPath) {
      patchIcon({
        filePath: relativePathFromRoot(selectedPath, mediaRoot),
      });
    }
  }

  return (
    <>
      <InspectorFieldRow
        key="app_icon_file"
        className="record-editor-field record-editor-field-string"
        label={<span>App icon image</span>}
        control={
          <div className="media-file-control actor-avatar-file-control">
            <DeferredTextInput
              value={filePath}
              onCommit={(nextValue) => patchIcon({ filePath: nextValue })}
            />
            <button
              type="button"
              className="record-editor-compact-button"
              disabled={!nativeBridge?.pickFile}
              onClick={() => {
                void chooseAppIconFile();
              }}
            >
              Browse…
            </button>
          </div>
        }
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
      <InspectorFieldRow
        key="app_icon_scale"
        className="record-editor-field record-editor-field-number"
        label={<span>Icon scale</span>}
        control={
          <input
            type="number"
            step={0.01}
            min={0.01}
            value={String(scale)}
            onChange={(event) => patchIcon({ scale: Number(event.target.value) })}
          />
        }
      />
      <InspectorFieldRow
        key="app_icon_offset_x"
        className="record-editor-field record-editor-field-number"
        label={<span>Icon offset X</span>}
        control={
          <input
            type="number"
            step={1}
            value={String(offsetX)}
            onChange={(event) => patchIcon({ offsetX: Number(event.target.value) })}
          />
        }
      />
      <InspectorFieldRow
        key="app_icon_offset_y"
        className="record-editor-field record-editor-field-number"
        label={<span>Icon offset Y</span>}
        control={
          <input
            type="number"
            step={1}
            value={String(offsetY)}
            onChange={(event) => patchIcon({ offsetY: Number(event.target.value) })}
          />
        }
      />
    </>
  );
}

function appWallpaper(appTokenRoot: Record<string, unknown>) {
  const wallpaper = appTokenRoot.wallpaper;
  return wallpaper && typeof wallpaper === "object" && !Array.isArray(wallpaper)
    ? (wallpaper as Record<string, unknown>)
    : {};
}

function appWallpaperModeColor(
  appTokenRoot: Record<string, unknown>,
  mode: "light" | "dark",
) {
  const modes = appTokenRoot.modes;
  const modeRoot =
    modes && typeof modes === "object" && !Array.isArray(modes)
      ? (modes as Record<string, unknown>)[mode]
      : undefined;
  const modeObject =
    modeRoot && typeof modeRoot === "object" && !Array.isArray(modeRoot)
      ? (modeRoot as Record<string, unknown>)
      : {};
  const wallpaper =
    modeObject.wallpaper &&
    typeof modeObject.wallpaper === "object" &&
    !Array.isArray(modeObject.wallpaper)
      ? (modeObject.wallpaper as Record<string, unknown>)
      : {};
  const color = wallpaper.color;
  return typeof color === "string" && color.trim()
    ? color
    : mode === "light"
      ? "#f7f7f7"
      : "#000000";
}

interface AppWallpaperEditorProps {
  appTokenRoot: Record<string, unknown>;
  mediaRoot: string;
  nativeBridge: AppNativeBridge | undefined;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  MediaCoverPreview: ComponentType<MediaCoverPreviewProps>;
  paletteCatalog?: PaletteColorCatalog;
  onTokenRootChange: (nextValue: JsonValue) => void;
}

export function AppWallpaperEditor({
  appTokenRoot,
  mediaRoot,
  nativeBridge,
  relativePathFromRoot,
  MediaCoverPreview,
  paletteCatalog,
  onTokenRootChange,
}: AppWallpaperEditorProps) {
  const wallpaper = appWallpaper(appTokenRoot);
  const kind = String(wallpaper.kind ?? "solid");
  const opacity =
    typeof wallpaper.opacity === "number" && Number.isFinite(wallpaper.opacity)
      ? wallpaper.opacity
      : 1;
  const image =
    wallpaper.image && typeof wallpaper.image === "object" && !Array.isArray(wallpaper.image)
      ? (wallpaper.image as Record<string, unknown>)
      : {};
  const filePath = typeof image.filePath === "string" ? image.filePath : "";

  function updateAppWallpaper(path: JsonPath, nextValue: JsonValue) {
    onTokenRootChange(
      setAtPath(appTokenRoot as JsonValue, ["wallpaper", ...path], nextValue),
    );
  }

  function updateAppWallpaperModeColor(mode: "light" | "dark", nextColor: string) {
    onTokenRootChange(
      setAtPath(
        appTokenRoot as JsonValue,
        ["modes", mode, "wallpaper", "color"],
        nextColor,
      ),
    );
  }

  async function chooseWallpaperFile() {
    const [selectedPath] = await (nativeBridge?.pickFile?.() ?? Promise.resolve([]));
    if (selectedPath) {
      updateAppWallpaper(
        ["image"],
        {
          filePath: relativePathFromRoot(selectedPath, mediaRoot),
          fit: "cover",
          position: "center",
        },
      );
    }
  }

  return (
    <div className="record-editor-field-stack record-editor-single-column wallpaper-editor">
      <InspectorFieldRow
        className="record-editor-field record-editor-field-string"
        label={<span>Kind</span>}
        control={
          <select
            value={kind === "image" ? "image" : "solid"}
            onChange={(event) =>
              updateAppWallpaper(["kind"], event.target.value)
            }
          >
            <option value="solid">solid</option>
            <option value="image">image</option>
          </select>
        }
      />
      <InspectorFieldRow
        className="record-editor-field record-editor-field-number"
        label={<span>Opacity</span>}
        control={
          <input
            type="number"
            min={0}
            max={1}
            step={0.01}
            value={String(opacity)}
            onChange={(event) =>
              updateAppWallpaper(["opacity"], Number(event.target.value))
            }
          />
        }
      />
      {kind === "image" ? (
        <>
          <InspectorFieldRow
            className="record-editor-field record-editor-field-string"
            label={<span>Image</span>}
            control={
              <div className="media-file-control actor-avatar-file-control">
                <DeferredTextInput
                  value={filePath}
                  onCommit={(nextValue) =>
                    updateAppWallpaper(["image"], {
                      filePath: nextValue,
                      fit: "cover",
                      position: "center",
                    })
                  }
                />
                <button
                  type="button"
                  className="record-editor-compact-button"
                  disabled={!nativeBridge?.pickFile}
                  onClick={() => {
                    void chooseWallpaperFile();
                  }}
                >
                  Browse…
                </button>
              </div>
            }
          />
          <div className="wallpaper-preview-frame">
            <MediaCoverPreview
              filePath={filePath}
              mediaRoot={mediaRoot}
              fallbackLabel="No wallpaper image"
            />
            <small>Fit: cover · Position: center</small>
          </div>
        </>
      ) : (
        <div className="wallpaper-color-grid">
          <div className="wallpaper-color-heading" />
          <div className="wallpaper-color-heading">Light</div>
          <div className="wallpaper-color-heading">Dark</div>
          <span>Color</span>
          {(["light", "dark"] as const).map((mode) => (
            <div key={mode} className="actor-color-control compact-color-control">
              <ColorValueEditor
                label={`Wallpaper ${mode}`}
                value={appWallpaperModeColor(appTokenRoot, mode)}
                paletteCatalog={paletteCatalog}
                onChange={(nextColor) => updateAppWallpaperModeColor(mode, nextColor)}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
