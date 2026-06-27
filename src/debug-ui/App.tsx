import { useEffect, useMemo, useState, type PointerEvent as ReactPointerEvent } from "react";
import {
  createAppRecord,
  deleteAppRecord,
  duplicateAppRecord,
  getAppState,
  getPreviewPayload,
  moveScreenInstance as moveScreenInstanceRecord,
  type AppRecord,
  type AppState,
  type DebugPayload,
  type DebugSelection,
} from "./api/client.js";
import { ProjectTree } from "./components/ProjectTree.js";
import { RecordEditor } from "./components/RecordEditor.js";
import {
  paletteTokenUsageCount,
  paletteTokenUsages,
  type PaletteTokenUsage,
} from "./editors/paletteUsage.js";
import { AppModalDialog } from "./components/AppModalDialog.js";
import { RightPreviewShell } from "./preview/index.js";
import "./AppShell.css";
import "./panels/LeftPanel.css";

const LAYOUT_STORAGE_KEY = "mockups.debugUi.layout.v1";
const UI_THEME_STORAGE_KEY = "mockups.debugUi.theme.v1";

type UiThemeMode = "light" | "dark";

interface PaletteDeleteBlocker {
  token: string;
  usages: PaletteTokenUsage[];
}

interface PendingPaletteDelete {
  tableId: "palette_colors";
  recordId: string;
  token: string;
}

interface StoredLayout {
  navigationWidth?: number;
  authoringWidth?: number;
}

