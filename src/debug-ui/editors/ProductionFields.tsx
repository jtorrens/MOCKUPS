import type { AppFieldDefinition } from "../api/client.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import { parsedObject } from "./recordJsonUtils.js";

interface ProductionNativeBridge {
  pickDirectory?: () => Promise<string[]>;
}

interface ProductionSettingsFieldProps {
  field: AppFieldDefinition;
  rawValue: string;
  nativeBridge: ProductionNativeBridge | undefined;
  onChange: (column: string, value: JsonValue) => void;
}

export function ProductionSettingsField({
  field,
  rawValue,
  nativeBridge,
  onChange,
}: ProductionSettingsFieldProps) {
  const root = parsedObject(rawValue);
  const mediaRoot = typeof root.mediaRoot === "string" ? root.mediaRoot : "";

  async function chooseDirectory() {
    const [directory] = await (nativeBridge?.pickDirectory?.() ?? Promise.resolve([]));
    if (directory) {
      onChange(field.column, {
        ...root,
        mediaRoot: directory,
      });
    }
  }

  return (
    <div key={field.column} className="flat-json-field-group">
      <InspectorFieldRow
        className="record-editor-field flat-json-row"
        label={<span>Media root</span>}
        control={
          <div className="media-file-control">
            <DeferredTextInput
              value={mediaRoot}
              onCommit={(nextValue) =>
                onChange(field.column, {
                  ...root,
                  mediaRoot: nextValue,
                })
              }
            />
            <button
              type="button"
              className="record-editor-compact-button"
              disabled={!nativeBridge?.pickDirectory}
              onClick={() => {
                void chooseDirectory();
              }}
            >
              Browse…
            </button>
          </div>
        }
      />
    </div>
  );
}
