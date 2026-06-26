import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import type { AppFieldDefinition, AppRecord, AppTableDefinition } from "../api/client.js";
import { AppModalDialog } from "../components/AppModalDialog.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import { EditorHeader } from "../editor-ui/EditorHeader.js";
import { EditorSectionButton } from "../editor-ui/EditorSectionButton.js";
import { EditorSectionCard } from "../editor-ui/EditorSectionCard.js";
import { EditorSections } from "../editor-ui/EditorSections.js";
import { parsedObject } from "./recordJsonUtils.js";

type IconThemeTab = "" | "general" | "tokens";

interface IconThemeNativeBridge {
  pickFile?: () => Promise<string[]>;
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

interface IconThemeRecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord;
  activeTab: IconThemeTab;
  drafts: Record<string, string>;
  mediaRoot: string;
  nativeBridge: IconThemeNativeBridge | undefined;
  renderField: (field: AppFieldDefinition) => ReactNode;
  setActiveTab: (tab: IconThemeTab) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
}

interface IconThemeTokenRow {
  category?: string;
  file?: string;
  materialName?: string;
  source?: string;
}

function mappingTokens(mapping: Record<string, unknown>) {
  return mapping.tokens &&
    typeof mapping.tokens === "object" &&
    !Array.isArray(mapping.tokens)
    ? (mapping.tokens as Record<string, IconThemeTokenRow>)
    : {};
}

function tokenRows(mapping: Record<string, unknown>) {
  return Object.entries(mappingTokens(mapping)).sort(([left], [right]) =>
    left.localeCompare(right, undefined, {
      numeric: true,
      sensitivity: "base",
    }),
  );
}

function normalizeCategory(value: string) {
  return value.trim().toLowerCase().replace(/[^a-z0-9]+/g, "_");
}

function basename(value: string) {
  return value.replace(/\\/g, "/").split("/").at(-1) ?? value;
}

function withoutSvgExtension(value: string) {
  return value.replace(/\.svg$/i, "");
}

function toText(value: unknown) {
  return typeof value === "string" ? value : "";
}

function tokenPath(assetRoot: string, file: string) {
  const trimmedFile = file.trim();
  if (!trimmedFile) return "";
  if (/^(data:|file:|https?:|\/)/i.test(trimmedFile)) return trimmedFile;
  return `${assetRoot.replace(/\/+$/g, "")}/${trimmedFile.replace(/^\/+/g, "")}`;
}

function addToken(
  mapping: Record<string, unknown>,
  token: string,
  category: string,
  file: string,
) {
  const tokens = mappingTokens(mapping);
  const nextCategory = normalizeCategory(category) || "misc";
  const nextToken = token.trim();
  const nextCategories =
    mapping.categories &&
    typeof mapping.categories === "object" &&
    !Array.isArray(mapping.categories)
      ? (mapping.categories as Record<string, JsonValue>)
      : {};
  const currentCategoryTokens = Array.isArray(nextCategories[nextCategory])
    ? (nextCategories[nextCategory] as JsonValue[]).filter(
        (value): value is string => typeof value === "string",
      )
    : [];

  return {
    ...mapping,
    tokens: {
      ...tokens,
      [nextToken]: {
        category: nextCategory,
        file,
      },
    },
    categories: {
      ...nextCategories,
      [nextCategory]: Array.from(new Set([...currentCategoryTokens, nextToken])),
    },
  } as JsonValue;
}

function deleteToken(mapping: Record<string, unknown>, token: string) {
  const tokens = { ...mappingTokens(mapping) };
  delete tokens[token];
  const categories =
    mapping.categories &&
    typeof mapping.categories === "object" &&
    !Array.isArray(mapping.categories)
      ? (mapping.categories as Record<string, JsonValue>)
      : {};
  const nextCategories = Object.fromEntries(
    Object.entries(categories).map(([category, values]) => [
      category,
      Array.isArray(values)
        ? values.filter((value) => value !== token)
        : values,
    ]),
  );
  return {
    ...mapping,
    tokens,
    categories: nextCategories,
  } as unknown as JsonValue;
}

