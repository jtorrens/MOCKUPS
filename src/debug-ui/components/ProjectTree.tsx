import { useState } from "react";
import type { ReactNode } from "react";
import type {
  AppRecord,
  AppTableDefinition,
  DebugOptions,
} from "../api/client.js";

interface ProjectTreeProps {
  tables: AppTableDefinition[];
  activeTableId: string;
  records: Record<string, AppRecord[]>;
  options: DebugOptions;
  selectedRecordIds: Record<string, string>;
  busyAction?: boolean;
  onTableChange: (tableId: string) => void;
  onRecordSelect: (tableId: string, recordId: string) => void;
  onCreateRecord: (
    tableId: "productions" | "episodes" | "shots",
    parent?: { productionId?: string; episodeId?: string },
  ) => void;
}

const HIERARCHY_TABLE_IDS = new Set([
  "productions",
  "episodes",
  "shots",
  "screen_instances",
]);

function tableById(tables: AppTableDefinition[]) {
  return new Map(tables.map((table) => [table.id, table]));
}

function recordTitle(table: AppTableDefinition, record: AppRecord): string {
  return String(record[table.titleColumn] ?? record.id);
}

function ActionButton({
  children,
  disabled,
  title,
  onClick,
}: {
  children: string;
  disabled?: boolean;
  title?: string;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      className="mini-action"
      disabled={disabled}
      title={title}
      onClick={(event) => {
        event.stopPropagation();
        onClick?.();
      }}
    >
      {children}
    </button>
  );
}

function BrowserPanel({
  title,
  subtitle,
  count,
  canAdd,
  busy,
  children,
  onAdd,
}: {
  title: string;
  subtitle: string;
  count: number;
  canAdd?: boolean;
  busy?: boolean;
  children: ReactNode;
  onAdd?: () => void;
}) {
  return (
    <details className="hierarchy-panel" open>
      <summary>
        <div>
          <span className="eyebrow">{subtitle}</span>
          <strong>
            {title} <small>{count}</small>
          </strong>
        </div>
        <div className="hierarchy-actions">
          <ActionButton
            disabled={!canAdd || busy}
            title={canAdd ? `Add ${title.toLowerCase()}` : "Select a parent first"}
            onClick={onAdd}
          >
            ＋
          </ActionButton>
          <ActionButton disabled title="Duplicate will copy child records in a later pass">
            ⧉
          </ActionButton>
          <ActionButton disabled title="Delete is disabled until cascade policy is confirmed">
            ⌫
          </ActionButton>
        </div>
      </summary>
      <div className="hierarchy-panel-body">{children}</div>
    </details>
  );
}

function EmptyPanel({ children }: { children: string }) {
  return <div className="empty-record-list compact-empty">{children}</div>;
}

