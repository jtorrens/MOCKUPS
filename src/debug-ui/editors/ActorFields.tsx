import type { ComponentType } from "react";
import type { AppRecord } from "../api/client.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { DeferredNumberInput } from "../editor-ui/DeferredNumberInput.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { parsedObject } from "./recordJsonUtils.js";

interface ActorNativeBridge {
  pickFile?: () => Promise<string[]>;
}

interface ActorAvatarPreviewProps {
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

interface ActorFieldsContext {
  record: AppRecord | undefined;
  drafts: Record<string, string>;
  mediaRoot: string;
  nativeBridge: ActorNativeBridge | undefined;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  AvatarPreview: ComponentType<ActorAvatarPreviewProps>;
  paletteCatalog?: PaletteColorCatalog;
  setMetadataRaw: (nextRaw: string) => void;
}

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

function actorInitials({
  record,
  drafts,
}: Pick<ActorFieldsContext, "record" | "drafts">) {
  const displayName = String(drafts.display_name ?? record?.display_name ?? "");
  const shortName = String(drafts.short_name ?? record?.short_name ?? "");
  const source = shortName || displayName;
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

const actorThemeModes = ["light", "dark"] as const;

function isHexColor(value: unknown): value is string {
  return typeof value === "string" && /^#[0-9a-f]{6}$/i.test(value);
}

function isPaletteColorToken(
  value: unknown,
  paletteCatalog: PaletteColorCatalog | undefined,
): value is string {
  return (
    typeof value === "string" &&
    Boolean(paletteCatalog?.byToken.has(value))
  );
}

function isStoredColor(
  value: unknown,
  paletteCatalog: PaletteColorCatalog | undefined,
): value is string {
  return isHexColor(value) || isPaletteColorToken(value, paletteCatalog);
}

function resolvedActorColorValue(
  value: string,
  paletteCatalog: PaletteColorCatalog | undefined,
) {
  return paletteCatalog?.byToken.get(value)?.valueHex ?? value;
}

function actorModeColor(
  drafts: Record<string, string>,
  mode: (typeof actorThemeModes)[number] = "light",
  field: "color" | "avatarTextColor" = "color",
  paletteCatalog?: PaletteColorCatalog,
) {
  const root = parsedObject(drafts.metadata_json ?? "{}");
  const modes = root.modes;
  const modeRoot =
    modes && typeof modes === "object" && !Array.isArray(modes)
      ? (modes as Record<string, unknown>)[mode]
      : undefined;
  if (modeRoot && typeof modeRoot === "object" && !Array.isArray(modeRoot)) {
    const value = (modeRoot as Record<string, unknown>)[field];
    if (isStoredColor(value, paletteCatalog)) {
      return value;
    }
  }
  if (field === "color" && isStoredColor(root.color, paletteCatalog)) {
    return root.color;
  }
  const avatar = root.avatar;
  if (
    field === "avatarTextColor" &&
    avatar &&
    typeof avatar === "object" &&
    !Array.isArray(avatar) &&
    isStoredColor((avatar as Record<string, unknown>).textColor, paletteCatalog)
  ) {
    return (avatar as Record<string, string>).textColor;
  }
  return field === "color" ? "#64748b" : "#ffffff";
}

function actorColor(
  drafts: Record<string, string>,
  paletteCatalog?: PaletteColorCatalog,
) {
  return actorModeColor(drafts, "light", "color", paletteCatalog);
}

function actorAvatarTextColor(
  drafts: Record<string, string>,
  paletteCatalog?: PaletteColorCatalog,
) {
  return actorModeColor(drafts, "light", "avatarTextColor", paletteCatalog);
}

function actorAvatar(drafts: Record<string, string>) {
  const root = parsedObject(drafts.metadata_json ?? "{}");
  const avatar = root.avatar;
  return avatar && typeof avatar === "object" && !Array.isArray(avatar)
    ? (avatar as Record<string, unknown>)
    : {};
}

function setActorModeColor({
  drafts,
  setMetadataRaw,
  mode,
  field,
  nextColor,
}: Pick<ActorFieldsContext, "drafts" | "setMetadataRaw"> & {
  mode: (typeof actorThemeModes)[number];
  field: "color" | "avatarTextColor";
  nextColor: string;
}) {
  const root = parsedObject(drafts.metadata_json ?? "{}");
  const modes =
    root.modes && typeof root.modes === "object" && !Array.isArray(root.modes)
      ? { ...(root.modes as Record<string, unknown>) }
      : {};
  const modeRoot =
    modes[mode] && typeof modes[mode] === "object" && !Array.isArray(modes[mode])
      ? { ...(modes[mode] as Record<string, unknown>) }
      : {};
  setMetadataRaw(
    stringifyJson({
      ...root,
      modes: {
        ...modes,
        [mode]: {
          ...modeRoot,
          [field]: nextColor,
        },
      },
    }),
  );
}

function setActorAvatarPatch({
  drafts,
  setMetadataRaw,
  patch,
}: Pick<ActorFieldsContext, "drafts" | "setMetadataRaw"> & {
  patch: Record<string, unknown>;
}) {
  const root = parsedObject(drafts.metadata_json ?? "{}");
  const avatar = actorAvatar(drafts);
  setMetadataRaw(
    stringifyJson({
      ...root,
      avatar: {
        baseSize: 640,
        scale: 1,
        offsetX: 0,
        offsetY: 0,
        ...avatar,
        ...patch,
      },
    }),
  );
}

function ActorColorField(context: ActorFieldsContext) {
  const initials = actorInitials(context);
  const rows = [
    { field: "color" as const, label: "Actor color" },
    { field: "avatarTextColor" as const, label: "Avatar text color" },
  ];
  return (
    <>
      {rows.map((row) => (
        <InspectorFieldRow
          key={`actor_${row.field}`}
          className="record-editor-field record-editor-field-string actor-color-field"
          label={<span>{row.label}</span>}
          control={
            <div className="actor-color-control actor-mode-color-grid">
              {actorThemeModes.map((mode) => {
                const color = actorModeColor(
                  context.drafts,
                  mode,
                  row.field,
                  context.paletteCatalog,
                );
                const previewBackground =
                  row.field === "color"
                    ? color
                    : actorModeColor(
                        context.drafts,
                        mode,
                        "color",
                        context.paletteCatalog,
                      );
                const previewText =
                  row.field === "avatarTextColor"
                    ? color
                    : actorModeColor(
                        context.drafts,
                        mode,
                        "avatarTextColor",
                        context.paletteCatalog,
                      );
                return (
                  <label key={mode} className="actor-mode-color-cell">
                    <span className="actor-mode-color-label">{mode}</span>
                    <span
                      className="actor-color-preview"
                      style={{
                        backgroundColor: resolvedActorColorValue(
                          previewBackground,
                          context.paletteCatalog,
                        ),
                        color: resolvedActorColorValue(
                          previewText,
                          context.paletteCatalog,
                        ),
                      }}
                      aria-hidden="true"
                    >
                      {row.field === "color" ? initials : "Aa"}
                    </span>
                    <ColorValueEditor
                      label={`${row.label} ${mode}`}
                      value={color}
                      paletteCatalog={context.paletteCatalog}
                      onChange={(nextColor) =>
                        setActorModeColor({
                          ...context,
                          mode,
                          field: row.field,
                          nextColor,
                        })
                      }
                    />
                  </label>
                );
              })}
            </div>
          }
        />
      ))}
    </>
  );
}

function ActorAvatarFields(context: ActorFieldsContext) {
  const avatar = actorAvatar(context.drafts);
  const filePath = typeof avatar.filePath === "string" ? avatar.filePath : "";
  const scale = typeof avatar.scale === "number" ? avatar.scale : 1;
  const offsetX = typeof avatar.offsetX === "number" ? avatar.offsetX : 0;
  const offsetY = typeof avatar.offsetY === "number" ? avatar.offsetY : 0;
  const useInitials = avatar.useInitials === true;
  const textColor = actorAvatarTextColor(context.drafts, context.paletteCatalog);
  const backgroundColor = actorColor(context.drafts, context.paletteCatalog);
  const initials = actorInitials(context);
  const AvatarPreview = context.AvatarPreview;

  function patchAvatar(patch: Record<string, unknown>) {
    setActorAvatarPatch({ ...context, patch });
  }

  async function chooseAvatarFile() {
    const [selectedPath] = await (context.nativeBridge?.pickFile?.() ?? Promise.resolve([]));
    if (selectedPath) {
      patchAvatar({
        filePath: context.relativePathFromRoot(selectedPath, context.mediaRoot),
      });
    }
  }

  return (
    <>
      <InspectorFieldRow
        key="actor_avatar_use_initials"
        className="record-editor-field record-editor-field-boolean"
        label={<span>Use initials</span>}
        control={
          <input
            type="checkbox"
            checked={useInitials}
            onChange={(event) => patchAvatar({ useInitials: event.target.checked })}
          />
        }
      />
      <InspectorFieldRow
        key="actor_avatar_file"
        className="record-editor-field record-editor-field-string"
        label={<span>Avatar image</span>}
        control={
          <div className="media-file-control actor-avatar-file-control">
            <DeferredTextInput
              value={filePath}
              onCommit={(nextValue) => patchAvatar({ filePath: nextValue })}
            />
            <button
              type="button"
              className="record-editor-compact-button"
              disabled={!context.nativeBridge?.pickFile}
              onClick={() => {
                void chooseAvatarFile();
              }}
            >
              Browse…
            </button>
          </div>
        }
      />
      <div className="actor-avatar-frame" aria-label="Avatar crop frame">
      <AvatarPreview
        filePath={filePath}
        mediaRoot={context.mediaRoot}
        scale={scale}
        offsetX={offsetX}
        offsetY={offsetY}
        useInitials={useInitials}
        backgroundColor={resolvedActorColorValue(
          backgroundColor,
          context.paletteCatalog,
        )}
        textColor={resolvedActorColorValue(textColor, context.paletteCatalog)}
        initials={initials}
      />
        <small>Base avatar frame: 640×640</small>
      </div>
      <InspectorFieldRow
        key="actor_avatar_scale"
        className="record-editor-field record-editor-field-number"
        label={<span>Avatar scale</span>}
        control={
          <DeferredNumberInput
            step={0.01}
            min={0.01}
            value={scale}
            onCommit={(nextValue) => patchAvatar({ scale: nextValue })}
          />
        }
      />
      <InspectorFieldRow
        key="actor_avatar_offset_x"
        className="record-editor-field record-editor-field-number"
        label={<span>Avatar offset X</span>}
        control={
          <DeferredNumberInput
            step={1}
            value={offsetX}
            onCommit={(nextValue) => patchAvatar({ offsetX: nextValue })}
          />
        }
      />
      <InspectorFieldRow
        key="actor_avatar_offset_y"
        className="record-editor-field record-editor-field-number"
        label={<span>Avatar offset Y</span>}
        control={
          <DeferredNumberInput
            step={1}
            value={offsetY}
            onCommit={(nextValue) => patchAvatar({ offsetY: nextValue })}
          />
        }
      />
    </>
  );
}

export function ActorMetadataFields(context: ActorFieldsContext) {
  return (
    <div className="flat-json-field-group">
      <ActorColorField {...context} />
      <ActorAvatarFields {...context} />
    </div>
  );
}
