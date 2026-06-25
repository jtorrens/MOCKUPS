import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type {
  JsonPath,
  JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";

interface KeyboardBehaviorFieldsProps {
  keyboardRoot: Record<string, JsonValue>;
  showKeyboard?: boolean;
  showVisibilityToggle?: boolean;
  showModeField?: boolean;
  updateBehaviorValue: (path: JsonPath, nextValue: JsonValue) => void;
}

function bottomIconValue(
  keyboardRoot: Record<string, JsonValue>,
  zone: "left" | "right",
) {
  const rawItems = Array.isArray(keyboardRoot.bottomItems)
    ? (keyboardRoot.bottomItems as JsonValue[])
    : undefined;
  if (!rawItems) return zone === "left" ? "app_language" : "media_mic";
  return rawItems
    .map((item) => {
      if (typeof item === "string") return item;
      if (
        item &&
        typeof item === "object" &&
        !Array.isArray(item) &&
        item.zone === zone &&
        typeof item.token === "string"
      ) {
        return item.token;
      }
      return "";
    })
    .filter(Boolean)
    .join(", ");
}

function updateBottomIcons(
  keyboardRoot: Record<string, JsonValue>,
  updateBehaviorValue: (path: JsonPath, nextValue: JsonValue) => void,
  zone: "left" | "right",
  rawValue: string,
) {
  const otherZone = zone === "left" ? "right" : "left";
  const otherTokens = bottomIconValue(keyboardRoot, otherZone)
    .split(",")
    .map((token) => token.trim())
    .filter(Boolean);
  const nextTokens = rawValue
    .split(",")
    .map((token) => token.trim())
    .filter(Boolean);
  const items = [
    ...otherTokens.map((token, index) => ({
      id: token,
      label: token,
      kind: "iconToken",
      token,
      zone: otherZone,
      order: (index + 1) * 10,
    })),
    ...nextTokens.map((token, index) => ({
      id: token,
      label: token,
      kind: "iconToken",
      token,
      zone,
      order: (index + 1) * 10,
    })),
  ].sort((left, right) =>
    left.zone === right.zone
      ? left.order - right.order
      : left.zone.localeCompare(right.zone),
  );
  updateBehaviorValue(["keyboard", "bottomItems"], items as JsonValue);
}

export function KeyboardBehaviorFields({
  keyboardRoot,
  showKeyboard = true,
  showVisibilityToggle = true,
  showModeField = true,
  updateBehaviorValue,
}: KeyboardBehaviorFieldsProps) {
  return (
    <>
      {showVisibilityToggle ? (
        <InspectorFieldRow
          key="showKeyboard"
          className="record-editor-field record-editor-field-boolean"
          label={<span>Show keyboard</span>}
          control={
            <input
              type="checkbox"
              checked={showKeyboard}
              onChange={(event) =>
                updateBehaviorValue(["showKeyboard"], event.target.checked)
              }
            />
          }
        />
      ) : null}
      {showModeField ? (
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
      ) : null}
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
        key="keyboardBottomLeft"
        className="record-editor-field record-editor-field-string"
        label={<span>Bottom left icons</span>}
        control={
          <input
            value={bottomIconValue(keyboardRoot, "left")}
            placeholder="app_language"
            onChange={(event) =>
              updateBottomIcons(
                keyboardRoot,
                updateBehaviorValue,
                "left",
                event.target.value,
              )
            }
          />
        }
      />
      <InspectorFieldRow
        key="keyboardBottomRight"
        className="record-editor-field record-editor-field-string"
        label={<span>Bottom right icons</span>}
        control={
          <input
            value={bottomIconValue(keyboardRoot, "right")}
            placeholder="media_mic"
            onChange={(event) =>
              updateBottomIcons(
                keyboardRoot,
                updateBehaviorValue,
                "right",
                event.target.value,
              )
            }
          />
        }
      />
    </>
  );
}
