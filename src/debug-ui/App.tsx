import { useEffect, useMemo, useState } from "react";
import {
  createAppRecord,
  getAppState,
  getPreviewPayload,
  type AppRecord,
  type AppState,
  type DebugPayload,
  type DebugSelection,
} from "./api/client.js";
import { AppPreviewPanel } from "./components/AppPreviewPanel.js";
import { ProjectTree } from "./components/ProjectTree.js";
import { RecordEditor } from "./components/RecordEditor.js";

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
  const screen =
    state.options.screenInstances.find(
      (candidate) =>
        candidate.shotId === shot?.id && candidate.moduleId === "core.chat",
    ) ??
    state.options.screenInstances.find(
      (candidate) => candidate.shotId === shot?.id,
    );
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
      const instance = state.options.screenInstances.find(
        (candidate) => candidate.shotId === shot?.id,
      );
      if (shot && instance) {
        setSelection({
          productionId: recordId,
          shotId: shot.id,
          screenInstanceId: instance.id,
          frame: instance.startFrame,
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
      const instance = state.options.screenInstances.find(
        (candidate) => candidate.shotId === shot?.id,
      );
      if (episode && shot && instance) {
        setSelection({
          productionId: episode.productionId,
          shotId: shot.id,
          screenInstanceId: instance.id,
          frame: instance.startFrame,
        });
      }
    }
    if (tableId === "shots") {
      const shot = state.options.shots.find(
        (candidate) => candidate.id === recordId,
      );
      const instance = state.options.screenInstances.find(
        (candidate) => candidate.shotId === recordId,
      );
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
      const firstInstance = nextState.options.screenInstances.find(
        (candidate) => candidate.shotId === record.id,
      );
      if (firstInstance) {
        setSelection({
          productionId: String(record.production_id),
          shotId: record.id,
          screenInstanceId: firstInstance.id,
          frame: firstInstance.startFrame,
        });
      }
    }
  }

  function handleCreateRecord(
    tableId: "productions" | "episodes" | "shots",
    parent?: { productionId?: string; episodeId?: string },
  ) {
    setBusyProjectAction(true);
    setRequestError("");
    void createAppRecord({ tableId, parent })
      .then((result) => {
        setState(result.state);
        syncSelectionForCreatedRecord(result.state, result.tableId, result.record);
        setRefreshCounter((value) => value + 1);
      })
      .catch((error: Error) => setRequestError(error.message))
      .finally(() => setBusyProjectAction(false));
  }

  if (!state || !selection) {
    return (
      <main className="loading-screen">
        <div className="loading-mark">M</div>
        <p>{requestError || "Loading app shell…"}</p>
      </main>
    );
  }

  return (
    <main className="core-app-shell">
      <section className="left-app-panel">
        <header className="app-header">
          <div>
            <span className="eyebrow">MOCKUPS · core app shell</span>
            <h1>Data workspace</h1>
            <p>
              Edit core tables with validated autosave. Preview remains
              calculated output.
            </p>
          </div>
        </header>
        {requestError ? (
          <div className="alert error" role="alert">
            {requestError}
          </div>
        ) : null}
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
        />
        {activeTable ? (
          <RecordEditor
            table={activeTable}
            record={selectedRecord}
            records={state.records}
            inheritedFields={
              selectedRecord
                ? state.inheritedJson[activeTable.id]?.[selectedRecord.id] ?? {}
                : {}
            }
            onRecordsChanged={(records) => updateRecords(activeTable.id, records)}
            onRecordSaved={(record) =>
              refreshAppStateAfterSave(activeTable.id, record)
            }
          />
        ) : null}
      </section>

      <AppPreviewPanel
        options={state.options}
        selection={selection}
        payload={preview}
        busy={busyPreview}
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
