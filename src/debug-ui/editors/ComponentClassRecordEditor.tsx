import type { ReactNode } from "react";
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
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import type { FieldDefinition, ValueKind } from "../../domain/value-system/index.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
  type DictionarySelectOptions,
} from "../editor-ui/DictionaryFieldControl.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { EditorSubsectionAccordion } from "../editor-ui/EditorSubsectionAccordion.js";
import { useSessionStoredState } from "../editor-ui/useSessionStoredState.js";
import { parsedObject } from "./recordJsonUtils.js";

type ComponentClassTab = "" | "general";

interface ComponentClassRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: ComponentClassTab;
  drafts: Record<string, string>;
  paletteCatalog?: PaletteColorCatalog;
  productionFontCatalog?: ProductionFontCatalog;
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

const THEME_COLOR_TOKEN_OPTIONS = [
  "background",
  "textPrimary",
  "textSecondary",
  "icons.primary",
  "icons.secondary",
  "icons.accent",
  "borders.primary",
  "borders.secondary",
  "borders.alternate",
  "theme.cursor.color",
].map((token) => ({ value: token, label: token }));

const FONT_WEIGHT_OPTIONS = ["100", "200", "300", "400", "500", "600", "700", "800", "900"].map(
  (weight) => ({ value: weight, label: weight }),
);

const FONT_STYLE_OPTIONS = [
  { value: "normal", label: "Normal" },
  { value: "italic", label: "Italic" },
];

function enumSelectOptions(options: readonly string[]): DictionarySelectOptions {
  return {
    options: options.map((option) => ({ value: option, label: option })),
  };
}