function IconPreview({
  assetRoot,
  file,
  mediaRoot,
  nativeBridge,
}: {
  assetRoot: string;
  file: string;
  mediaRoot: string;
  nativeBridge: IconThemeNativeBridge | undefined;
}) {
  const [url, setUrl] = useState("");
  const iconPath = tokenPath(assetRoot, file);

  useEffect(() => {
    let cancelled = false;
    setUrl("");
    if (!iconPath) return () => undefined;
    const loader = nativeBridge?.mediaDataUrl;
    if (!loader) {
      setUrl(iconPath.startsWith("/") ? `file://${encodeURI(iconPath)}` : iconPath);
      return () => undefined;
    }
    void loader(iconPath, mediaRoot)
      .then((nextUrl) => {
        if (!cancelled) setUrl(nextUrl || "");
      })
      .catch(() => {
        if (!cancelled) setUrl("");
      });
    return () => {
      cancelled = true;
    };
  }, [iconPath, mediaRoot, nativeBridge]);

  return (
    <span className="icon-theme-preview" aria-hidden="true">
      {url ? (
        <span
          className="icon-theme-preview-mask"
          style={{
            WebkitMaskImage: `url("${url}")`,
            maskImage: `url("${url}")`,
          }}
        />
      ) : (
        "—"
      )}
    </span>
  );
}

function AddIconTokenModal({
  canBrowse,
  error,
  file,
  family,
  token,
  onBrowse,
  onCancel,
  onSave,
  setFamily,
  setToken,
}: {
  canBrowse: boolean;
  error: string;
  file: string;
  family: string;
  token: string;
  onBrowse: () => void;
  onCancel: () => void;
  onSave: () => void;
  setFamily: (value: string) => void;
  setToken: (value: string) => void;
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <div className="modal-card icon-theme-modal" role="dialog" aria-modal="true">
        <header>
          <h2>Add icon token</h2>
          <p>Choose an SVG and assign the logical token used by apps/modules.</p>
        </header>
        <div className="record-editor-field-stack record-editor-direct-fields">
          <label className="icon-theme-modal-field">
            <span>Token name</span>
            <DeferredTextInput value={token} onCommit={setToken} />
          </label>
          <label className="icon-theme-modal-field">
            <span>Family / group</span>
            <DeferredTextInput value={family} onCommit={setFamily} />
          </label>
          <div className="icon-theme-modal-file">
            <span>SVG</span>
            <strong>{file ? basename(file) : "No SVG selected"}</strong>
            <button
              type="button"
              className="secondary-button"
              disabled={!canBrowse}
              onClick={onBrowse}
            >
              Browse SVG
            </button>
          </div>
          {error ? <p className="form-error">{error}</p> : null}
        </div>
        <footer>
          <button type="button" className="secondary-button" onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="primary-button" onClick={onSave}>
            Add token
          </button>
        </footer>
      </div>
    </div>
  );
}

