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

const APPS_TABLE_IDS = new Set(["apps", "screen_templates"]);

const PRODUCTION_DATA_TABLE_IDS = new Set([
  "actors",
  "themes",
  "devices",
  "device_states",
  "media_assets",
  "render_presets",
  "animation_presets",
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

function recordButtonClass(
  tableId: string,
  recordId: string,
  activeTableId: string,
  selectedRecordIds: Record<string, string>,
) {
  const classes = [];
  if (selectedRecordIds[tableId] === recordId) classes.push("active");
  if (activeTableId === tableId && selectedRecordIds[tableId] === recordId) {
    classes.push("editing");
  }
  return classes.join(" ");
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
  const [browserTab, setBrowserTab] = useState<"project" | "apps" | "data">(
    "project",
  );
  const tablesById = tableById(tables);
  const selectedProductionId = selectedRecordIds.productions;
  const selectedEpisodeId = selectedRecordIds.episodes;
  const selectedShotId = selectedRecordIds.shots;
  const selectedScreenId = selectedRecordIds.screen_instances;
  const selectedScreen = options.screenInstances.find(
    (instance) => instance.id === selectedScreenId,
  );
  const productionEpisodes = options.episodes.filter(
    (episode) => episode.productionId === selectedProductionId,
  );
  const episodeShots = options.shots.filter(
    (shot) => shot.episodeId === selectedEpisodeId,
  );
  const shotInstances = options.screenInstances.filter(
    (instance) => instance.shotId === selectedShotId,
  );
  const moduleThemeConfigRecords = (records.module_theme_configs ?? []).filter(
    (record) =>
      record.production_id === selectedProductionId &&
      (!selectedScreen?.moduleId || record.module_id === selectedScreen.moduleId),
  );
  const appsTables = tables.filter((table) => APPS_TABLE_IDS.has(table.id));
  const dataTables = tables.filter((table) =>
    PRODUCTION_DATA_TABLE_IDS.has(table.id),
  );

  function recordsForSelectedProduction(tableId: string) {
    return (records[tableId] ?? []).filter(
      (record) =>
        !Object.hasOwn(record, "production_id") ||
        record.production_id === selectedProductionId,
    );
  }

  function select(tableId: string, recordId: string) {
    onTableChange(tableId);
    onRecordSelect(tableId, recordId);
  }

  function renderRecordButtons(table: AppTableDefinition, tableRecords: AppRecord[]) {
    return tableRecords.length === 0 ? (
      <EmptyPanel>No records yet.</EmptyPanel>
    ) : (
      <div className="hierarchy-list">
        {tableRecords.map((record) => (
          <button
            key={record.id}
            type="button"
            data-testid={`record-${record.id}`}
            className={recordButtonClass(
              table.id,
              record.id,
              activeTableId,
              selectedRecordIds,
            )}
            onClick={() => select(table.id, record.id)}
          >
            <strong>{recordTitle(table, record)}</strong>
            <small>{record.id}</small>
          </button>
        ))}
      </div>
    );
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
          className={browserTab === "apps" ? "active" : ""}
          onClick={() => setBrowserTab("apps")}
        >
          Apps
        </button>
        <button
          type="button"
          role="tab"
          className={browserTab === "data" ? "active" : ""}
          onClick={() => setBrowserTab("data")}
        >
          Production data
        </button>
      </div>

      {browserTab === "project" ? (
        <div className="hierarchy-browser">
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
                    className={recordButtonClass(
                      "episodes",
                      episode.id,
                      activeTableId,
                      selectedRecordIds,
                    )}
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
                    className={recordButtonClass(
                      "shots",
                      shot.id,
                      activeTableId,
                      selectedRecordIds,
                    )}
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
                    className={recordButtonClass(
                      "screen_instances",
                      instance.id,
                      activeTableId,
                      selectedRecordIds,
                    )}
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

          <BrowserPanel
            title="Module Theme Configs"
            subtitle="Selected screen module"
            count={moduleThemeConfigRecords.length}
            canAdd={false}
          >
            {selectedScreenId ? (
              tablesById.get("module_theme_configs") ? (
                renderRecordButtons(
                  tablesById.get("module_theme_configs")!,
                  moduleThemeConfigRecords,
                )
              ) : null
            ) : (
              <EmptyPanel>Select a screen first.</EmptyPanel>
            )}
          </BrowserPanel>
        </div>
      ) : browserTab === "apps" ? (
        <div className="library-browser">
          <div className="hierarchy-browser two-column-browser">
            {appsTables.map((table) => (
              <BrowserPanel
                key={table.id}
                title={table.label}
                subtitle="Selected production"
                count={recordsForSelectedProduction(table.id).length}
                canAdd={false}
              >
                {renderRecordButtons(table, recordsForSelectedProduction(table.id))}
              </BrowserPanel>
            ))}
          </div>
        </div>
      ) : (
        <div className="library-browser">
          <div className="resource-table-list" role="tablist">
            {dataTables.map((table) => {
              const tableRecords = recordsForSelectedProduction(table.id);
              return (
                <button
                  key={table.id}
                  type="button"
                  role="tab"
                  data-testid={`tab-${table.id}`}
                  className={table.id === activeTableId ? "active" : ""}
                  onClick={() => onTableChange(table.id)}
                >
                  {table.label}
                  <span>{tableRecords.length}</span>
                </button>
              );
            })}
          </div>
          <div className="record-list resource-record-list">
            {dataTables
              .find((table) => table.id === activeTableId)
              ? renderRecordButtons(
                  dataTables.find((table) => table.id === activeTableId)!,
                  recordsForSelectedProduction(activeTableId),
                )
              : (
                <EmptyPanel>Select a production data table.</EmptyPanel>
              )}
          </div>
        </div>
      )}
    </section>
  );
}
