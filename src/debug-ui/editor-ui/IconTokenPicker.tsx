import { useMemo, useState } from "react";
import { AppModalDialog } from "../components/AppModalDialog.js";
import {
  IconGlyphPreview,
  iconThemeAssetRoot,
  iconThemeName,
  iconThemeTokenEntries,
  type IconThemeLikeRecord,
  type IconThemePreviewBridge,
} from "./IconGlyphPreview.js";

function tokensFromValue(value: unknown) {
  if (Array.isArray(value)) {
    return value
      .map((entry) => String(entry).trim())
      .filter(Boolean);
  }
  return String(value ?? "")
    .split(",")
    .map((entry) => entry.trim())
    .filter(Boolean);
}

function valueFromTokens(tokens: readonly string[], allowMultiple: boolean) {
  return allowMultiple ? tokens.join(", ") : tokens[0] ?? "";
}

export function IconTokenPicker({
  value,
  allowMultiple = false,
  disabled = false,
  iconThemeRecords = [],
  mediaRoot,
  nativeBridge,
  onChange,
}: {
  value: unknown;
  allowMultiple?: boolean;
  disabled?: boolean;
  iconThemeRecords?: readonly IconThemeLikeRecord[];
  mediaRoot?: string;
  nativeBridge?: IconThemePreviewBridge;
  onChange: (nextValue: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [selectedThemeId, setSelectedThemeId] = useState(
    () => String(iconThemeRecords[0]?.id ?? ""),
  );
  const currentTokens = tokensFromValue(value);
  const [draftTokens, setDraftTokens] = useState<string[]>(currentTokens);
  const selectedTheme =
    iconThemeRecords.find((record) => String(record.id ?? "") === selectedThemeId) ??
    iconThemeRecords[0];
  const entries = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    return iconThemeTokenEntries(selectedTheme).filter((entry) => {
      if (!normalizedQuery) return true;
      return (
        entry.token.toLowerCase().includes(normalizedQuery) ||
        (entry.category ?? "").toLowerCase().includes(normalizedQuery)
      );
    });
  }, [query, selectedTheme]);
  const assetRoot = iconThemeAssetRoot(selectedTheme);
  const previewEntry = iconThemeTokenEntries(selectedTheme).find(
    (entry) => entry.token === currentTokens[0],
  );
  const summary = currentTokens.length
    ? currentTokens.join(", ")
    : allowMultiple
      ? "Select icons…"
      : "Select icon…";

  function openModal() {
    setDraftTokens(currentTokens);
    setQuery("");
    setOpen(true);
  }

  function toggleToken(token: string) {
    setDraftTokens((current) => {
      if (!allowMultiple) return [token];
      if (current.includes(token)) {
        return current.filter((entry) => entry !== token);
      }
      return [...current, token];
    });
  }

  return (
    <>
      <button
        type="button"
        className="icon-token-picker-button dictionary-control"
        disabled={disabled || !iconThemeRecords.length}
        title={summary}
        onClick={openModal}
      >
        <IconGlyphPreview
          assetRoot={assetRoot}
          file={previewEntry?.file ?? ""}
          mediaRoot={mediaRoot}
          nativeBridge={nativeBridge}
        />
        <span>{summary}</span>
      </button>
      {open ? (
        <AppModalDialog
          title={allowMultiple ? "Select icon tokens" : "Select icon token"}
          eyebrow="Icon token"
          confirmLabel="Apply"
          cancelLabel="Cancel"
          className="icon-token-picker-modal"
          message={
            <div className="icon-token-picker">
              <div className="icon-token-picker-toolbar">
                <label>
                  <span>Set</span>
                  <select
                    className="json-value-control dictionary-control"
                    value={String(selectedTheme?.id ?? "")}
                    onChange={(event) => setSelectedThemeId(event.currentTarget.value)}
                  >
                    {iconThemeRecords.map((record) => (
                      <option key={String(record.id ?? "")} value={String(record.id ?? "")}>
                        {iconThemeName(record)}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Search</span>
                  <input
                    className="json-value-control dictionary-control"
                    value={query}
                    placeholder="Filter icons…"
                    onChange={(event) => setQuery(event.currentTarget.value)}
                  />
                </label>
              </div>
              <div className="icon-token-picker-selection">
                {draftTokens.length
                  ? draftTokens.map((token, index) => (
                      <span key={`${token}-${index}`}>{token}</span>
                    ))
                  : "No icon selected"}
              </div>
              <div className="icon-token-picker-grid">
                {entries.map((entry) => {
                  const selected = draftTokens.includes(entry.token);
                  return (
                    <button
                      key={entry.token}
                      type="button"
                      className={`icon-token-picker-option ${
                        selected ? "is-selected" : ""
                      }`}
                      title={entry.token}
                      onClick={() => toggleToken(entry.token)}
                    >
                      <IconGlyphPreview
                        assetRoot={assetRoot}
                        file={entry.file}
                        mediaRoot={mediaRoot}
                        nativeBridge={nativeBridge}
                      />
                      <span>{entry.token}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          }
          onCancel={() => setOpen(false)}
          onConfirm={() => {
            onChange(valueFromTokens(draftTokens, allowMultiple));
            setOpen(false);
          }}
        />
      ) : null}
    </>
  );
}
