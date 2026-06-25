import type { ComponentType } from "react";
import type { AppRecord } from "../api/client.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
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

function actorColor(drafts: Record<string, string>) {
  const root = parsedObject(drafts.metadata_json ?? "{}");
  return typeof root.color === "string" && /^#[0-9a-f]{6}$/i.test(root.color)
    ? root.color
    : "#64748b";
}

function actorAvatar(drafts: Record<string, string>) {
  const root = parsedObject(drafts.metadata_json ?? "{}");
  const avatar = root.avatar;
  return avatar && typeof avatar === "object" && !Array.isArray(avatar)
    ? (avatar as Record<string, unknown>)
    : {};
}

function setActorColor({
  drafts,
  setMetadataRaw,
  nextColor,
}: Pick<ActorFieldsContext, "drafts" | "setMetadataRaw"> & {
  nextColor: string;
}) {
  const root = parsedObject(drafts.metadata_json ?? "{}");
  setMetadataRaw(
    stringifyJson({
      ...root,
      color: nextColor,
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
  const color = actorColor(context.drafts);
  const initials = actorInitials(context);
  return (
    <InspectorFieldRow
      key="actor_color"
      className="record-editor-field record-editor-field-string actor-color-field"
      label={<span>Color</span>}
      control={
        <div className="actor-color-control">
          <span
            className="actor-color-preview"
            style={{ backgroundColor: color }}
            aria-hidden="true"
          >
            {initials}
          </span>
          <input
            aria-label="Actor color"
            type="color"
            value={color}
            onChange={(event) =>
              setActorColor({ ...context, nextColor: event.target.value })
            }
          />
          <input
            aria-label="Actor color hex"
            value={color}
            onChange={(event) =>
              setActorColor({ ...context, nextColor: event.target.value })
            }
          />
        </div>
      }
    />
  );
}

function ActorAvatarFields(context: ActorFieldsContext) {
  const avatar = actorAvatar(context.drafts);
  const filePath = typeof avatar.filePath === "string" ? avatar.filePath : "";
  const scale = typeof avatar.scale === "number" ? avatar.scale : 1;
  const offsetX = typeof avatar.offsetX === "number" ? avatar.offsetX : 0;
  const offsetY = typeof avatar.offsetY === "number" ? avatar.offsetY : 0;
  const useInitials = avatar.useInitials === true;
  const textColor =
    typeof avatar.textColor === "string" && /^#[0-9a-f]{6}$/i.test(avatar.textColor)
      ? avatar.textColor
      : "#ffffff";
  const backgroundColor = actorColor(context.drafts);
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
      <InspectorFieldRow
        key="actor_avatar_text_color"
        className="record-editor-field record-editor-field-string actor-color-field"
        label={<span>Avatar text color</span>}
        control={
          <div className="actor-color-control">
            <span
              className="actor-color-preview"
              style={{ backgroundColor: textColor, color: backgroundColor }}
              aria-hidden="true"
            >
              Aa
            </span>
            <input
              aria-label="Avatar text color"
              type="color"
              value={textColor}
              onChange={(event) => patchAvatar({ textColor: event.target.value })}
            />
            <input
              aria-label="Avatar text color hex"
              value={textColor}
              onChange={(event) => patchAvatar({ textColor: event.target.value })}
            />
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
          backgroundColor={backgroundColor}
          textColor={textColor}
          initials={initials}
        />
        <small>Base avatar frame: 640×640</small>
      </div>
      <InspectorFieldRow
        key="actor_avatar_scale"
        className="record-editor-field record-editor-field-number"
        label={<span>Avatar scale</span>}
        control={
          <input
            type="number"
            step={0.01}
            min={0.01}
            value={String(scale)}
            onChange={(event) => patchAvatar({ scale: Number(event.target.value) })}
          />
        }
      />
      <InspectorFieldRow
        key="actor_avatar_offset_x"
        className="record-editor-field record-editor-field-number"
        label={<span>Avatar offset X</span>}
        control={
          <input
            type="number"
            step={1}
            value={String(offsetX)}
            onChange={(event) => patchAvatar({ offsetX: Number(event.target.value) })}
          />
        }
      />
      <InspectorFieldRow
        key="actor_avatar_offset_y"
        className="record-editor-field record-editor-field-number"
        label={<span>Avatar offset Y</span>}
        control={
          <input
            type="number"
            step={1}
            value={String(offsetY)}
            onChange={(event) => patchAvatar({ offsetY: Number(event.target.value) })}
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
