import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import {
  isJsonObject,
  type JsonPath,
  type JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";

interface TextInputBarBehaviorFieldsProps {
  textInputBarRoot: Record<string, JsonValue>;
  updateBehaviorValue: (path: JsonPath, nextValue: JsonValue) => void;
}

function iconSetValue(
  textInputBarRoot: Record<string, JsonValue>,
  zone: "left" | "right",
  state: "idle" | "typing",
) {
  const iconSets = isJsonObject(textInputBarRoot.iconSets)
    ? (textInputBarRoot.iconSets as Record<string, JsonValue>)
    : {};
  const zoneSets = isJsonObject(iconSets[zone])
    ? (iconSets[zone] as Record<string, JsonValue>)
    : {};
  const rawItems = Array.isArray(zoneSets[state])
    ? (zoneSets[state] as JsonValue[])
    : undefined;
  if (!rawItems) {
    if (zone === "left" && state === "idle") return "chat_emoji, chat_attach";
    if (zone === "left" && state === "typing") return "chat_emoji";
    if (zone === "right" && state === "idle") return "media_camera, media_mic";
    return "chat_send";
  }
  return rawItems
    .map((item) => {
      if (typeof item === "string") return item;
      if (isJsonObject(item) && typeof item.token === "string") {
        return item.token;
      }
      return "";
    })
    .filter(Boolean)
    .join(", ");
}

function iconSetItems(
  zone: "left" | "right",
  state: "idle" | "typing",
  rawValue: string,
) {
  return rawValue
    .split(",")
    .map((token) => token.trim())
    .filter(Boolean)
    .map((token, index) => ({
      token,
      order: (index + 1) * 10,
      ...(zone === "right" && state === "typing" && token === "chat_send"
        ? { color: "#007AFF" }
        : {}),
    })) as JsonValue;
}

export function TextInputBarBehaviorFields({
  textInputBarRoot,
  updateBehaviorValue,
}: TextInputBarBehaviorFieldsProps) {
  const updateIconSet = (
    zone: "left" | "right",
    state: "idle" | "typing",
    rawValue: string,
  ) => {
    updateBehaviorValue(
      ["textInputBar", "iconSets", zone, state],
      iconSetItems(zone, state, rawValue),
    );
  };

  return (
    <>
      <InspectorFieldRow
        key="textInputState"
        className="record-editor-field record-editor-field-string"
        label={<span>Input state</span>}
        control={
          <select
            value={String(
              typeof textInputBarRoot.state === "string"
                ? textInputBarRoot.state
                : "idle",
            )}
            onChange={(event) =>
              updateBehaviorValue(
                ["textInputBar", "state"],
                event.target.value as JsonValue,
              )
            }
          >
            <option value="idle">Idle</option>
            <option value="typing">Typing</option>
          </select>
        }
      />
      <InspectorFieldRow
        key="textInputText"
        className="record-editor-field record-editor-field-string"
        label={<span>Input text</span>}
        control={
          <textarea
            value={String(
              typeof textInputBarRoot.text === "string"
                ? textInputBarRoot.text
                : "",
            )}
            rows={2}
            placeholder="Texto visible en la caja"
            onChange={(event) =>
              updateBehaviorValue(
                ["textInputBar", "text"],
                event.target.value as JsonValue,
              )
            }
          />
        }
      />
      <InspectorFieldRow
        key="textInputPlaceholder"
        className="record-editor-field record-editor-field-string"
        label={<span>Placeholder</span>}
        control={
          <input
            value={String(
              typeof textInputBarRoot.placeholder === "string"
                ? textInputBarRoot.placeholder
                : "Mensaje",
            )}
            onChange={(event) =>
              updateBehaviorValue(
                ["textInputBar", "placeholder"],
                event.target.value as JsonValue,
              )
            }
          />
        }
      />
      <InspectorFieldRow
        key="textInputCursor"
        className="record-editor-field record-editor-field-boolean"
        label={<span>Show cursor</span>}
        control={
          <input
            type="checkbox"
            checked={textInputBarRoot.cursorVisible !== false}
            onChange={(event) =>
              updateBehaviorValue(
                ["textInputBar", "cursorVisible"],
                event.target.checked,
              )
            }
          />
        }
      />
      {(["idle", "typing"] as const).flatMap((state) =>
        (["left", "right"] as const).map((zone) => (
          <InspectorFieldRow
            key={`textInput${zone}${state}`}
            className="record-editor-field record-editor-field-string"
            label={
              <span>
                {zone === "left" ? "Left" : "Right"} icons {state}
              </span>
            }
            control={
              <input
                value={iconSetValue(textInputBarRoot, zone, state)}
                onChange={(event) => updateIconSet(zone, state, event.target.value)}
              />
            }
          />
        )),
      )}
    </>
  );
}
