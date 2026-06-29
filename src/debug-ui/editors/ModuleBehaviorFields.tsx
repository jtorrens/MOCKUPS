import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import {
  deleteAtPathAndPrune,
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { ModuleBehaviorCard } from "./module-behavior/ModuleBehaviorCard.js";
import { useSessionStoredState } from "../editor-ui/useSessionStoredState.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
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
  const [statusBarOpen, setStatusBarOpen] = useSessionStoredState(
    `${sessionKey}:statusBarOpen`,
    false,
  );
  const [keyboardOpen, setKeyboardOpen] = useSessionStoredState(
    `${sessionKey}:keyboardOpen`,
    false,
  );
  const root = parsedObject(rawValue);
  const rawKeyboard = root.keyboard as JsonValue;
  const keyboardRoot = isJsonObject(rawKeyboard)
    ? (rawKeyboard as Record<string, JsonValue>)
    : {};
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

  function restoreBehaviorValue(path: JsonPath) {
    onRawChange(stringifyJson(deleteAtPathAndPrune(root as JsonValue, path)));
  }

  return (
    <>
      {[
        ["Show header", "showHeader", true],
        ["Show status bar", "showStatusBar", true],
        ["Show navigation bar", "showNavigationBar", true],
        ["Show incoming actor labels", "showIncomingActorLabels", false],
      ].map(([label, key, fallback]) => (
        <InspectorFieldRow
          key={String(key)}
          className={`record-editor-field record-editor-field-boolean ${
            hasOwnValue(root, String(key)) ? "is-override" : "is-inherited"
          }`}
          label={<span>{String(label)}</span>}
          control={
            <div className="module-behavior-override-control">
              <input
                type="checkbox"
                className={hasOwnValue(root, String(key)) ? "is-override" : "is-inherited"}
                checked={Boolean(root[String(key)] ?? fallback)}
                onChange={(event) =>
                  updateBehaviorValue([String(key)], event.target.checked)
                }
              />
              {hasOwnValue(root, String(key)) ? (
                <button
                  type="button"
                  className="field-restore-button"
                  aria-label={`Restore ${String(label)}`}
                  onClick={() => restoreBehaviorValue([String(key)])}
                >
                  ↻
                </button>
              ) : null}
            </div>
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
      <ModuleBehaviorCard
        title="Keyboard"
        summary="Text input visibility, keyboard visibility and pressed effect"
        icon="⌨"
        open={keyboardOpen}
        onToggle={() => setKeyboardOpen((current) => !current)}
      >
        {[
          ["Show text input bar", "showTextInputBar", false],
          ["Show keyboard", "showKeyboard", false],
        ].map(([label, key, fallback]) => (
          <InspectorFieldRow
            key={String(key)}
            className={`record-editor-field record-editor-field-boolean ${
              hasOwnValue(root, String(key)) ? "is-override" : "is-inherited"
            }`}
            label={<span>{String(label)}</span>}
            control={
              <div className="module-behavior-override-control">
                <input
                  type="checkbox"
                  className={hasOwnValue(root, String(key)) ? "is-override" : "is-inherited"}
                  checked={Boolean(root[String(key)] ?? fallback)}
                  onChange={(event) =>
                    updateBehaviorValue([String(key)], event.target.checked)
                  }
                />
                {hasOwnValue(root, String(key)) ? (
                  <button
                    type="button"
                    className="field-restore-button"
                    aria-label={`Restore ${String(label)}`}
                    onClick={() => restoreBehaviorValue([String(key)])}
                  >
                    ↻
                  </button>
                ) : null}
              </div>
            }
          />
        ))}
        <InspectorFieldRow
          key="keyboardPressedEffect"
          className={`record-editor-field record-editor-field-string ${
            hasOwnValue(keyboardRoot, "pressedEffect")
              ? "is-override"
              : "is-inherited"
          }`}
          label={<span>Pressed effect</span>}
          control={
            <div className="module-behavior-override-control">
              <select
                className={
                  hasOwnValue(keyboardRoot, "pressedEffect")
                    ? "is-override"
                    : "is-inherited"
                }
                value={String(keyboardRoot.pressedEffect ?? "popover")}
                onChange={(event) =>
                  updateBehaviorValue(
                    ["keyboard", "pressedEffect"],
                    event.target.value,
                  )
                }
              >
                <option value="popover">Popover</option>
                <option value="inPlace">In place</option>
                <option value="none">None</option>
              </select>
              {hasOwnValue(keyboardRoot, "pressedEffect") ? (
                <button
                  type="button"
                  className="field-restore-button"
                  aria-label="Restore pressed effect"
                  onClick={() =>
                    restoreBehaviorValue(["keyboard", "pressedEffect"])
                  }
                >
                  ↻
                </button>
              ) : null}
            </div>
          }
        />
      </ModuleBehaviorCard>
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
            const enabledState = statusFieldState(
              override,
              "enabled",
              item.zone !== "off",
            );
            const valueState = statusFieldState(
              override,
              "value",
              (item.value ?? "") as JsonValue,
            );
            const chargingState = statusFieldState(
              override,
              "charging",
              item.charging === true,
            );
            const itemPath: JsonPath = ["statusBar", "items", item.id];
            return (
              <div
                className={`module-behavior-status-row ${
                  enabledState.overridden ||
                  valueState.overridden ||
                  chargingState.overridden
                    ? "is-override"
                    : "is-inherited"
                }`}
                key={item.id}
              >
                <div className="module-behavior-status-label">
                  <strong>{item.label}</strong>
                  <small>{item.kind}</small>
                </div>
                <div className="module-behavior-override-control">
                  <input
                    type="checkbox"
                    className={enabledState.overridden ? "is-override" : "is-inherited"}
                    checked={Boolean(enabledState.value)}
                    onChange={(event) =>
                      updateBehaviorValue(
                        [...itemPath, "enabled"],
                        event.target.checked,
                      )
                    }
                  />
                  {enabledState.overridden ? (
                    <button
                      type="button"
                      className="field-restore-button"
                      aria-label={`Restore ${item.label} visibility`}
                      onClick={() => restoreBehaviorValue([...itemPath, "enabled"])}
                    >
                      ↻
                    </button>
                  ) : null}
                </div>
                {item.kind === "text" ? (
                  <div className="module-behavior-override-control">
                    <input
                      className={valueState.overridden ? "is-override" : "is-inherited"}
                      value={String(valueState.value ?? "")}
                      onChange={(event) =>
                        updateBehaviorValue([...itemPath, "value"], event.target.value)
                      }
                    />
                    {valueState.overridden ? (
                      <button
                        type="button"
                        className="field-restore-button"
                        aria-label={`Restore ${item.label} value`}
                        onClick={() => restoreBehaviorValue([...itemPath, "value"])}
                      >
                        ↻
                      </button>
                    ) : null}
                  </div>
                ) : item.kind === "generatedBattery" ||
                  item.kind === "generatedSignal" ? (
                  <div className="module-behavior-generated-value">
                    <div className="module-behavior-override-control">
                      <input
                        type="number"
                        min={0}
                        max={item.kind === "generatedBattery" ? 100 : 4}
                        className={valueState.overridden ? "is-override" : "is-inherited"}
                        value={Number(valueState.value || 0)}
                        onChange={(event) =>
                          updateBehaviorValue(
                            [...itemPath, "value"],
                            Number(event.target.value),
                          )
                        }
                      />
                      {valueState.overridden ? (
                        <button
                          type="button"
                          className="field-restore-button"
                          aria-label={`Restore ${item.label} value`}
                          onClick={() => restoreBehaviorValue([...itemPath, "value"])}
                        >
                          ↻
                        </button>
                      ) : null}
                    </div>
                    {item.kind === "generatedBattery" ? (
                      <label>
                        <input
                          type="checkbox"
                          className={chargingState.overridden ? "is-override" : "is-inherited"}
                          checked={Boolean(chargingState.value)}
                          onChange={(event) =>
                            updateBehaviorValue(
                              [...itemPath, "charging"],
                              event.target.checked,
                            )
                          }
                        />
                        <span>Charging</span>
                        {chargingState.overridden ? (
                          <button
                            type="button"
                            className="field-restore-button"
                            aria-label={`Restore ${item.label} charging`}
                            onClick={() => restoreBehaviorValue([...itemPath, "charging"])}
                          >
                            ↻
                          </button>
                        ) : null}
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
