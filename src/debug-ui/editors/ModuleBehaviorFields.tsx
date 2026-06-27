import { useState } from "react";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import {
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { ModuleBehaviorCard } from "./module-behavior/ModuleBehaviorCard.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

interface ModuleBehaviorFieldsProps {
  rawValue: string;
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

function runtimeValueForItem(
  item: StatusBarBehaviorItem,
  override: Record<string, JsonValue>,
) {
  if ("value" in override) return override.value;
  return item.value ?? "";
}

export function ModuleBehaviorFields({
  rawValue,
  statusBarItems = [],
  onRawChange,
}: ModuleBehaviorFieldsProps) {
  const [statusBarOpen, setStatusBarOpen] = useState(false);
  const root = parsedObject(rawValue);
  const rawStatusBar = root.statusBar as JsonValue;
  const statusBarRoot = isJsonObject(rawStatusBar)
    ? (rawStatusBar as Record<string, JsonValue>)
    : {};
  const statusBarItemOverrides = isJsonObject(statusBarRoot.items)
    ? (statusBarRoot.items as Record<string, JsonValue>)
    : {};

  function updateBehaviorValue(path: JsonPath, nextValue: JsonValue) {
    onRawChange(stringifyJson(setAtPath(root as JsonValue, path, nextValue)));
  }

  return (
    <>
      {[
        ["Show header", "showHeader", true],
        ["Show status bar", "showStatusBar", true],
        ["Show navigation bar", "showNavigationBar", true],
        ["Show text input bar", "showTextInputBar", false],
        ["Show keyboard", "showKeyboard", false],
        ["Show incoming actor labels", "showIncomingActorLabels", false],
      ].map(([label, key, fallback]) => (
        <InspectorFieldRow
          key={String(key)}
          className="record-editor-field record-editor-field-boolean"
          label={<span>{String(label)}</span>}
          control={
            <input
              type="checkbox"
              checked={Boolean(root[String(key)] ?? fallback)}
              onChange={(event) =>
                updateBehaviorValue([String(key)], event.target.checked)
              }
            />
          }
        />
      ))}
      <InspectorFieldRow
        key="initialScroll"
        className="record-editor-field record-editor-field-string"
        label={<span>Initial scroll</span>}
        control={
          <select
            value={String(root.initialScroll ?? "bottom")}
            onChange={(event) =>
              updateBehaviorValue(["initialScroll"], event.target.value)
            }
          >
            <option value="top">Top</option>
            <option value="bottom">Bottom</option>
            <option value="preserve">Preserve</option>
          </select>
        }
      />
      {statusBarItems.length ? (
        <ModuleBehaviorCard
          title="Status bar items"
          summary="Runtime item visibility and generated values"
          icon="▥"
          open={statusBarOpen}
          onToggle={() => setStatusBarOpen((current) => !current)}
        >
          <section className="module-behavior-status-bar">
          <div className="module-behavior-status-header">
            <span>Item</span>
            <span>On</span>
            <span>Value</span>
          </div>
          {statusBarItems.map((item) => {
            const rawOverride = statusBarItemOverrides[item.id];
            const override = isJsonObject(rawOverride)
              ? (rawOverride as Record<string, JsonValue>)
              : {};
            const enabled =
              typeof override.enabled === "boolean"
                ? override.enabled
                : item.zone !== "off";
            const itemPath: JsonPath = ["statusBar", "items", item.id];
            return (
              <div className="module-behavior-status-row" key={item.id}>
                <div className="module-behavior-status-label">
                  <strong>{item.label}</strong>
                  <small>{item.kind}</small>
                </div>
                <input
                  type="checkbox"
                  checked={enabled}
                  onChange={(event) =>
                    updateBehaviorValue(
                      [...itemPath, "enabled"],
                      event.target.checked,
                    )
                  }
                />
                {item.kind === "text" ? (
                  <input
                    value={String(runtimeValueForItem(item, override))}
                    onChange={(event) =>
                      updateBehaviorValue([...itemPath, "value"], event.target.value)
                    }
                  />
                ) : item.kind === "generatedBattery" ||
                  item.kind === "generatedSignal" ? (
                  <div className="module-behavior-generated-value">
                    <input
                      type="number"
                      min={0}
                      max={item.kind === "generatedBattery" ? 100 : 4}
                      value={Number(runtimeValueForItem(item, override) || 0)}
                      onChange={(event) =>
                        updateBehaviorValue(
                          [...itemPath, "value"],
                          Number(event.target.value),
                        )
                      }
                    />
                    {item.kind === "generatedBattery" ? (
                      <label>
                        <input
                          type="checkbox"
                          checked={
                            typeof override.charging === "boolean"
                              ? override.charging
                              : item.charging === true
                          }
                          onChange={(event) =>
                            updateBehaviorValue(
                              [...itemPath, "charging"],
                              event.target.checked,
                            )
                          }
                        />
                        <span>Charging</span>
                      </label>
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
