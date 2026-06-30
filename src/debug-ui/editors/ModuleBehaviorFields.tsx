import { MODULE_INSTANCE_BEHAVIOR_FIELDS } from "../../domain/fields/moduleInstanceBehaviorFields.js";
import type {
  FieldDefinition,
  JsonFieldBinding,
} from "../../domain/value-system/index.js";
import {
  isJsonObject,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { DICTIONARY_FIELD_CLASS, DictionaryFieldControl } from "../editor-ui/DictionaryFieldControl.js";
import { EditorFieldRow } from "../editor-ui/fields/EditorFieldRow.js";
import { createJsonFieldDescriptor } from "../editor-ui/fields/createJsonFieldDescriptor.js";
import { toDictionaryFieldControlProps } from "../editor-ui/fields/EditorFieldDescriptor.js";
import { ModuleBehaviorCard } from "./module-behavior/ModuleBehaviorCard.js";
import { useSessionStoredState } from "../editor-ui/useSessionStoredState.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

function enumSelectOptions(field: FieldDefinition) {
  return field.kind === "enum" && field.ui?.options?.length
    ? {
        options: field.ui.options.map((option) => ({
          value: option,
          label: option,
        })),
      }
    : undefined;
}

interface ModuleBehaviorFieldsProps {
  rawValue: string;
  sessionKey: string;
  statusBarItems?: StatusBarBehaviorItem[];
  onRawChange: (nextRaw: string) => void;
}

export interface StatusBarBehaviorItem {
  id: string;
  label: string;
  kind: string;
  value?: string | number | boolean;
  charging?: boolean;
  zone?: string;
}

function hasOwnValue(object: Record<string, unknown>, key: string) {
  return Object.prototype.hasOwnProperty.call(object, key);
}

function statusFieldState(
  override: Record<string, JsonValue>,
  key: string,
  inheritedValue: JsonValue,
) {
  if (!hasOwnValue(override, key)) {
    return {
      overridden: false,
      value: inheritedValue,
    };
  }
  return {
    overridden: true,
    value: override[key],
  };
}

export function ModuleBehaviorFields({
  rawValue,
  sessionKey,
  statusBarItems = [],
  onRawChange,
}: ModuleBehaviorFieldsProps) {
  const [activeBehaviorCard, setActiveBehaviorCard] = useSessionStoredState(
    `${sessionKey}:activeBehaviorCard`,
    "",
  );
  const root = parsedObject(rawValue);
  const rawStatusBar = root.statusBar as JsonValue;
  const statusBarRoot = isJsonObject(rawStatusBar)
    ? (rawStatusBar as Record<string, JsonValue>)
    : {};
  const statusBarItemOverrides = isJsonObject(statusBarRoot.items)
    ? (statusBarRoot.items as Record<string, JsonValue>)
    : {};

  function behaviorDescriptor({
    field,
    path,
    fallback,
  }: {
    field: FieldDefinition;
    path: JsonPath;
    fallback?: JsonValue;
  }) {
    const binding: JsonFieldBinding = {
      outputPath: path.map(String),
      field,
      fallback,
    };
    return createJsonFieldDescriptor({
      binding,
      localRoot: root as JsonValue,
      parentRoot: {},
      sourceKind: "module-instance-content",
      selectOptions: enumSelectOptions(field),
      onRootChange: (nextRoot) => onRawChange(stringifyJson(nextRoot)),
    });
  }

  function renderBehaviorField({
    field,
    path,
    fallback,
    className = "",
  }: {
    field: FieldDefinition;
    path: JsonPath;
    fallback?: JsonValue;
    className?: string;
  }) {
    const descriptor = behaviorDescriptor({ field, path, fallback });
    if (!descriptor) return null;
    return (
      <EditorFieldRow
        key={path.join(".")}
        className={`record-editor-field ${DICTIONARY_FIELD_CLASS} ${className}`.trim()}
        descriptor={descriptor}
      >
        <DictionaryFieldControl {...toDictionaryFieldControlProps(descriptor)} />
      </EditorFieldRow>
    );
  }

  return (
    <>
      {renderBehaviorField({
        field: MODULE_INSTANCE_BEHAVIOR_FIELDS.showHeader,
        path: ["showHeader"],
        fallback: true,
      })}
      {renderBehaviorField({
        field: MODULE_INSTANCE_BEHAVIOR_FIELDS.showStatusBar,
        path: ["showStatusBar"],
        fallback: true,
      })}
      {renderBehaviorField({
        field: MODULE_INSTANCE_BEHAVIOR_FIELDS.showNavigationBar,
        path: ["showNavigationBar"],
        fallback: true,
      })}
      {renderBehaviorField({
        field: MODULE_INSTANCE_BEHAVIOR_FIELDS.showIncomingActorLabels,
        path: ["showIncomingActorLabels"],
        fallback: false,
      })}
      {renderBehaviorField({
        field: MODULE_INSTANCE_BEHAVIOR_FIELDS.initialScroll,
        path: ["initialScroll"],
        fallback: "bottom",
      })}
      <ModuleBehaviorCard
        title="Keyboard"
        summary="Text input visibility, keyboard visibility and pressed effect"
        icon="⌨"
        open={activeBehaviorCard === "keyboard"}
        onToggle={() =>
          setActiveBehaviorCard((current) =>
            current === "keyboard" ? "" : "keyboard",
          )
        }
      >
        {renderBehaviorField({
          field: MODULE_INSTANCE_BEHAVIOR_FIELDS.showTextInputBar,
          path: ["showTextInputBar"],
          fallback: false,
        })}
        {renderBehaviorField({
          field: MODULE_INSTANCE_BEHAVIOR_FIELDS.showKeyboard,
          path: ["showKeyboard"],
          fallback: false,
        })}
        {renderBehaviorField({
          field: MODULE_INSTANCE_BEHAVIOR_FIELDS.keyboardPressedEffect,
          path: ["keyboard", "pressedEffect"],
          fallback: "popover",
        })}
      </ModuleBehaviorCard>
      {statusBarItems.length ? (
        <ModuleBehaviorCard
          title="Status bar items"
          summary="Runtime item visibility and generated values"
          icon="▥"
          open={activeBehaviorCard === "statusBarItems"}
          onToggle={() =>
            setActiveBehaviorCard((current) =>
              current === "statusBarItems" ? "" : "statusBarItems",
            )
          }
        >
          <section className="module-behavior-status-bar">
          {statusBarItems.map((item) => {
            const rawOverride = statusBarItemOverrides[item.id];
            const override = isJsonObject(rawOverride)
              ? (rawOverride as Record<string, JsonValue>)
              : {};
            const hasOverride = Boolean(
              statusFieldState(override, "enabled", item.zone !== "off").overridden ||
                statusFieldState(override, "value", (item.value ?? "") as JsonValue).overridden ||
                statusFieldState(override, "charging", item.charging === true).overridden,
            );
            const itemPath: JsonPath = ["statusBar", "items", item.id];
            return (
              <div
                className={`module-behavior-status-row ${
                  hasOverride ? "is-override" : "is-inherited"
                }`}
                key={item.id}
              >
                <div className="module-behavior-status-label">
                  <strong>{item.label}</strong>
                </div>
                {renderBehaviorField({
                  field: {
                    ...MODULE_INSTANCE_BEHAVIOR_FIELDS.statusItemEnabled,
                    defaultValue: item.zone !== "off",
                  },
                  path: [...itemPath, "enabled"],
                  fallback: item.zone !== "off",
                  className: "module-behavior-status-field",
                })}
                {item.kind === "text" ? (
                  renderBehaviorField({
                    field: {
                      ...MODULE_INSTANCE_BEHAVIOR_FIELDS.statusItemTextValue,
                      defaultValue: String(item.value ?? ""),
                    },
                    path: [...itemPath, "value"],
                    fallback: String(item.value ?? ""),
                    className: "module-behavior-status-field",
                  })
                ) : item.kind === "generatedBattery" ||
                  item.kind === "generatedSignal" ? (
                  <div className="module-behavior-generated-value">
                    {renderBehaviorField({
                      field:
                        item.kind === "generatedBattery"
                          ? {
                              ...MODULE_INSTANCE_BEHAVIOR_FIELDS.statusItemBatteryValue,
                              defaultValue: Number(item.value ?? 0),
                            }
                          : {
                              ...MODULE_INSTANCE_BEHAVIOR_FIELDS.statusItemSignalValue,
                              defaultValue: Number(item.value ?? 0),
                            },
                      path: [...itemPath, "value"],
                      fallback: Number(item.value ?? 0),
                      className: "module-behavior-status-field",
                    })}
                    {item.kind === "generatedBattery" ? (
                      renderBehaviorField({
                        field: {
                          ...MODULE_INSTANCE_BEHAVIOR_FIELDS.statusItemCharging,
                          defaultValue: item.charging === true,
                        },
                        path: [...itemPath, "charging"],
                        fallback: item.charging === true,
                        className: "module-behavior-status-field",
                      })
                    ) : null}
                  </div>
                ) : (
                  <span className="module-behavior-token-value">Uses status token</span>
                )}
              </div>
            );
          })}
          </section>
        </ModuleBehaviorCard>
      ) : null}
    </>
  );
}
