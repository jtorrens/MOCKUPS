import type { ComponentType } from "react";
import type { AppRecord } from "../api/client.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { ACTOR_FIELDS } from "../../domain/fields/actorFields.js";
import {
  controlDefinitionForField,
  editorMetadataForField,
  type EditorControlKind,
} from "../editor-ui/ValueKindControlRegistry.js";
import type { FieldDefinition } from "../../domain/value-system/index.js";
import { parsedObject } from "./recordJsonUtils.js";
import {
  DictionaryFieldControl,
  DICTIONARY_CONTROL_CLASS,
  DICTIONARY_FIELD_CLASS,
} from "../editor-ui/DictionaryFieldControl.js";

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
  initialsPadding?: number;
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

function actorFieldMetadata(
  field: FieldDefinition,
  expectedControl: EditorControlKind,
) {
  const control = controlDefinitionForField(field).control;
  if (control !== expectedControl) {
    throw new Error(
      `Actor field "${field.id}" expected ${expectedControl} control, got ${control}`,
    );
  }
  return editorMetadataForField(field);
}

function ActorDictionaryFieldRow({
  field,
  expectedControl,
  value,
  onChange,
}: {
  field: FieldDefinition;
  expectedControl: EditorControlKind;
  value: boolean | number | string;
  onChange: (nextValue: unknown) => void;
}) {
  const metadata = actorFieldMetadata(field, expectedControl);
  return (
    <InspectorFieldRow
      className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
      label={<span>{metadata.label}</span>}
      control={
        <DictionaryFieldControl
          field={field}
          value={value}
          onChange={onChange}
        />
      }
    />
  );
}

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
    {
      field: "color" as const,
      label: "Actor color",
      fields: {
        light: ACTOR_FIELDS.colorLight,
        dark: ACTOR_FIELDS.colorDark,
      },
    },
    {
      field: "avatarTextColor" as const,
      label: "Avatar text color",
      fields: {
        light: ACTOR_FIELDS.avatarTextColorLight,
        dark: ACTOR_FIELDS.avatarTextColorDark,
      },
    },
  ];
  return (
    <>
      {rows.map((row) => (
        <InspectorFieldRow
          key={`actor_${row.field}`}
          className={`record-editor-field record-editor-field-string ${DICTIONARY_FIELD_CLASS} actor-color-field`}
          label={<span>{row.label}</span>}
          control={
            <div className={`actor-color-control actor-mode-color-grid ${DICTIONARY_CONTROL_CLASS}`}>
              {actorThemeModes.map((mode) => {
                actorFieldMetadata(row.fields[mode], "paletteColorToken");
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
  const filePathField = actorFieldMetadata(
    ACTOR_FIELDS.avatarFilePath,
    "relativeFilePath",
  );
  const scaleField = actorFieldMetadata(ACTOR_FIELDS.avatarScale, "number");
  const initialsPaddingField = actorFieldMetadata(
    ACTOR_FIELDS.avatarInitialsPadding,
    "number",
  );
  const avatar = actorAvatar(context.drafts);
  const filePath = typeof avatar.filePath === "string" ? avatar.filePath : "";
  const scale = typeof avatar.scale === "number" ? avatar.scale : 1;
  const offsetX = typeof avatar.offsetX === "number" ? avatar.offsetX : 0;
  const offsetY = typeof avatar.offsetY === "number" ? avatar.offsetY : 0;
  const initialsPadding =
    typeof avatar.initialsPadding === "number"
      ? avatar.initialsPadding
      : Number(ACTOR_FIELDS.avatarInitialsPadding.defaultValue ?? 96);
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
      <ActorDictionaryFieldRow
        key="actor_avatar_use_initials"
        field={ACTOR_FIELDS.avatarUseInitials}
        expectedControl="checkbox"
        value={useInitials}
        onChange={(nextValue) => patchAvatar({ useInitials: nextValue === true })}
      />
      <InspectorFieldRow
        key="actor_avatar_file"
        className={`record-editor-field record-editor-field-string ${DICTIONARY_FIELD_CLASS}`}
        label={<span>{filePathField.label}</span>}
        control={
          <div className={`media-file-control actor-avatar-file-control ${DICTIONARY_CONTROL_CLASS}`}>
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
        initialsPadding={initialsPadding}
      />
        <small>Base avatar frame: 640×640</small>
      </div>
      <InspectorFieldRow
        key="actor_avatar_scale"
        className={`record-editor-field record-editor-field-number ${DICTIONARY_FIELD_CLASS}`}
        label={<span>{scaleField.label}</span>}
        control={
          <DictionaryFieldControl
            field={ACTOR_FIELDS.avatarScale}
            value={scale}
            onChange={(nextValue) => patchAvatar({ scale: Number(nextValue) })}
          />
        }
      />
      <InspectorFieldRow
        key="actor_avatar_offset"
        className={`record-editor-field record-editor-field-number ${DICTIONARY_FIELD_CLASS}`}
        label={<span>Avatar offset</span>}
        control={
          <div className={`record-editor-field-pair ${DICTIONARY_CONTROL_CLASS}`}>
            <label className="record-editor-field-pair-item">
              <span>X</span>
              <DictionaryFieldControl
                field={ACTOR_FIELDS.avatarOffsetX}
                value={offsetX}
                onChange={(nextValue) =>
                  patchAvatar({ offsetX: Math.round(Number(nextValue)) })
                }
              />
            </label>
            <label className="record-editor-field-pair-item">
              <span>Y</span>
              <DictionaryFieldControl
                field={ACTOR_FIELDS.avatarOffsetY}
                value={offsetY}
                onChange={(nextValue) =>
                  patchAvatar({ offsetY: Math.round(Number(nextValue)) })
                }
              />
            </label>
          </div>
        }
      />
      <InspectorFieldRow
        key="actor_avatar_initials_padding"
        className={`record-editor-field record-editor-field-number ${DICTIONARY_FIELD_CLASS}`}
        label={<span>{initialsPaddingField.label}</span>}
        control={
          <DictionaryFieldControl
            field={ACTOR_FIELDS.avatarInitialsPadding}
            value={initialsPadding}
            onChange={(nextValue) =>
              patchAvatar({
                initialsPadding: Math.max(0, Math.round(Number(nextValue))),
              })
            }
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
