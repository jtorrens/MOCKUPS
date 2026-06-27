import { useState, type ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
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
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
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
        ? { color: "icons.accent" }
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
  const [activeComponentGroup, setActiveComponentGroup] = useState("");

  function updateTokens(nextTokens: Record<string, JsonValue>) {
    setJsonDraft("tokens_json", {
      ...tokens,
      ...nextTokens,
      schemaVersion: numberValue(nextTokens.schemaVersion, 1),
      componentType: stringValue(nextTokens.componentType, componentType),
    });
  }

  function tokenNumberRow(label: string, key: string, fallback: number) {
    return (
      <InspectorFieldRow
        label={label}
        control={
          <DeferredTextInput
            ariaLabel={label}
            value={String(numberValue(tokens[key], fallback))}
            onCommit={(nextValue) => {
              const parsed = Number(nextValue);
              if (!Number.isFinite(parsed)) return;
              updateTokens(setTokenValue(tokens, key, parsed));
            }}
          />
        }
      />
    );
  }

  function tokenTextRow(label: string, key: string, fallback: string) {
    return (
      <InspectorFieldRow
        label={label}
        control={
          <DeferredTextInput
            ariaLabel={label}
            value={stringValue(tokens[key], fallback)}
            onCommit={(nextValue) =>
              updateTokens(setTokenValue(tokens, key, nextValue))
            }
          />
        }
      />
    );
  }

  function tokenCheckboxRow(label: string, key: string, fallback = false) {
    return (
      <InspectorFieldRow
        label={label}
        control={shadowCheckbox({
          checked: booleanValue(tokens[key], fallback),
          onChange: (nextValue) =>
            updateTokens(setTokenValue(tokens, key, nextValue)),
        })}
      />
    );
  }

  function surfaceReliefRow(fallback = false) {
    return (
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
              checked: booleanValue(tokens.surfaceReliefEnabled, fallback),
              onChange: (nextValue) =>
                updateTokens(
                  setTokenValue(tokens, "surfaceReliefEnabled", nextValue),
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
    );
  }

  function componentAccordion(
    group: string,
    children: ReactNode,
    warning = false,
  ) {
    return (
      <EditorSubsectionAccordion
        group={group}
        activeGroup={activeComponentGroup}
        warning={warning}
        onToggle={setActiveComponentGroup}
      >
        {children}
      </EditorSubsectionAccordion>
    );
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
                  {componentAccordion(
                    "appearance",
                    <>
                      {tokenNumberRow("Corner radius", "cornerRadius", 12)}
                      {tokenNumberRow("Border width", "borderWidth", 0)}
                      {tokenTextRow(
                        "Border theme color",
                        "borderColorToken",
                        "borders.primary",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "effects",
                    <>
                      {tokenCheckboxRow("Shadow enabled", "shadowEnabled")}
                      {surfaceReliefRow(true)}
                    </>,
                  )}
                </>
              ) : null}
              {componentType === "button_icon" ? (
                <>
                  {componentAccordion(
                    "container",
                    <>
                      {tokenNumberRow("Corner radius", "cornerRadius", 0)}
                      {tokenNumberRow("Border width", "borderWidth", 0)}
                      {tokenNumberRow("Icon padding", "iconPadding", 2)}
                      {tokenTextRow(
                        "Border theme color",
                        "borderColorToken",
                        "borders.primary",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "effects",
                    <>
                      {tokenCheckboxRow("Shadow enabled", "shadowEnabled")}
                      {surfaceReliefRow(false)}
                    </>,
                  )}
                  {componentAccordion(
                    "label",
                    <>
                      {tokenCheckboxRow("Show label", "labelEnabled")}
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
                      {tokenNumberRow("Label padding", "labelPadding", 2)}
                      {tokenNumberRow("Label size", "labelSize", 10)}
                      {tokenTextRow(
                        "Label theme color",
                        "labelColorToken",
                        "icons.primary",
                      )}
                    </>,
                  )}
                </>
              ) : null}
              {componentType === "audio_message" ? (
                <>
                  {componentAccordion(
                    "container",
                    <>
                      {tokenNumberRow("Width", "width", 260)}
                      {tokenNumberRow("Height", "height", 58)}
                      {tokenNumberRow("Corner radius", "cornerRadius", 18)}
                      {tokenNumberRow("Border width", "borderWidth", 0)}
                      {tokenTextRow(
                        "Border color",
                        "borderColorToken",
                        "borders.primary",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "avatar",
                    <>
                      {tokenNumberRow("Avatar size", "avatarSize", 38)}
                      {tokenNumberRow("Avatar gap", "avatarGap", 8)}
                      <InspectorFieldRow
                        label="Avatar position"
                        control={
                          <select
                            className="json-value-control"
                            value={stringValue(tokens.avatarPosition, "left")}
                            onChange={(event) =>
                              updateTokens(
                                setTokenValue(
                                  tokens,
                                  "avatarPosition",
                                  event.currentTarget.value,
                                ),
                              )
                            }
                          >
                            <option value="left">Left</option>
                            <option value="right">Right</option>
                          </select>
                        }
                      />
                      {tokenNumberRow(
                        "Microphone badge size",
                        "microphoneBadgeSize",
                        16,
                      )}
                      {tokenTextRow(
                        "Microphone badge icon",
                        "microphoneBadgeIconToken",
                        "media_mic",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "playback",
                    <>
                      {tokenNumberRow("Play circle size", "playCircleSize", 32)}
                      {tokenTextRow(
                        "Play circle color",
                        "playCircleColorToken",
                        "icons.accent",
                      )}
                      {tokenTextRow(
                        "Play icon color",
                        "playIconColorToken",
                        "icons.secondary",
                      )}
                      {tokenNumberRow(
                        "Progress knob size",
                        "progressKnobSize",
                        9,
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "waveform",
                    <>
                      {tokenNumberRow("Waveform bars", "waveformBarCount", 28)}
                      {tokenNumberRow("Waveform gap", "waveformGap", 2)}
                      {tokenNumberRow(
                        "Waveform min height",
                        "waveformMinHeight",
                        4,
                      )}
                      {tokenNumberRow(
                        "Waveform max height",
                        "waveformMaxHeight",
                        22,
                      )}
                      {tokenTextRow(
                        "Waveform color",
                        "waveformColorToken",
                        "icons.primary",
                      )}
                      {tokenTextRow(
                        "Waveform played color",
                        "waveformPlayedColorToken",
                        "icons.accent",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "text",
                    <>
                      {tokenNumberRow("Text size", "textSize", 11)}
                      {tokenTextRow(
                        "Text color",
                        "textColorToken",
                        "icons.secondary",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "effects",
                    <>
                      {tokenCheckboxRow("Shadow", "shadowEnabled")}
                      {tokenTextRow("Shadow token", "shadowToken", "system")}
                      {surfaceReliefRow()}
                    </>,
                  )}
                </>
              ) : null}
              {componentType === "video_message" ? (
                <>
                  {componentAccordion(
                    "container",
                    <>
                      {tokenNumberRow("Corner radius", "cornerRadius", 18)}
                      {tokenNumberRow("Border width", "borderWidth", 0)}
                      {tokenTextRow(
                        "Border color",
                        "borderColorToken",
                        "borders.primary",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "playOverlay",
                    <>
                      {tokenCheckboxRow(
                        "Show play overlay",
                        "playOverlayEnabled",
                        true,
                      )}
                      {tokenNumberRow(
                        "Play circle size",
                        "playCircleSize",
                        44,
                      )}
                      {tokenNumberRow(
                        "Play circle alpha",
                        "playCircleAlpha",
                        0.55,
                      )}
                      {tokenTextRow(
                        "Play circle color",
                        "playCircleColorToken",
                        "icons.accent",
                      )}
                      {tokenTextRow(
                        "Play icon color",
                        "playIconColorToken",
                        "icons.secondary",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "statusBar",
                    <>
                      {tokenCheckboxRow("Show status", "statusVisible", true)}
                      {tokenTextRow(
                        "Status icon",
                        "statusIconToken",
                        "media_video",
                      )}
                      {tokenTextRow(
                        "Status color",
                        "statusColorToken",
                        "icons.secondary",
                      )}
                      {tokenNumberRow("Status size", "statusSize", 12)}
                      {tokenNumberRow(
                        "Status padding X",
                        "statusPaddingX",
                        8,
                      )}
                      {tokenNumberRow(
                        "Status padding Y",
                        "statusPaddingY",
                        6,
                      )}
                      {tokenNumberRow("Status gap", "statusGap", 4)}
                    </>,
                  )}
                  {componentAccordion(
                    "effects",
                    <>
                      {tokenCheckboxRow("Shadow", "shadowEnabled")}
                      {tokenTextRow("Shadow token", "shadowToken", "system")}
                      {surfaceReliefRow()}
                    </>,
                  )}
                </>
              ) : null}
              {componentType === "text_input_bar" ? (
                <>
                  {componentAccordion(
                    "field",
                    <>
                      {tokenTextRow("Placeholder", "placeholder", "Mensaje")}
                      {tokenTextRow(
                        "Idle text color",
                        "idleTextColor",
                        "icons.secondary",
                      )}
                      {tokenNumberRow(
                        "Input corner radius",
                        "fieldRadius",
                        20,
                      )}
                      {tokenCheckboxRow(
                        "Input shadow",
                        "fieldShadowEnabled",
                        true,
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "cursor",
                    <>
                      {tokenCheckboxRow("Show cursor", "cursorVisible", true)}
                      {tokenNumberRow("Cursor width", "cursorWidth", 2)}
                      {tokenNumberRow(
                        "Cursor blink speed",
                        "cursorBlinkFrames",
                        15,
                      )}
                      {tokenTextRow(
                        "Cursor color",
                        "cursorColor",
                        "icons.accent",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "iconSets",
                    <>
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
                                    setIconSetValue(
                                      tokens,
                                      zone,
                                      state,
                                      nextValue,
                                    ),
                                  )
                                }
                              />
                            }
                          />
                        )),
                      )}
                    </>,
                  )}
                </>
              ) : null}
              {componentType === "keyboard" ? (
                <>
                  {componentAccordion(
                    "general",
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
                      {tokenNumberRow(
                        "Push duration frames",
                        "pushDurationFrames",
                        8,
                      )}
                      {tokenNumberRow(
                        "Message gap to text input",
                        "messageGapToTextInput",
                        10,
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "keys",
                    <>
                      {tokenNumberRow("Key corner radius", "keyRadius", 7)}
                      {tokenCheckboxRow(
                        "Key shadow",
                        "keyShadowEnabled",
                        true,
                      )}
                      {surfaceReliefRow(true)}
                    </>,
                  )}
                  {componentAccordion(
                    "bottomIcons",
                    <>
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
                    </>,
                  )}
                </>
              ) : null}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>
    </section>
  );
}
