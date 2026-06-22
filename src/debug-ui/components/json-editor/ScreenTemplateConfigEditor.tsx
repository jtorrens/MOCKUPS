import { useState } from "react";
import type { AppRecord } from "../../api/client.js";
import { friendlyGroupLabel } from "./labels.js";
import { TokenOverrideEditor } from "./TokenOverrideEditor.js";
import { buildJsonUiHints, type JsonUiHints } from "./uiHints.js";
import {
  isJsonObject,
  setAtPath,
  type JsonValue,
} from "./jsonEditorUtils.js";

interface ScreenTemplateConfigEditorProps {
  rootValue: JsonValue;
  record?: Record<string, unknown>;
  records: Record<string, AppRecord[]>;
  section?: "behavior" | "overrides";
  onRootChange: (nextValue: JsonValue) => void;
}

type ModuleConfigField =
  | {
      key: string;
      label: string;
      kind: "boolean";
    }
  | {
      key: string;
      label: string;
      kind: "select";
      options: string[];
    };

const CHAT_CONFIG_FIELDS: ModuleConfigField[] = [
  { key: "showHeader", label: "Show header", kind: "boolean" },
  { key: "showStatusBar", label: "Show status bar", kind: "boolean" },
  { key: "showKeyboard", label: "Show keyboard", kind: "boolean" },
  { key: "debugShowBounds", label: "Show debug bounds", kind: "boolean" },
  {
    key: "initialScroll",
    label: "Initial scroll",
    kind: "select",
    options: ["", "top", "bottom", "keep_latest_visible"],
  },
  {
    key: "messageGrouping",
    label: "Message grouping",
    kind: "select",
    options: ["", "none", "bySender"],
  },
];

function objectAt(value: JsonValue, key: string): Record<string, JsonValue> {
  if (!isJsonObject(value)) return {};
  const child = value[key];
  return isJsonObject(child) ? child : {};
}

function moduleIdForTemplate(record?: Record<string, unknown>): string {
  const configured = record?.module_id ?? record?.module_key;
  if (configured === "core.chat" || configured === "ChatScreen") {
    return "core.chat";
  }
  return String(configured ?? "core.chat");
}

function findTokenCatalog(
  records: Record<string, AppRecord[]>,
  record?: Record<string, unknown>,
): JsonValue {
  const moduleId = moduleIdForTemplate(record);
  const productionId = record?.production_id;
  const config = records.module_theme_configs?.find(
    (candidate) =>
      candidate.module_id === moduleId &&
      (!productionId || candidate.production_id === productionId),
  );
  const tokens = config?.tokens_json;
  return isJsonObject(tokens as JsonValue) ? (tokens as JsonValue) : {};
}

function withoutEmptyObject(rootValue: JsonValue, key: string, value: JsonValue) {
  if (isJsonObject(value) && Object.keys(value).length === 0) {
    if (!isJsonObject(rootValue)) return {};
    const next = { ...rootValue };
    delete next[key];
    return next;
  }
  return setAtPath(rootValue, [key], value);
}

function objectKeys(value: JsonValue): string[] {
  return isJsonObject(value) ? Object.keys(value) : [];
}

function objectGroup(value: JsonValue, key: string): JsonValue {
  return isJsonObject(value) && isJsonObject(value[key]) ? value[key] : {};
}

function hasGroupOverride(value: Record<string, JsonValue>, key: string): boolean {
  return isJsonObject(value[key]) && Object.keys(value[key]).length > 0;
}

function hintsForGroup(hints: JsonUiHints, groupKey: string): JsonUiHints {
  const prefix = `${groupKey}.`;
  return Object.fromEntries(
    Object.entries(hints).flatMap(([path, hint]) => {
      if (path === groupKey) return [["", hint]];
      if (path.startsWith(prefix)) return [[path.slice(prefix.length), hint]];
      return [];
    }),
  );
}