function readStoredLayout(): StoredLayout {
  try {
    const raw = window.localStorage.getItem(LAYOUT_STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as StoredLayout;
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

function readStoredUiTheme(): UiThemeMode {
  try {
    const value = window.localStorage.getItem(UI_THEME_STORAGE_KEY);
    return value === "dark" ? "dark" : "light";
  } catch {
    return "light";
  }
}

function storedPanelWidth(
  key: keyof StoredLayout,
  fallback: number,
  min: number,
  max: number,
) {
  const value = readStoredLayout()[key];
  if (typeof value !== "number" || !Number.isFinite(value)) return fallback;
  return Math.min(max, Math.max(min, value));
}

function chooseInitialSelection(state: AppState): DebugSelection {
  const production = state.options.productions[0];
  const episode = state.options.episodes.find(
    (candidate) => candidate.productionId === production?.id,
  );
  const shot = state.options.shots.find(
    (candidate) =>
      candidate.episodeId === episode?.id ||
      candidate.productionId === production?.id,
  );
  const screen = choosePreviewScreenForShot(state, shot?.id);
  if (!production || !shot || !screen) {
    throw new Error("Development database has no selectable screen instance");
  }
  return {
    productionId: production.id,
    shotId: shot.id,
    screenInstanceId: screen.id,
    frame: Math.max(screen.startFrame, Math.min(210, screen.endFrame - 1)),
  };
}

function choosePreviewScreenForShot(
  state: AppState,
  shotId: string | undefined,
) {
  return (
    state.options.screenInstances.find(
      (candidate) => candidate.shotId === shotId && candidate.moduleId === "core.chat",
    ) ??
    state.options.screenInstances.find((candidate) => candidate.shotId === shotId)
  );
}

function previewFrameForScreen(
  screen: NonNullable<ReturnType<typeof choosePreviewScreenForShot>>,
  fallbackFrame = 210,
) {
  return Math.max(
    screen.startFrame,
    Math.min(fallbackFrame, screen.endFrame - 1),
  );
}

function initialSelectedRecords(state: AppState) {
  return Object.fromEntries(
    state.tables.map((table) => [
      table.id,
      state.records[table.id]?.[0]?.id ?? "",
    ]),
  );
}

export function App() {
  const [state, setState] = useState<AppState | null>(null);
  const [activeTableId, setActiveTableId] = useState("productions");
  const [selectedRecordIds, setSelectedRecordIds] = useState<
    Record<string, string>
  >({});
  const [selection, setSelection] = useState<DebugSelection | null>(null);
  const [preview, setPreview] = useState<DebugPayload | null>(null);
  const [previewError, setPreviewError] = useState("");
  const [busyPreview, setBusyPreview] = useState(false);
  const [busyProjectAction, setBusyProjectAction] = useState(false);
  const [requestError, setRequestError] = useState("");
  const [refreshCounter, setRefreshCounter] = useState(0);
  const [isProductionModalOpen, setProductionModalOpen] = useState(false);
  const [paletteDeleteBlocker, setPaletteDeleteBlocker] =
    useState<PaletteDeleteBlocker | null>(null);
  const [pendingPaletteDelete, setPendingPaletteDelete] =
    useState<PendingPaletteDelete | null>(null);
  const [themeCreateParent, setThemeCreateParent] = useState<{
    productionId?: string;
  } | null>(null);
  const [navigationWidth, setNavigationWidth] = useState(() =>
    storedPanelWidth("navigationWidth", 380, 280, 720),
  );
  const [authoringWidth, setAuthoringWidth] = useState(() =>
    storedPanelWidth("authoringWidth", 1040, 720, 1800),
  );
  const [uiTheme, setUiTheme] = useState<UiThemeMode>(() => readStoredUiTheme());

  useEffect(() => {
    void getAppState()
      .then((value) => {
        setState(value);
        setSelectedRecordIds(initialSelectedRecords(value));
        setSelection(chooseInitialSelection(value));
      })
      .catch((error: Error) => setRequestError(error.message));
  }, []);

  useEffect(() => {
    if (!selection) return;
    setBusyPreview(true);
    void getPreviewPayload(selection)
      .then((payload) => {
        setPreview(payload);
        setPreviewError("");
      })
      .catch((error: Error) => setPreviewError(error.message))
      .finally(() => setBusyPreview(false));
  }, [selection, refreshCounter]);

  useEffect(() => {
    window.localStorage.setItem(
      LAYOUT_STORAGE_KEY,
      JSON.stringify({ navigationWidth, authoringWidth }),
    );
  }, [authoringWidth, navigationWidth]);

  useEffect(() => {
    document.documentElement.dataset.uiTheme = uiTheme;
    window.localStorage.setItem(UI_THEME_STORAGE_KEY, uiTheme);
  }, [uiTheme]);

  const activeTable = state?.tables.find((table) => table.id === activeTableId);
  const selectedRecord = useMemo(() => {
    if (!state || !activeTable) return undefined;
    const selectedId = selectedRecordIds[activeTable.id];
    return state.records[activeTable.id]?.find(
      (record) => record.id === selectedId,
    );
  }, [activeTable, selectedRecordIds, state]);

  function selectContextFromRecord(tableId: string, recordId: string) {
    if (!state || !selection) return;
    if (tableId === "productions") {
      const episode = state.options.episodes.find(
        (candidate) => candidate.productionId === recordId,
      );
      const shot = state.options.shots.find(
        (candidate) =>
          candidate.episodeId === episode?.id ||
          candidate.productionId === recordId,
      );
      const instance = choosePreviewScreenForShot(state, shot?.id);
      if (shot && instance) {
        setSelection({
          productionId: recordId,
          shotId: shot.id,
          screenInstanceId: instance.id,
          frame: previewFrameForScreen(instance),
        });
      }
    }
    if (tableId === "episodes") {
      const episode = state.options.episodes.find(
        (candidate) => candidate.id === recordId,
      );
      const shot = state.options.shots.find(
        (candidate) => candidate.episodeId === recordId,
      );
      const instance = choosePreviewScreenForShot(state, shot?.id);
      if (episode && shot && instance) {
        setSelection({
          productionId: episode.productionId,
          shotId: shot.id,
          screenInstanceId: instance.id,
          frame: previewFrameForScreen(instance),
        });
      }
    }
    if (tableId === "shots") {
      const shot = state.options.shots.find(
        (candidate) => candidate.id === recordId,
      );
      const instance = choosePreviewScreenForShot(state, recordId);
      if (shot && instance) {
        setSelection({
          productionId: shot.productionId,
          shotId: recordId,
          screenInstanceId: instance.id,
          frame: Math.max(instance.startFrame, Math.min(selection.frame, instance.endFrame - 1)),
        });
      }
    }
    if (tableId === "screen_instances") {
      const instance = state.options.screenInstances.find(
        (candidate) => candidate.id === recordId,
      );
      const shot = state.options.shots.find(
        (candidate) => candidate.id === instance?.shotId,
      );
      if (instance && shot) {
        setSelection({
          productionId: shot.productionId,
          shotId: shot.id,
          screenInstanceId: instance.id,
          frame: Math.max(instance.startFrame, Math.min(selection.frame, instance.endFrame - 1)),
        });
      }
    }
    if (tableId === "module_instances") {
      const moduleInstance = state.records.module_instances?.find(
        (candidate) => candidate.id === recordId,
      );
      const instance = state.options.screenInstances.find(
        (candidate) => candidate.id === moduleInstance?.screen_instance_id,
      );
      const shot = state.options.shots.find(
        (candidate) => candidate.id === instance?.shotId,
      );
      if (instance && shot) {
        setSelection({
          productionId: shot.productionId,
          shotId: shot.id,
          screenInstanceId: instance.id,
          frame: Math.max(
            instance.startFrame,
            Math.min(selection.frame, instance.endFrame - 1),
          ),
        });
      }
    }
  }

  function selectProduction(productionId: string) {
    if (!state || !selection) return;
    const episode = state.options.episodes.find(
      (candidate) => candidate.productionId === productionId,
    );
    const shot = state.options.shots.find(
      (candidate) =>
        candidate.episodeId === episode?.id ||
        candidate.productionId === productionId,
    );
    const instance = choosePreviewScreenForShot(state, shot?.id);

    setSelectedRecordIds((current) => ({
      ...current,
      productions: productionId,
      episodes: episode?.id ?? "",
      shots: shot?.id ?? "",
      screen_instances: instance?.id ?? "",
    }));
    setActiveTableId("productions");

    if (shot && instance) {
      setSelection({
        productionId,
        shotId: shot.id,
        screenInstanceId: instance.id,
        frame: previewFrameForScreen(instance),
      });
    } else {
      setSelection({
        ...selection,
        productionId,
      });
    }
  }

  function updateRecords(tableId: string, records: AppRecord[]) {
    if (!state) return;
    setState({
      ...state,
      records: {
        ...state.records,
        [tableId]: records,
      },
    });
    setRefreshCounter((value) => value + 1);
  }

  function refreshRecord(tableId: string, record: AppRecord) {
    setSelectedRecordIds((current) => ({
      ...current,
      [tableId]: record.id,
    }));
    setRefreshCounter((value) => value + 1);
  }

  function refreshAppStateAfterSave(tableId: string, record: AppRecord) {
    refreshRecord(tableId, record);
    void getAppState()
      .then((nextState) => {
        setState(nextState);
        setSelectedRecordIds((current) => ({
          ...initialSelectedRecords(nextState),
          ...current,
          [tableId]: record.id,
        }));
        setRefreshCounter((value) => value + 1);
      })
      .catch((error: Error) => setRequestError(error.message));
  }

  function syncSelectionForCreatedRecord(
    nextState: AppState,
    tableId: string,
    record: AppRecord,
  ) {
    setActiveTableId(tableId);
    setSelectedRecordIds((current) => ({
      ...initialSelectedRecords(nextState),
      ...current,
      [tableId]: record.id,
      ...(tableId === "episodes"
        ? {
            productions: String(record.production_id),
            episodes: record.id,
          }
        : {}),
      ...(tableId === "shots"
        ? {
            productions: String(record.production_id),
            episodes: String(record.episode_id ?? current.episodes),
            shots: record.id,
          }
        : {}),
    }));
    if (tableId === "shots") {
      const firstInstance = choosePreviewScreenForShot(nextState, record.id);
      if (firstInstance) {
        setSelection({
          productionId: String(record.production_id),
          shotId: record.id,
          screenInstanceId: firstInstance.id,
          frame: previewFrameForScreen(firstInstance),
        });
      }
    }
  }

  function performCreateRecord(
    tableId:
      | "productions"
      | "episodes"
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "palette_colors"
      | "production_fonts"
      | "render_presets",
    parent?: { productionId?: string; episodeId?: string },
    options?: { family?: "ios" | "android" },
  ) {
    setBusyProjectAction(true);
    setRequestError("");
    void createAppRecord({
      tableId,
      parent,
      ...(options?.family ? { family: options.family } : {}),
    })
      .then((result) => {
        setState(result.state);
        syncSelectionForCreatedRecord(result.state, result.tableId, result.record);
        setRefreshCounter((value) => value + 1);
      })
      .catch((error: Error) => setRequestError(error.message))
      .finally(() => setBusyProjectAction(false));
  }

  function handleCreateRecord(
    tableId:
      | "productions"
      | "episodes"
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "palette_colors"
      | "production_fonts"
      | "render_presets",
    parent?: { productionId?: string; episodeId?: string },
  ) {
    if (tableId === "themes") {
      setThemeCreateParent({ productionId: parent?.productionId });
      return;
    }
    performCreateRecord(tableId, parent);
  }

  function handleCreateProductionFromModal() {
    performCreateRecord("productions");
    setProductionModalOpen(false);
  }

  function handleCreateThemeFromModal(family: "ios" | "android") {
    performCreateRecord("themes", themeCreateParent ?? undefined, { family });
    setThemeCreateParent(null);
  }

  function handleDuplicateRecord(
    tableId:
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "palette_colors"
      | "production_fonts"
      | "render_presets",
    recordId: string,
  ) {
    setBusyProjectAction(true);
    setRequestError("");
    void duplicateAppRecord({ tableId, recordId })
      .then((result) => {
        setState(result.state);
        syncSelectionForCreatedRecord(result.state, result.tableId, result.record);
        setRefreshCounter((value) => value + 1);
      })
      .catch((error: Error) => setRequestError(error.message))
      .finally(() => setBusyProjectAction(false));
  }

  type DeletableTableId =
    | "shots"
    | "icon_themes"
    | "status_bars"
    | "navigation_bars"
    | "themes"
    | "devices"
    | "palette_colors"
    | "production_fonts"
    | "render_presets";

  function executeDeleteRecord(tableId: DeletableTableId, recordId: string) {
    setBusyProjectAction(true);
    setRequestError("");
    void deleteAppRecord({ tableId, recordId })
      .then((result) => {
        setState(result.state);
        const nextSelected = initialSelectedRecords(result.state);
        setSelectedRecordIds(nextSelected);
        setActiveTableId(tableId);
        try {
          setSelection(chooseInitialSelection(result.state));
        } catch {
          setSelection(null);
        }
        setRefreshCounter((value) => value + 1);
      })
      .catch((error: Error) => setRequestError(error.message))
      .finally(() => setBusyProjectAction(false));
  }

  function handleDeleteRecord(
    tableId:
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "palette_colors"
      | "production_fonts"
      | "render_presets",
    recordId: string,
  ) {
    if (tableId === "palette_colors" && state) {
      const record = state.records.palette_colors?.find(
        (candidate) => candidate.id === recordId,
      );
      const token = String(record?.token ?? "");
      const usages = paletteTokenUsages({
        tables: state.tables,
        records: state.records,
        record,
        token,
      });
      if (paletteTokenUsageCount(usages) > 0) {
        setPaletteDeleteBlocker({ token, usages });
        return;
      }
      setPendingPaletteDelete({ tableId, recordId, token });
      return;
    }
    executeDeleteRecord(tableId, recordId);
  }

  function handleMoveScreenInstance(recordId: string, direction: -1 | 1) {
    setBusyProjectAction(true);
    setRequestError("");
    void moveScreenInstanceRecord({ recordId, direction })
      .then((result) => {
        setState(result.state);
        setSelectedRecordIds({
          ...selectedRecordIds,
          screen_instances: recordId,
        });
        const moved = result.state.options.screenInstances.find(
          (instance) => instance.id === recordId,
        );
        if (moved && selection?.screenInstanceId === recordId) {
          setSelection({
            ...selection,
            frame: Math.max(
              moved.startFrame,
              Math.min(selection.frame, moved.endFrame - 1),
            ),
          });
        }
        setRefreshCounter((value) => value + 1);
      })
      .catch((error: Error) => setRequestError(error.message))
      .finally(() => setBusyProjectAction(false));
  }

  function beginHorizontalResize(
    startEvent: ReactPointerEvent,
    options: {
      currentWidth: number;
      minWidth: number;
      maxWidth: number;
      onChange: (nextWidth: number) => void;
    },
  ) {
    startEvent.preventDefault();
    const startX = startEvent.clientX;
    const startWidth = options.currentWidth;

    function move(event: PointerEvent) {
      const nextWidth = Math.min(
        options.maxWidth,
        Math.max(options.minWidth, startWidth + event.clientX - startX),
      );
      options.onChange(nextWidth);
    }

    function stop() {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", stop);
      document.body.classList.remove("is-resizing-panels");
    }

    document.body.classList.add("is-resizing-panels");
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", stop);
  }

  if (!state || !selection) {
    return (
      <main className="loading-screen">
        <div className="loading-mark">M</div>
        <p>{requestError || "Loading app shell…"}</p>
      </main>
    );
  }

  const selectedProduction = state.options.productions.find(
    (production) => production.id === selection.productionId,
  );
  const selectedShot = state.options.shots.find(
    (shot) => shot.id === selection.shotId,
  );
  const selectedEpisode = state.options.episodes.find(
    (episode) => episode.id === selectedShot?.episodeId,
  );
  const selectedScreenInstance = state.options.screenInstances.find(
    (instance) => instance.id === selection.screenInstanceId,
  );
  const selectedScreenLabel =
    selectedScreenInstance?.moduleId?.replace(/^core\./, "") ??
    selectedScreenInstance?.screenType ??
    "Screen";

  return (
    <main className="core-app-shell">
      <section
        className="left-panel-shell"
        style={{ width: authoringWidth }}
      >
        {isProductionModalOpen ? (
          <div
            className="modal-backdrop"
            role="presentation"
            onMouseDown={() => setProductionModalOpen(false)}
          >
            <section
              className="app-modal-card production-modal"
              role="dialog"
              aria-modal="true"
              aria-label="Production actions"
              onMouseDown={(event) => event.stopPropagation()}
            >
              <div className="app-modal-heading">
                <div>
                  <span className="eyebrow">Production actions</span>
                  <h2>Manage productions</h2>
                </div>
                <button
                  type="button"
                  className="app-modal-close-button"
                  onClick={() => setProductionModalOpen(false)}
                >
                  Close
                </button>
              </div>
              <div className="production-action-list">
                <button
                  type="button"
                  className="app-modal-action-button"
                  disabled={busyProjectAction}
                  onClick={handleCreateProductionFromModal}
                >
                  Add production
                </button>
                <button
                  type="button"
                  className="app-modal-action-button"
                  disabled
                  title="Duplicate will copy the full production tree in a later pass."
                >
                  Duplicate selected production
                </button>
                <button
                  type="button"
                  className="app-modal-action-button"
                  disabled
                  title="Delete is disabled until cascade rules and backups are confirmed."
                >
                  Delete selected production
                </button>
              </div>
            </section>
          </div>
        ) : null}
        {themeCreateParent ? (
          <div
            className="modal-backdrop"
            role="presentation"
            onMouseDown={() => setThemeCreateParent(null)}
          >
            <section
              className="app-modal-card production-modal theme-family-modal"
              role="dialog"
              aria-modal="true"
              aria-label="Choose theme base"
              onMouseDown={(event) => event.stopPropagation()}
            >
              <div className="app-modal-heading">
                <div>
                  <span className="eyebrow">New theme</span>
                  <h2>Choose a starting point</h2>
                </div>
                <button
                  type="button"
                  className="app-modal-close-button"
                  onClick={() => setThemeCreateParent(null)}
                >
                  Cancel
                </button>
              </div>
              <p className="modal-help">
                This only seeds sensible defaults. The family becomes read-only
                metadata afterwards.
              </p>
              <div className="theme-family-actions">
                <button
                  type="button"
                  className="app-modal-choice-button"
                  disabled={busyProjectAction}
                  onClick={() => handleCreateThemeFromModal("ios")}
                >
                  <strong>iOS</strong>
                  <span>SF Pro, iOS status bar and home indicator defaults.</span>
                </button>
                <button
                  type="button"
                  className="app-modal-choice-button"
                  disabled={busyProjectAction}
                  onClick={() => handleCreateThemeFromModal("android")}
                >
                  <strong>Android</strong>
                  <span>Roboto, Android status/navigation defaults.</span>
                </button>
              </div>
            </section>
          </div>
        ) : null}
        {paletteDeleteBlocker ? (
          <div
            className="modal-backdrop"
            role="presentation"
            onMouseDown={() => setPaletteDeleteBlocker(null)}
          >
            <section
              className="app-modal-card palette-token-modal"
              role="dialog"
              aria-modal="true"
              aria-label="Palette color cannot be deleted"
              onMouseDown={(event) => event.stopPropagation()}
            >
              <div className="app-modal-heading">
                <div>
                  <span className="eyebrow">Palette color</span>
                  <h2>Cannot delete “{paletteDeleteBlocker.token}”</h2>
                </div>
              </div>
              <p className="modal-help">
                This token is still used. Rename or replace these references
                before deleting it.
              </p>
              <div className="palette-usage-list">
                {paletteDeleteBlocker.usages.map((usage) => (
                  <div
                    key={`${usage.tableId}:${usage.recordId}:${usage.field}`}
                    className="palette-usage-row"
                  >
                    <strong>{usage.tableLabel}</strong>
                    <span>{usage.recordLabel}</span>
                    <small>
                      {usage.field} · {usage.count} reference
                      {usage.count === 1 ? "" : "s"}
                    </small>
                  </div>
                ))}
              </div>
              <footer className="palette-modal-actions">
                <button
                  type="button"
                  className="app-modal-button"
                  onClick={() => setPaletteDeleteBlocker(null)}
                >
                  Cancel
                </button>
              </footer>
            </section>
          </div>
        ) : null}
        {pendingPaletteDelete ? (
          <AppModalDialog
            eyebrow="Palette color"
            title={`Delete “${pendingPaletteDelete.token}”?`}
            message="This cannot be undone."
            confirmLabel="Delete"
            destructive
            onCancel={() => setPendingPaletteDelete(null)}
            onConfirm={() => {
              const pending = pendingPaletteDelete;
              setPendingPaletteDelete(null);
              executeDeleteRecord(pending.tableId, pending.recordId);
            }}
          />
        ) : null}
        <div className="authoring-workspace">
          <aside
            className="navigation-rail"
            style={{ width: navigationWidth }}
          >
            <div className="navigation-rail-header">
              <header className="app-header">
                <div>
                  <span className="eyebrow">MOCKUPS · core app shell</span>
                  <h1>Production workspace</h1>
                  <p>
                    Production setup, episodes, shots, screens and module data.
                  </p>
                </div>
                <div className="production-switcher">
                  <label className="ui-theme-switcher">
                    UI
                    <select
                      aria-label="UI theme"
                      value={uiTheme}
                      onChange={(event) =>
                        setUiTheme(event.target.value === "dark" ? "dark" : "light")
                      }
                    >
                      <option value="light">Light</option>
                      <option value="dark">Dark</option>
                    </select>
                  </label>
                  <label>
                    Production
                    <select
                      data-testid="top-production-select"
                      value={selection.productionId}
                      onChange={(event) => selectProduction(event.target.value)}
                    >
                      {state.options.productions.map((production) => (
                        <option key={production.id} value={production.id}>
                          {production.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <button
                    type="button"
                    className="production-menu-button ui-icon-button"
                    aria-label="Production actions"
                    onClick={() => setProductionModalOpen(true)}
                  >
                    …
                  </button>
                </div>
              </header>
              {requestError ? (
                <div className="alert error" role="alert">
                  {requestError}
                </div>
              ) : null}
            </div>
            <div className="navigation-rail-divider" aria-hidden="true" />
            <div className="navigation-rail-content">
              <ProjectTree
                tables={state.tables}
                activeTableId={activeTableId}
                records={state.records}
                options={state.options}
                selectedRecordIds={selectedRecordIds}
                busyAction={busyProjectAction}
                onTableChange={setActiveTableId}
                onRecordSelect={(tableId, recordId) => {
                  setSelectedRecordIds({
                    ...selectedRecordIds,
                    [tableId]: recordId,
                  });
                  selectContextFromRecord(tableId, recordId);
                }}
                onCreateRecord={handleCreateRecord}
                onDuplicateRecord={handleDuplicateRecord}
                onDeleteRecord={handleDeleteRecord}
                onMoveScreenInstance={handleMoveScreenInstance}
              />
            </div>
          </aside>
          <div
            className="panel-resizer"
            role="separator"
            aria-label="Resize navigation and editor panels"
            onPointerDown={(event) =>
              beginHorizontalResize(event, {
                currentWidth: navigationWidth,
                minWidth: 280,
                maxWidth: Math.max(320, authoringWidth - 420),
                onChange: setNavigationWidth,
              })
            }
          />
          <div className="editor-workspace">
            <div className="workspace-breadcrumb">
              <span className="breadcrumb-home">⌂</span>
              <span>{selectedProduction?.name ?? "Production"}</span>
              <span>›</span>
              <span>{selectedEpisode?.name ?? "Episode"}</span>
              <span>›</span>
              <span>{selectedShot?.name ?? "Shot"}</span>
              <span>›</span>
              <strong>{selectedScreenLabel}</strong>
              <button
                type="button"
                className="workspace-breadcrumb-action"
                disabled
                title="Module workspace will be wired in a later pass"
              >
                Open in Module ↗
              </button>
            </div>
            {activeTable ? (
              <RecordEditor
                tables={state.tables}
                table={activeTable}
                record={selectedRecord}
                records={state.records}
                productionId={selection.productionId}
                inheritedFields={
                  selectedRecord
                    ? state.inheritedJson[activeTable.id]?.[selectedRecord.id] ?? {}
                    : {}
                }
                onRecordsChanged={(records) =>
                  updateRecords(activeTable.id, records)
                }
                onRecordSaved={(record) =>
                  refreshAppStateAfterSave(activeTable.id, record)
                }
                onAppStateChanged={(nextState, tableId, record) => {
                  setState(nextState);
                  setSelectedRecordIds((current) => ({
                    ...initialSelectedRecords(nextState),
                    ...current,
                    [tableId]: record.id,
                  }));
                  setRefreshCounter((value) => value + 1);
                }}
                onPreviewRelativeFrameChange={(relativeFrame) => {
                  if (!state) return;
                  setSelection((current) => {
                    if (!current) return current;
                    const instance = state.options.screenInstances.find(
                      (candidate) => candidate.id === current.screenInstanceId,
                    );
                    if (!instance) return current;
                    return {
                      ...current,
                      frame: Math.max(
                        instance.startFrame,
                        Math.min(
                          instance.startFrame + Math.max(0, Math.round(relativeFrame)),
                          instance.endFrame - 1,
                        ),
                      ),
                    };
                  });
                }}
              />
            ) : null}
          </div>
        </div>
      </section>

      <div
        className="panel-resizer"
        role="separator"
        aria-label="Resize editor and preview panels"
        onPointerDown={(event) =>
          beginHorizontalResize(event, {
            currentWidth: authoringWidth,
            minWidth: Math.max(720, navigationWidth + 360),
            maxWidth: Math.max(760, window.innerWidth - 420),
            onChange: setAuthoringWidth,
          })
        }
      />

      <RightPreviewShell
        options={state.options}
        selection={selection}
        payload={preview}
        error={previewError}
        onSelectionChange={(nextSelection) => {
          setSelection(nextSelection);
          setSelectedRecordIds((current) => ({
            ...current,
            productions: nextSelection.productionId,
            episodes:
              state.options.shots.find(
                (shot) => shot.id === nextSelection.shotId,
              )?.episodeId ?? current.episodes,
            shots: nextSelection.shotId,
            screen_instances: nextSelection.screenInstanceId,
          }));
        }}
      />
    </main>
  );
}