export function ProjectTree({
  tables,
  activeTableId,
  records,
  options,
  selectedRecordIds,
  busyAction = false,
  onTableChange,
  onRecordSelect,
  onCreateRecord,
}: ProjectTreeProps) {
  const [browserTab, setBrowserTab] = useState<"project" | "library">("project");
  const tablesById = tableById(tables);
  const resourceTables = tables.filter((table) => !HIERARCHY_TABLE_IDS.has(table.id));
  const activeResourceTable = resourceTables.find(
    (table) => table.id === activeTableId,
  ) ?? resourceTables[0];
  const activeResourceRecords = activeResourceTable
    ? (records[activeResourceTable.id] ?? [])
    : [];
  const selectedProductionId = selectedRecordIds.productions;
  const selectedEpisodeId = selectedRecordIds.episodes;
  const selectedShotId = selectedRecordIds.shots;
  const productionEpisodes = options.episodes.filter(
    (episode) => episode.productionId === selectedProductionId,
  );
  const episodeShots = options.shots.filter(
    (shot) => shot.episodeId === selectedEpisodeId,
  );
  const shotInstances = options.screenInstances.filter(
    (instance) => instance.shotId === selectedShotId,
  );

  function select(tableId: string, recordId: string) {
    onTableChange(tableId);
    onRecordSelect(tableId, recordId);
  }

  return (
    <section className="panel project-browser">
      <div className="workspace-tabs" role="tablist" aria-label="Workspace">
        <button
          type="button"
          role="tab"
          className={browserTab === "project" ? "active" : ""}
          onClick={() => setBrowserTab("project")}
        >
          Project
        </button>
        <button
          type="button"
          role="tab"
          className={browserTab === "library" ? "active" : ""}
          onClick={() => setBrowserTab("library")}
        >
          Library
        </button>
      </div>

      {browserTab === "project" ? (
        <div className="hierarchy-browser">
          <BrowserPanel
            title="Productions"
            subtitle="Project"
            count={options.productions.length}
            canAdd
            busy={busyAction}
            onAdd={() => onCreateRecord("productions")}
          >
            <div className="hierarchy-list">
              {options.productions.map((production) => (
                <button
                  key={production.id}
                  type="button"
                  data-testid={`record-${production.id}`}
                  className={
                    selectedRecordIds.productions === production.id ? "active" : ""
                  }
                  onClick={() => select("productions", production.id)}
                >
                  <strong>{production.name}</strong>
                  <small>{production.id}</small>
                </button>
              ))}
            </div>
          </BrowserPanel>

          <BrowserPanel
            title="Episodes"
            subtitle="Selected production"
            count={productionEpisodes.length}
            canAdd={Boolean(selectedProductionId)}
            busy={busyAction}
            onAdd={() =>
              onCreateRecord("episodes", { productionId: selectedProductionId })
            }
          >
            {selectedProductionId ? (
              <div className="hierarchy-list">
                {productionEpisodes.map((episode) => (
                  <button
                    key={episode.id}
                    type="button"
                    data-testid={`record-${episode.id}`}
                    className={
                      selectedRecordIds.episodes === episode.id ? "active" : ""
                    }
                    onClick={() => select("episodes", episode.id)}
                  >
                    <strong>{episode.name}</strong>
                    <small>{episode.id}</small>
                  </button>
                ))}
              </div>
            ) : (
              <EmptyPanel>Select a production first.</EmptyPanel>
            )}
          </BrowserPanel>

          <BrowserPanel
            title="Shots"
            subtitle="Selected episode"
            count={episodeShots.length}
            canAdd={Boolean(selectedEpisodeId)}
            busy={busyAction}
            onAdd={() => onCreateRecord("shots", { episodeId: selectedEpisodeId })}
          >
            {selectedEpisodeId ? (
              <div className="hierarchy-list">
                {episodeShots.map((shot) => (
                  <button
                    key={shot.id}
                    type="button"
                    data-testid={`record-${shot.id}`}
                    className={selectedRecordIds.shots === shot.id ? "active" : ""}
                    onClick={() => select("shots", shot.id)}
                  >
                    <strong>{shot.name}</strong>
                    <small>
                      {shot.durationFrames}f · {shot.fps}fps
                    </small>
                  </button>
                ))}
              </div>
            ) : (
              <EmptyPanel>Select an episode first.</EmptyPanel>
            )}
          </BrowserPanel>

          <BrowserPanel
            title="Screens"
            subtitle="Selected shot"
            count={shotInstances.length}
            canAdd={false}
          >
            {selectedShotId ? (
              <div className="hierarchy-list">
                {shotInstances.map((instance) => (
                  <button
                    key={instance.id}
                    type="button"
                    data-testid={`record-${instance.id}`}
                    className={
                      selectedRecordIds.screen_instances === instance.id
                        ? "active"
                        : ""
                    }
                    onClick={() => select("screen_instances", instance.id)}
                  >
                    <strong>{instance.screenType}</strong>
                    <small>{instance.moduleId ?? "legacy"}</small>
                  </button>
                ))}
              </div>
            ) : (
              <EmptyPanel>Select a shot first.</EmptyPanel>
            )}
          </BrowserPanel>
        </div>
      ) : (
        <div className="library-browser">
          <div className="resource-table-list" role="tablist">
            {resourceTables.map((table) => (
              <button
                key={table.id}
                type="button"
                role="tab"
                data-testid={`tab-${table.id}`}
                className={table.id === activeTableId ? "active" : ""}
                onClick={() => onTableChange(table.id)}
              >
                {table.label}
                <span>{records[table.id]?.length ?? 0}</span>
              </button>
            ))}
          </div>
          <div className="record-list resource-record-list">
            {activeResourceTable ? (
              activeResourceRecords.length === 0 ? (
                <div className="empty-record-list">No records yet.</div>
              ) : (
                activeResourceRecords.map((record) => (
                  <button
                    key={record.id}
                    type="button"
                    data-testid={`record-${record.id}`}
                    className={
                      selectedRecordIds[activeResourceTable.id] === record.id
                        ? "active"
                        : ""
                    }
                    onClick={() => select(activeResourceTable.id, record.id)}
                  >
                    <strong>
                      {recordTitle(
                        tablesById.get(activeResourceTable.id) ??
                          activeResourceTable,
                        record,
                      )}
                    </strong>
                    <small>{record.id}</small>
                  </button>
                ))
              )
            ) : null}
          </div>
        </div>
      )}
    </section>
  );
}