export function ScreenTemplateConfigEditor({
  rootValue,
  record,
  records,
  section,
  onRootChange,
}: ScreenTemplateConfigEditorProps) {
  const [localSection, setLocalSection] = useState<"behavior" | "overrides">(
    "behavior",
  );
  const [overrideGroup, setOverrideGroup] = useState("");
  const activeSection = section ?? localSection;
  const safeRoot = isJsonObject(rootValue) ? rootValue : {};
  const moduleConfig = objectAt(safeRoot, "module_config_json");
  const tokenOverrides = objectAt(safeRoot, "module_tokens_override_json");
  const tokenCatalog = findTokenCatalog(records, record);
  const hints = buildJsonUiHints(
    {
      id: "module_theme_configs",
      label: "Module Theme Configs",
      table: "module_theme_configs",
      titleColumn: "name",
      fields: [],
      jsonFields: ["tokens_json"],
    },
    { column: "tokens_json", label: "Module design tokens", kind: "json" },
    { module_id: moduleIdForTemplate(record), module_schema_version: 1 },
  );

  function setConfigField(key: string, value: JsonValue | undefined) {
    const nextConfig = { ...moduleConfig };
    if (value === undefined || value === "") {
      delete nextConfig[key];
    } else {
      nextConfig[key] = value;
    }
    onRootChange(withoutEmptyObject(safeRoot, "module_config_json", nextConfig));
  }

  function renderBehaviorSection() {
    return (
      <section className="template-editor-section">
        <div className="template-editor-heading">
          <strong>Behavior</strong>
          <span>Empty means the screen instance/module default can decide.</span>
        </div>
        <div className="template-config-grid">
          {CHAT_CONFIG_FIELDS.map((field) => {
            const value = moduleConfig[field.key];
            return (
              <label key={field.key}>
                {field.label}
                {field.kind === "boolean" ? (
                  <select
                    value={
                      typeof value === "boolean" ? String(value) : ""
                    }
                    onChange={(event) => {
                      const raw = event.target.value;
                      setConfigField(
                        field.key,
                        raw === "" ? undefined : raw === "true",
                      );
                    }}
                  >
                    <option value="">Inherit/default</option>
                    <option value="true">true</option>
                    <option value="false">false</option>
                  </select>
                ) : (
                  <select
                    value={typeof value === "string" ? value : ""}
                    onChange={(event) =>
                      setConfigField(field.key, event.target.value)
                    }
                  >
                    {field.options.map((option) => (
                      <option key={option || "inherit"} value={option}>
                        {option || "Inherit/default"}
                      </option>
                    ))}
                  </select>
                )}
              </label>
            );
          })}
        </div>
      </section>
    );
  }

  function renderOverridesSection() {
    const overrideGroups = objectKeys(tokenCatalog);
    const activeOverrideGroup =
      overrideGroup && overrideGroups.includes(overrideGroup)
        ? overrideGroup
        : overrideGroups[0] ?? "";

    function setOverrideGroupValue(nextGroupValue: JsonValue) {
      const nextOverrides = { ...tokenOverrides };
      if (
        isJsonObject(nextGroupValue) &&
        Object.keys(nextGroupValue).length > 0
      ) {
        nextOverrides[activeOverrideGroup] = nextGroupValue;
      } else {
        delete nextOverrides[activeOverrideGroup];
      }
      onRootChange(
        withoutEmptyObject(
          safeRoot,
          "module_tokens_override_json",
          nextOverrides,
        ),
      );
    }

    return (
      <section className="template-editor-section">
        <div className="template-editor-heading">
          <strong>Token bindings / fixed overrides</strong>
          <span>
            Token names stay abstract here. Fill Override only to freeze a fixed
            value for instances inheriting this template.
          </span>
        </div>
        {overrideGroups.length ? (
          <div className="nested-editor-stack template-override-groups">
            <div className="editor-tabs subtle-tabs">
              {overrideGroups.map((group) => (
                <button
                  key={group}
                  type="button"
                  className={`${activeOverrideGroup === group ? "active" : ""} ${
                    hasGroupOverride(tokenOverrides, group) ? "has-warning" : ""
                  }`}
                  onClick={() => setOverrideGroup(group)}
                >
                  {friendlyGroupLabel(group)}
                </button>
              ))}
            </div>
            <TokenOverrideEditor
              rootValue={objectGroup(tokenOverrides, activeOverrideGroup)}
              inheritedRoot={objectGroup(tokenCatalog, activeOverrideGroup)}
              hints={hintsForGroup(hints, activeOverrideGroup)}
              showInheritedValue={false}
              groupContext={activeOverrideGroup}
              onRootChange={setOverrideGroupValue}
            />
          </div>
        ) : (
          <TokenOverrideEditor
            rootValue={tokenOverrides}
            inheritedRoot={tokenCatalog}
            hints={hints}
            showInheritedValue={false}
            onRootChange={(nextOverrides) =>
              onRootChange(
                withoutEmptyObject(
                  safeRoot,
                  "module_tokens_override_json",
                  nextOverrides,
                ),
              )
            }
          />
        )}
      </section>
    );
  }

  return (
    <div className="screen-template-config-editor">
      {section ? null : (
        <div className="editor-tabs subtle-tabs">
          <button
            type="button"
            className={activeSection === "behavior" ? "active" : ""}
            onClick={() => setLocalSection("behavior")}
          >
            Behavior
          </button>
          <button
            type="button"
            className={activeSection === "overrides" ? "active" : ""}
            onClick={() => setLocalSection("overrides")}
          >
            Overrides
          </button>
        </div>
      )}
      {activeSection === "behavior"
        ? renderBehaviorSection()
        : renderOverridesSection()}
    </div>
  );
}