export function IconThemeRecordEditor({
  table,
  record,
  activeTab,
  drafts,
  mediaRoot,
  nativeBridge,
  renderField,
  setActiveTab,
  setJsonDraft,
}: IconThemeRecordEditorProps) {
  const [adding, setAdding] = useState(false);
  const [newToken, setNewToken] = useState("");
  const [newFamily, setNewFamily] = useState("");
  const [newFile, setNewFile] = useState("");
  const [modalError, setModalError] = useState("");
  const [pendingDeleteToken, setPendingDeleteToken] = useState("");
  const mapping = parsedObject(drafts.mapping_json ?? "{}");
  const rows = tokenRows(mapping);
  const assetRoot = String(record.asset_root ?? drafts.asset_root ?? "");
  const generalFields = table.fields.filter(
    (field) =>
      !["id", "production_id", "mapping_json", "metadata_json"].includes(field.column),
  );

  async function browseNewSvg() {
    const [selectedPath] = await (nativeBridge?.pickFile?.() ?? Promise.resolve([]));
    if (!selectedPath) return;
    setNewFile(selectedPath);
    if (!newToken.trim()) {
      setNewToken(withoutSvgExtension(basename(selectedPath)));
    }
  }

  function resetModal() {
    setNewToken("");
    setNewFamily("");
    setNewFile("");
    setModalError("");
  }

  function saveNewToken() {
    const token = newToken.trim();
    const file = basename(newFile.trim());
    if (!token) {
      setModalError("Token name is required.");
      return;
    }
    if (!file || !file.toLowerCase().endsWith(".svg")) {
      setModalError("Choose an SVG file.");
      return;
    }
    if (Object.hasOwn(mappingTokens(mapping), token)) {
      setModalError(`Token “${token}” already exists.`);
      return;
    }
    setJsonDraft("mapping_json", addToken(mapping, token, newFamily, file));
    resetModal();
    setAdding(false);
  }

  return (
    <section className="record-editor">
      {pendingDeleteToken ? (
        <AppModalDialog
          eyebrow="Icon token"
          title={`Delete “${pendingDeleteToken}”?`}
          message="This removes the token from this icon theme."
          confirmLabel="Delete"
          destructive
          onCancel={() => setPendingDeleteToken("")}
          onConfirm={() => {
            setJsonDraft("mapping_json", deleteToken(mapping, pendingDeleteToken));
            setPendingDeleteToken("");
          }}
        />
      ) : null}
      <EditorHeader
        eyebrow="Icon Theme Editor"
        title={String(record[table.titleColumn] ?? record.id)}
        summary={`${rows.length} icon token${rows.length === 1 ? "" : "s"}`}
      />
      <EditorSections>
        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "general"}
            onClick={() => setActiveTab(activeTab === "general" ? "" : "general")}
          >
            General
          </EditorSectionButton>
          {activeTab === "general" ? (
            <div className="editor-section-body record-editor-field-stack record-editor-direct-fields">
              {generalFields.map((field) => renderField(field))}
            </div>
          ) : null}
        </EditorSectionCard>

        <EditorSectionCard>
          <EditorSectionButton
            active={activeTab === "tokens"}
            onClick={() => setActiveTab(activeTab === "tokens" ? "" : "tokens")}
          >
            Icon tokens
          </EditorSectionButton>
          {activeTab === "tokens" ? (
            <div className="editor-section-body icon-theme-token-editor">
              <div className="icon-theme-token-toolbar">
                <span>{rows.length} tokens</span>
                <button
                  type="button"
                  className="primary-button"
                  onClick={() => {
                    resetModal();
                    setAdding(true);
                  }}
                >
                  Add icon
                </button>
              </div>
              {rows.length === 0 ? (
                <p className="empty-panel">No icon tokens yet.</p>
              ) : (
                <div className="icon-theme-token-table" role="table">
                  <div className="icon-theme-token-header" role="row">
                    <span>Icon</span>
                    <span>Token</span>
                    <span>Family</span>
                    <span>Source</span>
                    <span>Actions</span>
                  </div>
                  {rows.map(([token, value]) => (
                    <div className="icon-theme-token-row" role="row" key={token}>
                      <span>
                        <IconPreview
                          assetRoot={assetRoot}
                          file={toText(value.file)}
                          mediaRoot={mediaRoot}
                          nativeBridge={nativeBridge}
                        />
                      </span>
                      <strong>{token}</strong>
                      <span>{toText(value.category) || "misc"}</span>
                      <span>{toText(value.materialName) || toText(value.source) || "—"}</span>
                      <span>
                        <button
                          type="button"
                          className="secondary-button icon-theme-delete-button"
                          onClick={() => setPendingDeleteToken(token)}
                        >
                          Delete
                        </button>
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          ) : null}
        </EditorSectionCard>
      </EditorSections>

      {adding ? (
        <AddIconTokenModal
          canBrowse={Boolean(nativeBridge?.pickFile)}
          error={modalError}
          file={newFile}
          family={newFamily}
          token={newToken}
          onBrowse={() => void browseNewSvg()}
          onCancel={() => {
            resetModal();
            setAdding(false);
          }}
          onSave={saveNewToken}
          setFamily={setNewFamily}
          setToken={setNewToken}
        />
      ) : null}
    </section>
  );
}
