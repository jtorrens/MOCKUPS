import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type {
  JsonPath,
  JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";

interface KeyboardBehaviorFieldsProps {
  keyboardRoot: Record<string, JsonValue>;
  updateBehaviorValue: (path: JsonPath, nextValue: JsonValue) => void;
}

export function KeyboardBehaviorFields({
  keyboardRoot,
  updateBehaviorValue,
}: KeyboardBehaviorFieldsProps) {
  return (
    <>
      <InspectorFieldRow
        key="keyboardMode"
        className="record-editor-field record-editor-field-string"
        label={<span>Keyboard mode</span>}
        control={
          <select
            value={String(
              typeof keyboardRoot.mode === "string"
                ? keyboardRoot.mode
                : "lowercase",
            )}
            onChange={(event) =>
              updateBehaviorValue(
                ["keyboard", "mode"],
                event.target.value as JsonValue,
              )
            }
          >
            <option value="lowercase">Minúsculas</option>
            <option value="shift">Mayúsculas</option>
            <option value="numeric">Números</option>
            <option value="symbols">Símbolos</option>
            <option value="emoji">Emoji</option>
          </select>
        }
      />
      <InspectorFieldRow
        key="keyboardLanguage"
        className="record-editor-field record-editor-field-string"
        label={<span>Keyboard language</span>}
        control={
          <select
            value={String(
              typeof keyboardRoot.language === "string"
                ? keyboardRoot.language
                : "es",
            )}
            onChange={(event) =>
              updateBehaviorValue(
                ["keyboard", "language"],
                event.target.value as JsonValue,
              )
            }
          >
            <option value="es">Español</option>
            <option value="en">English</option>
          </select>
        }
      />
      <InspectorFieldRow
        key="keyboardPressedKey"
        className="record-editor-field record-editor-field-string"
        label={<span>Pressed key</span>}
        control={
          <input
            value={String(
              typeof keyboardRoot.pressedKey === "string"
                ? keyboardRoot.pressedKey
                : "",
            )}
            placeholder="a, ñ, intro, backspace…"
            onChange={(event) =>
              updateBehaviorValue(
                ["keyboard", "pressedKey"],
                event.target.value as JsonValue,
              )
            }
          />
        }
      />
    </>
  );
}
