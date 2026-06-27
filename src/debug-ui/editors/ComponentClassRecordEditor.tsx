import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import {
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import {
  InspectorFieldRow,
} from "../components/inspector/InspectorFieldRow.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { parsedObject } from "./recordJsonUtils.js";

type ComponentClassTab = "" | "general";

interface ComponentClassRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: ComponentClassTab;
  drafts: Record<string, string>;
  paletteCatalog?: PaletteColorCatalog;
  renderField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: ComponentClassTab) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function booleanValue(value: unknown, fallback = false) {
  return typeof value === "boolean" ? value : fallback;
}

function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function borderColorValue(
  tokens: Record<string, JsonValue>,
  mode: "light" | "dark",
) {
  const borderColor = isJsonObject(tokens.borderColor)
    ? tokens.borderColor
    : {};
  return stringValue(
    borderColor[mode],
    mode === "dark" ? "gray_020" : "gray_100",
  );
}

function setTokenValue(
  tokens: Record<string, JsonValue>,
  key: string,
  value: JsonValue,
) {
  return {
    ...tokens,
    [key]: value,
  };
}

function setBorderColorValue(
  tokens: Record<string, JsonValue>,
  mode: "light" | "dark",
  value: string,
) {
  const borderColor = isJsonObject(tokens.borderColor)
    ? tokens.borderColor
    : {};
  return {
    ...tokens,
    borderColor: {
      ...borderColor,
      [mode]: value,
    },
  };
}

function shadowCheckbox({
  checked,
  label,
  onChange,
}: {
  checked: boolean;
  label?: string;
  onChange: (nextValue: boolean) => void;
}) {
  return (
    <label className="json-checkbox">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.currentTarget.checked)}
      />
      {label ?? (checked ? "true" : "false")}
    </label>
  );
}

function iconSetValue(
  tokens: Record<string, JsonValue>,
  zone: "left" | "right",
  state: "idle" | "typing",
) {
  const iconSets = isJsonObject(tokens.iconSets) ? tokens.iconSets : {};
  const zoneSets = isJsonObject(iconSets[zone]) ? iconSets[zone] : {};
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
      if (isJsonObject(item) && typeof item.token === "string") return item.token;
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
        ? { color: "blue" }
        : {}),
    })) as JsonValue;
}

function setIconSetValue(
  tokens: Record<string, JsonValue>,
  zone: "left" | "right",
  state: "idle" | "typing",
  rawValue: string,
) {
  const iconSets = isJsonObject(tokens.iconSets) ? tokens.iconSets : {};
  const zoneSets = isJsonObject(iconSets[zone]) ? iconSets[zone] : {};
  return {
    ...tokens,
    iconSets: {
      ...iconSets,
      [zone]: {
        ...zoneSets,
        [state]: iconSetItems(zone, state, rawValue),
      },
    },
  };
}

