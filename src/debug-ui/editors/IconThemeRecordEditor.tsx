import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import {
  deleteIconThemeToken,
  generateIconThemeToken,
  refreshIconThemeSet,
  searchIconThemeSources,
  type AppFieldDefinition,
  type AppRecord,
  type AppState,
  type AppTableDefinition,
  type IconThemeSourceCandidate,
} from "../api/client.js";
import { AppModalDialog } from "../components/AppModalDialog.js";
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
  onAppStateChanged?: (state: AppState, tableId: string, record: AppRecord) => void;
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

function toText(value: unknown) {
  return typeof value === "string" ? value : "";
}

function tokenFromText(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9_]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .replace(/_+/g, "_");
}

function categoryFromToken(token: string) {
  return token.split("_")[0] || "misc";
}

function tokenPath(assetRoot: string, file: string) {
  const trimmedFile = file.trim();
  if (!trimmedFile) return "";
  if (/^(data:|file:|https?:|\/)/i.test(trimmedFile)) return trimmedFile;
  return `${assetRoot.replace(/\/+$/g, "")}/${trimmedFile.replace(/^\/+/g, "")}`;
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

function SourceCandidateButton({
  candidate,
  selected,
  onClick,
}: {
  candidate: IconThemeSourceCandidate;
  selected: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      className={`secondary-button icon-theme-source-candidate ${
        selected ? "is-selected" : ""
      }`}
      onClick={onClick}
    >
      <span className="icon-theme-preview" aria-hidden="true">
        {candidate.previewUrl ? (
          <span
            className="icon-theme-preview-mask"
            style={{
              WebkitMaskImage: `url("${candidate.previewUrl}")`,
              maskImage: `url("${candidate.previewUrl}")`,
            }}
          />
        ) : (
          "—"
        )}
      </span>
      <span>{candidate.sourceName}</span>
    </button>
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
  onAppStateChanged,
}: IconThemeRecordEditorProps) {
  const [pendingDeleteToken, setPendingDeleteToken] = useState("");
  const [deleting, setDeleting] = useState(false);
  const [generatorOpen, setGeneratorOpen] = useState(false);
  const [generatorQuery, setGeneratorQuery] = useState("");
  const [generatorToken, setGeneratorToken] = useState("");
  const [generatorCategory, setGeneratorCategory] = useState("");
  const [generatorDescription, setGeneratorDescription] = useState("");
  const [generatorSearching, setGeneratorSearching] = useState(false);
  const [generatorGenerating, setGeneratorGenerating] = useState(false);
  const [generatorError, setGeneratorError] = useState("");
  const [sourceResults, setSourceResults] = useState<{
    lucide: IconThemeSourceCandidate[];
    material: IconThemeSourceCandidate[];
  }>({ lucide: [], material: [] });
  const [selectedLucide, setSelectedLucide] = useState("");
  const [selectedMaterial, setSelectedMaterial] = useState("");
  const [pendingRefresh, setPendingRefresh] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshMessage, setRefreshMessage] = useState("");
  const [refreshMessageTitle, setRefreshMessageTitle] = useState("Icon theme update complete");
  const mapping = parsedObject(drafts.mapping_json ?? "{}");
  const rows = tokenRows(mapping);
  const assetRoot = String(record.asset_root ?? drafts.asset_root ?? "");
  const generalFields = table.fields.filter(
    (field) =>
      ![
        "id",
        "production_id",
        "asset_root",
        "mapping_json",
        "metadata_json",
      ].includes(field.column),
  );

  async function confirmRefreshSets() {
    setRefreshing(true);
    try {
      const result = await refreshIconThemeSet({ recordId: record.id });
      onAppStateChanged?.(result.state, result.tableId, result.record);
      setRefreshMessageTitle("Refresh complete");
      setRefreshMessage(
        `Refreshed ${result.commonTokenCount} common token${
          result.commonTokenCount === 1 ? "" : "s"
        } across ${result.setCount} set${result.setCount === 1 ? "" : "s"}.${
          result.omittedTokenCount
            ? ` ${result.omittedTokenCount} token${
                result.omittedTokenCount === 1 ? "" : "s"
              } omitted because they are not present in every set.`
            : ""
        }`,
      );
    } catch (error) {
      setRefreshMessageTitle("Refresh failed");
      setRefreshMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setRefreshing(false);
      setPendingRefresh(false);
    }
  }

  async function confirmDeleteToken() {
    const token = pendingDeleteToken;
    if (!token) return;
    setDeleting(true);
    try {
      const result = await deleteIconThemeToken({ recordId: record.id, token });
      onAppStateChanged?.(result.state, result.tableId, result.record);
      setRefreshMessageTitle("Delete complete");
      setRefreshMessage(
        `Deleted “${token}” from ${result.deletedFileCount} icon set${
          result.deletedFileCount === 1 ? "" : "s"
        } and removed it from the mapping.`,
      );
    } catch (error) {
      setRefreshMessageTitle("Delete failed");
      setRefreshMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setDeleting(false);
      setPendingDeleteToken("");
    }
  }

  async function searchSources() {
    const query = generatorQuery.trim();
    if (!query) return;
    setGeneratorSearching(true);
    setGeneratorError("");
    try {
      const result = await searchIconThemeSources({
        recordId: record.id,
        query,
      });
      setSourceResults(result);
      setSelectedLucide(result.lucide[0]?.sourceName ?? "");
      setSelectedMaterial(result.material[0]?.sourceName ?? "");
      const nextToken = generatorToken.trim() || tokenFromText(query);
      setGeneratorToken(nextToken);
      setGeneratorCategory(generatorCategory.trim() || categoryFromToken(nextToken));
    } catch (error) {
      setGeneratorError(error instanceof Error ? error.message : String(error));
    } finally {
      setGeneratorSearching(false);
    }
  }

  async function generateSelectedToken() {
    const token = tokenFromText(generatorToken);
    if (!token) {
      setGeneratorError("Token is required.");
      return;
    }
    setGeneratorGenerating(true);
    setGeneratorError("");
    try {
      const result = await generateIconThemeToken({
        recordId: record.id,
        token,
        category: tokenFromText(generatorCategory) || categoryFromToken(token),
        description: generatorDescription,
        selectedSources: {
          lucide: selectedLucide,
          material: selectedMaterial,
        },
      });
      onAppStateChanged?.(result.state, result.tableId, result.record);
      setRefreshMessageTitle("Generate complete");
      setRefreshMessage(
        `Generated “${result.token}” in ${result.writtenFileCount} set${
          result.writtenFileCount === 1 ? "" : "s"
        }. Refreshed ${result.refreshedThemeCount} icon theme${
          result.refreshedThemeCount === 1 ? "" : "s"
        }; ${result.commonTokenCount} common token${
          result.commonTokenCount === 1 ? "" : "s"
        }.`,
      );
      setGeneratorOpen(false);
    } catch (error) {
      setGeneratorError(error instanceof Error ? error.message : String(error));
    } finally {
      setGeneratorGenerating(false);
    }
  }

  function closeGenerator() {
    if (generatorSearching || generatorGenerating) return;
    setGeneratorOpen(false);
    setGeneratorError("");
  }

  return (
    <section className="record-editor">
      {pendingRefresh ? (
        <AppModalDialog
          eyebrow="Icon theme"
          title="Refresh icon sets?"
          message={
            <>
              This scans every set in the icon-themes directory and rebuilds this
              mapping using only SVG tokens present in all sets.
            </>
          }
          confirmLabel={refreshing ? "Refreshing…" : "Refresh"}
          onCancel={() => {
            if (!refreshing) setPendingRefresh(false);
          }}
          onConfirm={() => void confirmRefreshSets()}
        />
      ) : null}
      {refreshMessage ? (
        <AppModalDialog
          eyebrow="Icon theme"
          title={refreshMessageTitle}
          message={refreshMessage}
          hideConfirm
          cancelLabel="OK"
          onCancel={() => setRefreshMessage("")}
          onConfirm={() => setRefreshMessage("")}
        />
      ) : null}
      {pendingDeleteToken ? (
        <AppModalDialog
          eyebrow="Icon token"
          title={`Delete “${pendingDeleteToken}”?`}
          message="This deletes the SVG from every sibling icon set and removes the token from this mapping."
          confirmLabel={deleting ? "Deleting…" : "Delete"}
          destructive
          onCancel={() => {
            if (!deleting) setPendingDeleteToken("");
          }}
          onConfirm={() => void confirmDeleteToken()}
        />
      ) : null}
      {generatorOpen ? (
        <AppModalDialog
          className="icon-theme-generator-modal"
          eyebrow="Icon themes"
          title="Search / add icon token"
          message={
            <div className="icon-theme-generator">
              <label className="app-modal-form-field">
                <span>Search</span>
                <div className="icon-theme-generator-search-row">
                  <input
                    className="app-modal-input"
                    value={generatorQuery}
                    placeholder="telephone"
                    onChange={(event) => setGeneratorQuery(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") void searchSources();
                    }}
                  />
                  <button
                    type="button"
                    className="secondary-button"
                    onClick={() => void searchSources()}
                  >
                    {generatorSearching ? "Searching…" : "Search"}
                  </button>
                </div>
              </label>
              <div className="icon-theme-generator-results">
                <div>
                  <strong>Lucide</strong>
                  <div className="icon-theme-generator-candidates">
                    {sourceResults.lucide.length ? (
                      sourceResults.lucide.map((candidate) => (
                        <SourceCandidateButton
                          key={candidate.sourceName}
                          candidate={candidate}
                          selected={selectedLucide === candidate.sourceName}
                          onClick={() => setSelectedLucide(candidate.sourceName)}
                        />
                      ))
                    ) : (
                      <span className="empty-panel">No results yet.</span>
                    )}
                  </div>
                </div>
                <div>
                  <strong>Material</strong>
                  <div className="icon-theme-generator-candidates">
                    {sourceResults.material.length ? (
                      sourceResults.material.map((candidate) => (
                        <SourceCandidateButton
                          key={candidate.sourceName}
                          candidate={candidate}
                          selected={selectedMaterial === candidate.sourceName}
                          onClick={() => setSelectedMaterial(candidate.sourceName)}
                        />
                      ))
                    ) : (
                      <span className="empty-panel">No results yet.</span>
                    )}
                  </div>
                </div>
              </div>
              <label className="app-modal-form-field">
                <span>MOCKUPS token</span>
                <input
                  className="app-modal-input"
                  value={generatorToken}
                  placeholder="phone_call"
                  onChange={(event) => {
                    const nextToken = event.target.value;
                    setGeneratorToken(nextToken);
                    if (!generatorCategory.trim()) {
                      setGeneratorCategory(categoryFromToken(tokenFromText(nextToken)));
                    }
                  }}
                />
              </label>
              <label className="app-modal-form-field">
                <span>Category</span>
                <input
                  className="app-modal-input"
                  value={generatorCategory}
                  placeholder="phone"
                  onChange={(event) => setGeneratorCategory(event.target.value)}
                />
              </label>
              <label className="app-modal-form-field">
                <span>Description</span>
                <textarea
                  className="app-modal-input"
                  rows={3}
                  value={generatorDescription}
                  placeholder="Phone call icon"
                  onChange={(event) => setGeneratorDescription(event.target.value)}
                />
              </label>
              {generatorError ? (
                <strong className="editor-field-row-error">{generatorError}</strong>
              ) : null}
            </div>
          }
          confirmLabel={generatorGenerating ? "Generating…" : "Generate"}
          onCancel={closeGenerator}
          onConfirm={() => void generateSelectedToken()}
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
                  onClick={() => setPendingRefresh(true)}
                >
                  Refresh sets
                </button>
                <button
                  type="button"
                  className="secondary-button"
                  onClick={() => setGeneratorOpen(true)}
                >
                  Search / add token
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
    </section>
  );
}