function themeColorSelectOptions(): DictionarySelectOptions {
  return { options: THEME_COLOR_TOKEN_OPTIONS };
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
  productionFontCatalog,
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
  const [activeComponentGroup, setActiveComponentGroup] =
    useSessionStoredState(`component_classes:${record.id}:activeGroup`, "");

  function updateTokens(nextTokens: Record<string, JsonValue>) {
    setJsonDraft("tokens_json", {
      ...tokens,
      ...nextTokens,
      schemaVersion: numberValue(nextTokens.schemaVersion, 1),
      componentType: stringValue(nextTokens.componentType, componentType),
    });
  }

  function tokenFieldRow({
    label,
    key,
    kind,
    fallback,
    selectOptions,
    min,
    step,
  }: {
    label: string;
    key: string;
    kind: ValueKind;
    fallback: JsonValue;
    selectOptions?: DictionarySelectOptions;
    min?: number;
    step?: number | "any";
  }) {
    const field: FieldDefinition = {
      id: `component.${componentType}.${key}`,
      kind,
      defaultValue: fallback,
      ui: {
        label,
        ...(min !== undefined ? { min } : {}),
        ...(step !== undefined ? { step } : {}),
      },
    };
    const value = tokens[key] ?? fallback;
    return (
      <InspectorFieldRow
        className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
        label={<span>{label}</span>}
        control={
          <DictionaryFieldControl
            field={field}
            value={value}
            selectOptions={selectOptions}
            productionFontCatalog={productionFontCatalog}
            onChange={(nextValue) =>
              updateTokens(setTokenValue(tokens, key, nextValue as JsonValue))
            }
          />
        }
      />
    );
  }

  function tokenNumberRow(label: string, key: string, fallback: number) {
    return tokenFieldRow({
      label,
      key,
      kind: Number.isInteger(fallback) ? "integer" : "decimal",
      fallback,
      min: 0,
      step: Number.isInteger(fallback) ? 1 : "any",
    });
  }

  function tokenTextRow(label: string, key: string, fallback: string) {
    return tokenFieldRow({ label, key, kind: "text", fallback });
  }

  function tokenCheckboxRow(label: string, key: string, fallback = false) {
    return tokenFieldRow({ label, key, kind: "boolean", fallback });
  }

  function tokenThemeColorRow(label: string, key: string, fallback: string) {
    return tokenFieldRow({
      label,
      key,
      kind: "themeColorToken",
      fallback,
      selectOptions: themeColorSelectOptions(),
    });
  }

  function tokenIconRow(label: string, key: string, fallback: string) {
    return tokenFieldRow({ label, key, kind: "iconToken", fallback });
  }

  function tokenEnumRow(
    label: string,
    key: string,
    fallback: string,
    options: readonly string[],
  ) {
    return tokenFieldRow({
      label,
      key,
      kind: "enum",
      fallback,
      selectOptions: enumSelectOptions(options),
    });
  }

  function tokenOfficialFontRows() {
    return (
      <>
        {tokenFieldRow({
          label: "Font family",
          key: "fontFamily",
          kind: "fontFamily",
          fallback: "SF Pro Text",
        })}
        {tokenFieldRow({
          label: "Font weight",
          key: "fontWeight",
          kind: "fontWeight",
          fallback: 400,
          selectOptions: { options: FONT_WEIGHT_OPTIONS },
        })}
        {tokenFieldRow({
          label: "Font style",
          key: "fontStyle",
          kind: "fontStyle",
          fallback: "normal",
          selectOptions: { options: FONT_STYLE_OPTIONS },
        })}
      </>
    );
  }

  function surfaceReliefRow(fallback = false) {
    const field: FieldDefinition = {
      id: `component.${componentType}.surfaceReliefEnabled`,
      kind: "boolean",
      defaultValue: fallback,
      ui: { label: "Surface relief" },
    };
    return (
      <InspectorFieldRow
        className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
        label={<span>Surface relief</span>}
        control={
          <span
            style={{
              alignItems: "center",
              display: "inline-flex",
              gap: 10,
            }}
          >
            <DictionaryFieldControl
              field={field}
              value={tokens.surfaceReliefEnabled ?? fallback}
              onChange={(nextValue) =>
                updateTokens(
                  setTokenValue(tokens, "surfaceReliefEnabled", nextValue as JsonValue),
                )
              }
            />
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
                      {tokenThemeColorRow(
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
                      {tokenThemeColorRow(
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
                      {tokenEnumRow("Label position", "labelPosition", "bottom", [
                        "top",
                        "bottom",
                      ])}
                      {tokenNumberRow("Label padding", "labelPadding", 2)}
                      {tokenNumberRow("Label size", "labelSize", 10)}
                      {tokenThemeColorRow(
                        "Label theme color",
                        "labelColorToken",
                        "icons.primary",
                      )}
                    </>,
                  )}
                </>
              ) : null}
              {componentType === "label" ? (
                <>
                  {componentAccordion(
                    "layout",
                    <>
                      {tokenEnumRow("Sizing mode", "sizingMode", "content", [
                        "content",
                        "fixed",
                      ])}
                      {tokenNumberRow("Width", "width", 120)}
                      {tokenNumberRow("Height", "height", 28)}
                      {tokenNumberRow("Padding X", "paddingX", 8)}
                      {tokenNumberRow("Padding Y", "paddingY", 4)}
                    </>,
                  )}
                  {componentAccordion(
                    "appearance",
                    <>
                      {tokenCheckboxRow(
                        "Background visible",
                        "backgroundVisible",
                        true,
                      )}
                      {tokenThemeColorRow(
                        "Background theme color",
                        "backgroundColorToken",
                        "background",
                      )}
                      {tokenNumberRow("Corner radius", "cornerRadius", 10)}
                      {tokenNumberRow("Border width", "borderWidth", 0)}
                      {tokenThemeColorRow(
                        "Border theme color",
                        "borderColorToken",
                        "borders.primary",
                      )}
                    </>,
                  )}
                  {componentAccordion(
                    "text",
                    <>
                      {tokenNumberRow("Text size", "fontSize", 12)}
                      {tokenOfficialFontRows()}
                      {tokenThemeColorRow(
                        "Text theme color",
                        "textColorToken",
                        "textPrimary",
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
              {componentType === "audio_message" ? (
                <>
                  {componentAccordion(
                    "container",
                    <>
                      {tokenNumberRow("Width", "width", 260)}
                      {tokenNumberRow("Height", "height", 58)}
                      {tokenNumberRow("Corner radius", "cornerRadius", 18)}
                      {tokenNumberRow("Border width", "borderWidth", 0)}
                      {tokenThemeColorRow(
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
                      {tokenEnumRow("Avatar position", "avatarPosition", "left", [
                        "left",
                        "right",
                      ])}
                      {tokenNumberRow(
                        "Microphone badge size",
                        "microphoneBadgeSize",
                        16,
                      )}
                      {tokenIconRow(
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
                      {tokenThemeColorRow(
                        "Play circle color",
                        "playCircleColorToken",
                        "icons.accent",
                      )}
                      {tokenThemeColorRow(
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
                      {tokenThemeColorRow(
                        "Waveform color",
                        "waveformColorToken",
                        "icons.primary",
                      )}
                      {tokenThemeColorRow(
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
                      {tokenThemeColorRow(
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
                      {tokenThemeColorRow(
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
                      {tokenThemeColorRow(
                        "Play circle color",
                        "playCircleColorToken",
                        "icons.accent",
                      )}
                      {tokenThemeColorRow(
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
                      {tokenIconRow(
                        "Status icon",
                        "statusIconToken",
                        "media_video",
                      )}
                      {tokenThemeColorRow(
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
                      {tokenThemeColorRow(
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
                      {tokenThemeColorRow(
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
                            className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
                            label={
                              <span>{`${zone === "left" ? "Left" : "Right"} icons ${state}`}</span>
                            }
                            control={
                              <DictionaryFieldControl
                                field={{
                                  id: `component.${componentType}.iconSets.${zone}.${state}`,
                                  kind: "text",
                                  defaultValue: iconSetValue(tokens, zone, state),
                                  ui: {
                                    label: `${zone} icons ${state}`,
                                  },
                                }}
                                value={iconSetValue(tokens, zone, state)}
                                onChange={(nextValue) =>
                                  updateTokens(
                                    setIconSetValue(
                                      tokens,
                                      zone,
                                      state,
                                      stringValue(nextValue),
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
                      {tokenEnumRow("Keyboard language", "language", "es", [
                        "es",
                        "en",
                      ])}
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
                      {tokenOfficialFontRows()}
                    </>,
                  )}
                  {componentAccordion(
                    "keys",
                    <>
                      {tokenEnumRow(
                        "Pressed effect",
                        "pressedEffect",
                        "popover",
                        ["popover", "inPlace", "none"],
                      )}
                      {tokenNumberRow("Key corner radius", "keyRadius", 7)}
                      {tokenNumberRow("Key padding", "keyPadding", 6)}
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
                        className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
                        label={<span>Bottom left icons</span>}
                        control={
                          <DictionaryFieldControl
                            field={{
                              id: `component.${componentType}.bottomItems.left`,
                              kind: "text",
                              defaultValue: "app_language",
                              ui: { label: "Bottom left icons" },
                            }}
                            value={bottomIconValue(tokens, "left")}
                            onChange={(nextValue) =>
                              updateTokens(
                                setBottomIconValue(
                                  tokens,
                                  "left",
                                  stringValue(nextValue),
                                ),
                              )
                            }
                          />
                        }
                      />
                      <InspectorFieldRow
                        className={`record-editor-field ${DICTIONARY_FIELD_CLASS}`}
                        label={<span>Bottom right icons</span>}
                        control={
                          <DictionaryFieldControl
                            field={{
                              id: `component.${componentType}.bottomItems.right`,
                              kind: "text",
                              defaultValue: "media_mic",
                              ui: { label: "Bottom right icons" },
                            }}
                            value={bottomIconValue(tokens, "right")}
                            onChange={(nextValue) =>
                              updateTokens(
                                setBottomIconValue(
                                  tokens,
                                  "right",
                                  stringValue(nextValue),
                                ),
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