function bottomIconValue(
  tokens: Record<string, JsonValue>,
  zone: "left" | "right",
) {
  const rawItems = Array.isArray(tokens.bottomItems)
    ? (tokens.bottomItems as JsonValue[])
    : undefined;
  if (!rawItems) return zone === "left" ? "app_language" : "media_mic";
  return rawItems
    .map((item) => {
      if (typeof item === "string") return item;
      if (
        isJsonObject(item) &&
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

function setBottomIconValue(
  tokens: Record<string, JsonValue>,
  zone: "left" | "right",
  rawValue: string,
) {
  const otherZone = zone === "left" ? "right" : "left";
  const otherTokens = bottomIconValue(tokens, otherZone)
    .split(",")
    .map((token) => token.trim())
    .filter(Boolean);
  const nextTokens = rawValue
    .split(",")
    .map((token) => token.trim())
    .filter(Boolean);
  return {
    ...tokens,
    bottomItems: [
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
    ] as JsonValue,
  };
}

export function ComponentClassRecordEditor({
  table,
  record,
  activeTab,
  drafts,
  paletteCatalog,
  renderField,
  setActiveTab,
  setJsonDraft,
}: ComponentClassRecordEditorProps) {
  const tokens = parsedObject(drafts.tokens_json ?? "{}") as Record<
    string,
    JsonValue
  >;
  const nameField = table.fields.find((field) => field.column === "name");
  const componentType = stringValue(
    record.component_type,
    stringValue(tokens.componentType, "avatar"),
  );

  function updateTokens(nextTokens: Record<string, JsonValue>) {
    setJsonDraft("tokens_json", {
      ...tokens,
      ...nextTokens,
      schemaVersion: numberValue(nextTokens.schemaVersion, 1),
      componentType: stringValue(nextTokens.componentType, componentType),
    });
  }

  return (
    <section className="record-editor">
      <EditorHeader
        eyebrow="Component Class Editor"
        title={String(record[table.titleColumn] ?? record.id)}
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "general"}
            onClick={() =>
              setActiveTab(activeTab === "general" ? "" : "general")
            }
          >
            General
          </EditorSectionButton>
          {activeTab === "general" ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {nameField ? renderField(nameField) : null}
              {componentType === "avatar" ? (
                <>
                  <InspectorFieldRow
                    label="Corner radius"
                    control={
                      <DeferredTextInput
                        ariaLabel="Corner radius"
                        value={String(numberValue(tokens.cornerRadius, 12))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "cornerRadius", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Border width"
                    control={
                      <DeferredTextInput
                        ariaLabel="Border width"
                        value={String(numberValue(tokens.borderWidth, 0))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "borderWidth", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Border color light"
                    control={
                      <ColorValueEditor
                        label="Border color light"
                        value={borderColorValue(tokens, "light")}
                        paletteCatalog={paletteCatalog}
                        onChange={(nextValue) =>
                          updateTokens(
                            setBorderColorValue(tokens, "light", nextValue),
                          )
                        }
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Border color dark"
                    control={
                      <ColorValueEditor
                        label="Border color dark"
                        value={borderColorValue(tokens, "dark")}
                        paletteCatalog={paletteCatalog}
                        onChange={(nextValue) =>
                          updateTokens(
                            setBorderColorValue(tokens, "dark", nextValue),
                          )
                        }
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Shadow enabled"
                    control={
                      <label className="json-checkbox">
                        <input
                          type="checkbox"
                          checked={booleanValue(tokens.shadowEnabled)}
                          onChange={(event) =>
                            updateTokens(
                              setTokenValue(
                                tokens,
                                "shadowEnabled",
                                event.currentTarget.checked,
                              ),
                            )
                          }
                        />
                        {booleanValue(tokens.shadowEnabled) ? "true" : "false"}
                      </label>
                    }
                  />
                  <InspectorFieldRow
                    label="Surface relief"
                    control={
                      <span
                        style={{
                          alignItems: "center",
                          display: "inline-flex",
                          gap: 10,
                        }}
                      >
                        <label className="json-checkbox">
                          <input
                            type="checkbox"
                            checked={booleanValue(
                              tokens.surfaceReliefEnabled,
                              true,
                            )}
                            onChange={(event) =>
                              updateTokens(
                                setTokenValue(
                                  tokens,
                                  "surfaceReliefEnabled",
                                  event.currentTarget.checked,
                                ),
                              )
                            }
                          />
                          {booleanValue(tokens.surfaceReliefEnabled, true)
                            ? "true"
                            : "false"}
                        </label>
                        <button
                          type="button"
                          className="inspector-restore-button"
                          title="Surface relief detail editing will be added later"
                          aria-label="Edit surface relief settings"
                          disabled
                        >
                          ✎
                        </button>
                      </span>
                    }
                  />
                </>
              ) : null}
              {componentType === "button_icon" ? (
                <>
                  <InspectorFieldRow
                    label="Corner radius"
                    control={
                      <DeferredTextInput
                        ariaLabel="Corner radius"
                        value={String(numberValue(tokens.cornerRadius, 0))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "cornerRadius", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Border width"
                    control={
                      <DeferredTextInput
                        ariaLabel="Border width"
                        value={String(numberValue(tokens.borderWidth, 0))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "borderWidth", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Icon padding"
                    control={
                      <DeferredTextInput
                        ariaLabel="Icon padding"
                        value={String(numberValue(tokens.iconPadding, 2))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "iconPadding", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Border theme color"
                    control={
                      <DeferredTextInput
                        ariaLabel="Border theme color"
                        value={stringValue(
                          tokens.borderColorToken,
                          "textSecondary",
                        )}
                        onCommit={(nextValue) =>
                          updateTokens(
                            setTokenValue(
                              tokens,
                              "borderColorToken",
                              nextValue,
                            ),
                          )
                        }
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Shadow enabled"
                    control={shadowCheckbox({
                      checked: booleanValue(tokens.shadowEnabled),
                      onChange: (nextValue) =>
                        updateTokens(
                          setTokenValue(tokens, "shadowEnabled", nextValue),
                        ),
                    })}
                  />
                  <InspectorFieldRow
                    label="Surface relief"
                    control={
                      <span
                        style={{
                          alignItems: "center",
                          display: "inline-flex",
                          gap: 10,
                        }}
                      >
                        {shadowCheckbox({
                          checked: booleanValue(
                            tokens.surfaceReliefEnabled,
                            false,
                          ),
                          onChange: (nextValue) =>
                            updateTokens(
                              setTokenValue(
                                tokens,
                                "surfaceReliefEnabled",
                                nextValue,
                              ),
                            ),
                        })}
                        <button
                          type="button"
                          className="inspector-restore-button"
                          title="Surface relief detail editing will be added later"
                          aria-label="Edit surface relief settings"
                          disabled
                        >
                          ✎
                        </button>
                      </span>
                    }
                  />
                  <InspectorFieldRow
                    label="Show label"
                    control={shadowCheckbox({
                      checked: booleanValue(tokens.labelEnabled),
                      onChange: (nextValue) =>
                        updateTokens(
                          setTokenValue(tokens, "labelEnabled", nextValue),
                        ),
                    })}
                  />
                  <InspectorFieldRow
                    label="Label position"
                    control={
                      <select
                        className="json-value-control"
                        value={stringValue(tokens.labelPosition, "bottom")}
                        onChange={(event) =>
                          updateTokens(
                            setTokenValue(
                              tokens,
                              "labelPosition",
                              event.currentTarget.value,
                            ),
                          )
                        }
                      >
                        <option value="top">Top</option>
                        <option value="bottom">Bottom</option>
                      </select>
                    }
                  />
                  <InspectorFieldRow
                    label="Label padding"
                    control={
                      <DeferredTextInput
                        ariaLabel="Label padding"
                        value={String(numberValue(tokens.labelPadding, 2))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "labelPadding", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Label size"
                    control={
                      <DeferredTextInput
                        ariaLabel="Label size"
                        value={String(numberValue(tokens.labelSize, 10))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "labelSize", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Label theme color"
                    control={
                      <DeferredTextInput
                        ariaLabel="Label theme color"
                        value={stringValue(
                          tokens.labelColorToken,
                          "textSecondary",
                        )}
                        onCommit={(nextValue) =>
                          updateTokens(
                            setTokenValue(tokens, "labelColorToken", nextValue),
                          )
                        }
                      />
                    }
                  />
                </>
              ) : null}
              {componentType === "text_input_bar" ? (
                <>
                  <InspectorFieldRow
                    label="Placeholder"
                    control={
                      <DeferredTextInput
                        ariaLabel="Placeholder"
                        value={stringValue(tokens.placeholder, "Mensaje")}
                        onCommit={(nextValue) =>
                          updateTokens(
                            setTokenValue(tokens, "placeholder", nextValue),
                          )
                        }
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Show cursor"
                    control={
                      <label className="json-checkbox">
                        <input
                          type="checkbox"
                          checked={booleanValue(tokens.cursorVisible, true)}
                          onChange={(event) =>
                            updateTokens(
                              setTokenValue(
                                tokens,
                                "cursorVisible",
                                event.currentTarget.checked,
                              ),
                            )
                          }
                        />
                        {booleanValue(tokens.cursorVisible, true)
                          ? "true"
                          : "false"}
                      </label>
                    }
                  />
                  <InspectorFieldRow
                    label="Idle text color"
                    control={
                      <ColorValueEditor
                        label="Idle text color"
                        value={stringValue(tokens.idleTextColor, "gray_050")}
                        paletteCatalog={paletteCatalog}
                        onChange={(nextValue) =>
                          updateTokens(
                            setTokenValue(tokens, "idleTextColor", nextValue),
                          )
                        }
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Cursor width"
                    control={
                      <DeferredTextInput
                        ariaLabel="Cursor width"
                        value={String(numberValue(tokens.cursorWidth, 2))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "cursorWidth", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Cursor blink speed"
                    control={
                      <DeferredTextInput
                        ariaLabel="Cursor blink speed"
                        value={String(numberValue(tokens.cursorBlinkFrames, 15))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "cursorBlinkFrames", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Cursor color"
                    control={
                      <ColorValueEditor
                        label="Cursor color"
                        value={stringValue(tokens.cursorColor, "blue")}
                        paletteCatalog={paletteCatalog}
                        onChange={(nextValue) =>
                          updateTokens(
                            setTokenValue(tokens, "cursorColor", nextValue),
                          )
                        }
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Input corner radius"
                    control={
                      <DeferredTextInput
                        ariaLabel="Input corner radius"
                        value={String(numberValue(tokens.fieldRadius, 20))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "fieldRadius", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Input shadow"
                    control={
                      <label className="json-checkbox">
                        <input
                          type="checkbox"
                          checked={booleanValue(tokens.fieldShadowEnabled, true)}
                          onChange={(event) =>
                            updateTokens(
                              setTokenValue(
                                tokens,
                                "fieldShadowEnabled",
                                event.currentTarget.checked,
                              ),
                            )
                          }
                        />
                        {booleanValue(tokens.fieldShadowEnabled, true)
                          ? "true"
                          : "false"}
                      </label>
                    }
                  />
                  {(["idle", "typing"] as const).flatMap((state) =>
                    (["left", "right"] as const).map((zone) => (
                      <InspectorFieldRow
                        key={`${zone}-${state}`}
                        label={`${zone === "left" ? "Left" : "Right"} icons ${state}`}
                        control={
                          <DeferredTextInput
                            ariaLabel={`${zone} icons ${state}`}
                            value={iconSetValue(tokens, zone, state)}
                            onCommit={(nextValue) =>
                              updateTokens(
                                setIconSetValue(tokens, zone, state, nextValue),
                              )
                            }
                          />
                        }
                      />
                    )),
                  )}
                </>
              ) : null}
              {componentType === "keyboard" ? (
                <>
                  <InspectorFieldRow
                    label="Keyboard language"
                    control={
                      <select
                        className="json-value-control"
                        value={stringValue(tokens.language, "es")}
                        onChange={(event) =>
                          updateTokens(
                            setTokenValue(
                              tokens,
                              "language",
                              event.currentTarget.value,
                            ),
                          )
                        }
                      >
                        <option value="es">Español</option>
                        <option value="en">English</option>
                      </select>
                    }
                  />
                  <InspectorFieldRow
                    label="Key corner radius"
                    control={
                      <DeferredTextInput
                        ariaLabel="Key corner radius"
                        value={String(numberValue(tokens.keyRadius, 7))}
                        onCommit={(nextValue) => {
                          const parsed = Number(nextValue);
                          if (!Number.isFinite(parsed)) return;
                          updateTokens(
                            setTokenValue(tokens, "keyRadius", parsed),
                          );
                        }}
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Key shadow"
                    control={
                      <label className="json-checkbox">
                        <input
                          type="checkbox"
                          checked={booleanValue(tokens.keyShadowEnabled, true)}
                          onChange={(event) =>
                            updateTokens(
                              setTokenValue(
                                tokens,
                                "keyShadowEnabled",
                                event.currentTarget.checked,
                              ),
                            )
                          }
                        />
                        {booleanValue(tokens.keyShadowEnabled, true)
                          ? "true"
                          : "false"}
                      </label>
                    }
                  />
                  <InspectorFieldRow
                    label="Surface relief"
                    control={
                      <span
                        style={{
                          alignItems: "center",
                          display: "inline-flex",
                          gap: 10,
                        }}
                      >
                        <label className="json-checkbox">
                          <input
                            type="checkbox"
                            checked={booleanValue(
                              tokens.surfaceReliefEnabled,
                              true,
                            )}
                            onChange={(event) =>
                              updateTokens(
                                setTokenValue(
                                  tokens,
                                  "surfaceReliefEnabled",
                                  event.currentTarget.checked,
                                ),
                              )
                            }
                          />
                          {booleanValue(tokens.surfaceReliefEnabled, true)
                            ? "true"
                            : "false"}
                        </label>
                        <button
                          type="button"
                          className="inspector-restore-button"
                          title="Surface relief detail editing will be added later"
                          aria-label="Edit surface relief settings"
                          disabled
                        >
                          ✎
                        </button>
                      </span>
                    }
                  />
                  <InspectorFieldRow
                    label="Bottom left icons"
                    control={
                      <DeferredTextInput
                        ariaLabel="Bottom left icons"
                        value={bottomIconValue(tokens, "left")}
                        onCommit={(nextValue) =>
                          updateTokens(
                            setBottomIconValue(tokens, "left", nextValue),
                          )
                        }
                      />
                    }
                  />
                  <InspectorFieldRow
                    label="Bottom right icons"
                    control={
                      <DeferredTextInput
                        ariaLabel="Bottom right icons"
                        value={bottomIconValue(tokens, "right")}
                        onCommit={(nextValue) =>
                          updateTokens(
                            setBottomIconValue(tokens, "right", nextValue),
                          )
                        }
                      />
                    }
                  />
                </>
              ) : null}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
