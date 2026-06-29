import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import {
  isJsonObject,
  type JsonPath,
  type JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";

interface TextInputBarBehaviorFieldsProps {
  textInputBarRoot: Record<string, JsonValue>;
  showTextInputBar?: boolean;
  showVisibilityToggle?: boolean;
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
        ? { color: "icons.accent" }
        : {}),
    })) as JsonValue;
}

export function TextInputBarBehaviorFields({
  textInputBarRoot,
  showTextInputBar = true,
  showVisibilityToggle = true,
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
      {showVisibilityToggle ? (
        <InspectorFieldRow
          key="showTextInputBar"
          className="record-editor-field record-editor-field-boolean"
          label={<span>Show text input bar</span>}
          control={
            <input
              type="checkbox"
              checked={showTextInputBar}
              onChange={(event) =>
                updateBehaviorValue(["showTextInputBar"], event.target.checked)
              }
            />
          }
        />
      ) : null}
      {showTextInputBar ? (
        <>
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
      ) : null}
    </>
  );
}
