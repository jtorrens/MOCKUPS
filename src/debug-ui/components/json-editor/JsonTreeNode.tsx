import { useState } from "react";
import { JsonArrayEditor } from "./JsonArrayEditor.js";
import { JsonObjectEditor } from "./JsonObjectEditor.js";
import { JsonValueEditor } from "./JsonValueEditor.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "../inspector/InspectorFieldRow.js";
import { compactLabelForGroup, groupFromPathSegment } from "./labels.js";
import { hintForPath, type JsonUiHint, type JsonUiHints } from "./uiHints.js";
import {
  isJsonObject,
  deepEqualJson,
  deleteAtPathAndPrune,
  getAtPath,
  hasAtPath,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";
import type { ProductionFontCatalog } from "./productionFonts.js";
import type { PaletteColorCatalog } from "./paletteColors.js";

interface JsonTreeNodeProps {
  rootValue: JsonValue;
  inheritedRoot?: JsonValue;
  path: JsonPath;
  label: string;
  value: JsonValue;
  hints: JsonUiHints;
  restoreStrategy: "remove" | "set";
  allowObjectStructuralEdits: boolean;
  allowArrayStructuralEdits: boolean;
  groupContext?: string;
  productionFontCatalog?: ProductionFontCatalog;
  paletteCatalog?: PaletteColorCatalog;
  onRootChange: (nextValue: JsonValue) => void;
}

function nodeSummary(value: JsonValue): string {
  if (Array.isArray(value)) return `${value.length} items`;
  if (isJsonObject(value)) return `${Object.keys(value).length} keys`;
  if (value === null) return "null";
  return typeof value;
}

function humanizeKey(label: string): string {
  if (/^\[\d+\]$/.test(label)) return label;
  return label
    .replace(/_json$/i, "")
    .replace(/_id$/i, "")
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim()
    .replace(/\s+/g, " ")
    .replace(/^./, (character) => character.toUpperCase());
}

function truncateSummary(text: string): string {
  const normalized = text.replace(/\s+/g, " ").trim();
  return normalized.length > 58 ? `${normalized.slice(0, 55)}…` : normalized;
}

function usefulObjectSummary(
  value: Record<string, JsonValue>,
  hint: JsonUiHint,
): string {
  const preferredKeys = hint.summaryKeys ?? [
    "displayName",
    "text",
    "name",
    "title",
    "label",
    "role",
    "type",
    "id",
  ];
  for (const key of preferredKeys) {
    const candidate = value[key];
    if (typeof candidate === "string" && candidate.trim()) {
      return truncateSummary(candidate);
    }
  }
  return nodeSummary(value);
}

function collapsedSummary(value: JsonValue, hint: JsonUiHint): string {
  if (Array.isArray(value)) return nodeSummary(value);
  if (isJsonObject(value)) return usefulObjectSummary(value, hint);
  if (typeof value === "string") return truncateSummary(value);
  if (value === null) return "null";
  return String(value);
}

function displayLabel(
  hints: JsonUiHints,
  path: JsonPath,
  fallbackLabel: string,
  value: JsonValue,
  groupContext?: string,
): string {
  if (typeof path[path.length - 1] === "number") return fallbackLabel;
  return compactLabelForGroup(
    hintForPath(hints, path, value, groupContext).label ??
      humanizeKey(fallbackLabel),
    groupFromPathSegment(path[0]) ?? groupContext,
  );
}

export function JsonTreeNode({
  rootValue,
  inheritedRoot,
  path,
  label,
  value,
  hints,
  restoreStrategy,
  allowObjectStructuralEdits,
  allowArrayStructuralEdits,
  groupContext,
  productionFontCatalog,
  paletteCatalog,
  onRootChange,
}: JsonTreeNodeProps) {
  const [isOpen, setIsOpen] = useState(path.length < 2);
  const hasInherited = hasAtPath(inheritedRoot, path);
  const inheritedValue = hasInherited ? getAtPath(inheritedRoot!, path) : null;
  const isOverride =
    path.length > 0 && hasInherited && !deepEqualJson(value, inheritedValue);
  const hint = hintForPath(hints, path, value, groupContext);
  const visibleLabel = displayLabel(hints, path, label, value, groupContext);
  const visibleSummary = collapsedSummary(value, hint);

  function restoreInherited() {
    onRootChange(
      restoreStrategy === "set"
        ? setAtPath(rootValue, path, inheritedValue)
        : deleteAtPathAndPrune(rootValue, path),
    );
  }

  if (Array.isArray(value)) {
    return (
      <details
        className={`json-tree-node json-tree-group ${
          isOverride ? "json-override" : ""
        }`}
        open={isOpen}
        onToggle={(event) => setIsOpen(event.currentTarget.open)}
      >
        <summary>
          <span>{visibleLabel}</span>
          {!isOpen ? (
            <strong className="json-collapsed-preview">
              {visibleSummary}
            </strong>
          ) : null}
          <em>
            array · {nodeSummary(value)}
          </em>
        </summary>
        <JsonArrayEditor
          rootValue={rootValue}
          inheritedRoot={inheritedRoot}
          path={path}
          value={value}
          hints={hints}
          restoreStrategy={restoreStrategy}
          allowObjectStructuralEdits={allowObjectStructuralEdits}
          allowArrayStructuralEdits={allowArrayStructuralEdits}
          groupContext={groupContext}
          productionFontCatalog={productionFontCatalog}
          paletteCatalog={paletteCatalog}
          onRootChange={onRootChange}
        />
      </details>
    );
  }

  if (isJsonObject(value)) {
    return (
      <details
        className={`json-tree-node json-tree-group ${
          isOverride ? "json-override" : ""
        }`}
        open={isOpen}
        onToggle={(event) => setIsOpen(event.currentTarget.open)}
      >
        <summary>
          <span>{visibleLabel}</span>
          {!isOpen ? (
            <strong className="json-collapsed-preview">
              {visibleSummary}
            </strong>
          ) : null}
          <em>
            object · {nodeSummary(value)}
          </em>
        </summary>
        <JsonObjectEditor
          rootValue={rootValue}
          inheritedRoot={inheritedRoot}
          path={path}
          value={value}
          hints={hints}
          restoreStrategy={restoreStrategy}
          allowObjectStructuralEdits={allowObjectStructuralEdits}
          allowArrayStructuralEdits={allowArrayStructuralEdits}
          groupContext={groupContext}
          productionFontCatalog={productionFontCatalog}
          paletteCatalog={paletteCatalog}
          onRootChange={onRootChange}
        />
      </details>
    );
  }

  return (
    <InspectorFieldRow
      className={`json-editor-field json-primitive-row ${
        isOverride ? "json-override" : ""
      }`}
      state={isOverride ? "override" : "default"}
      label={<span>{visibleLabel}</span>}
      control={
        <JsonValueEditor
          rootValue={rootValue}
          path={path}
          value={value}
          hints={hints}
          groupContext={groupContext}
          productionFontCatalog={productionFontCatalog}
          paletteCatalog={paletteCatalog}
          onRootChange={onRootChange}
          onChange={(nextValue) =>
            onRootChange(setAtPath(rootValue, path, nextValue))
          }
        />
      }
      restore={
        isOverride ? (
          <InspectorRestoreButton
            label={`Restore ${visibleLabel}`}
            onClick={restoreInherited}
          />
        ) : undefined
      }
    />
  );
}
