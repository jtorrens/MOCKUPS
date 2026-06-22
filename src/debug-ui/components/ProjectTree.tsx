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

const APPS_TABLE_IDS = new Set(["apps", "module_theme_configs"]);

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

function TreeActions({
  canAdd,
  busy,
  onAdd,
}: {
  canAdd?: boolean;
  busy?: boolean;
  onAdd?: () => void;
}) {
  return (
    <span className="tree-actions">
      <ActionButton disabled={!canAdd || busy} title="Add" onClick={onAdd}>
        ＋
      </ActionButton>
      <ActionButton disabled title="Duplicate will copy child records in a later pass">
        ⧉
      </ActionButton>
      <ActionButton disabled title="Delete is disabled until cascade policy is confirmed">
        ⌫
      </ActionButton>
    </span>
  );
}

function TreeButton({
  tableId,
  recordId,
  activeTableId,
  selectedRecordIds,
  title,
  meta,
  onClick,
}: {
  tableId: string;
  recordId: string;
  activeTableId: string;
  selectedRecordIds: Record<string, string>;
  title: string;
  meta?: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      data-testid={`record-${recordId}`}
      className={recordButtonClass(
        tableId,
        recordId,
        activeTableId,
        selectedRecordIds,
      )}
      onClick={onClick}
    >
      <strong>{title}</strong>
      {meta ? <small>{meta}</small> : null}
    </button>
  );
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
  const appRecords = recordsForSelectedProduction("apps");
  const moduleConfigRecords = recordsForSelectedProduction("module_theme_configs");

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

  function screenTitle(instance: DebugOptions["screenInstances"][number]) {
    return instance.moduleId?.replace(/^core\./, "") ?? instance.screenType;
  }

  function renderProjectTree() {
    if (!selectedProductionId) {
      return <EmptyPanel>Select a production first.</EmptyPanel>;
    }
    return (
      <div className="project-tree-view">
        <div className="tree-toolbar">
          <span>Episodes</span>
          <TreeActions
            canAdd={Boolean(selectedProductionId)}
            busy={busyAction}
            onAdd={() =>
              onCreateRecord("episodes", { productionId: selectedProductionId })
            }
          />
        </div>
        {productionEpisodes.length === 0 ? (
          <EmptyPanel>No episodes yet.</EmptyPanel>
        ) : (
          productionEpisodes.map((episode) => {
            const shots = options.shots.filter(
              (shot) => shot.episodeId === episode.id,
            );
            return (
              <details key={episode.id} className="tree-node" open>
                <summary>
                  <TreeButton
                    tableId="episodes"
                    recordId={episode.id}
                    activeTableId={activeTableId}
                    selectedRecordIds={selectedRecordIds}
                    title={episode.name}
                    meta={`${shots.length} shot${shots.length === 1 ? "" : "s"}`}
                    onClick={() => select("episodes", episode.id)}
                  />
                  <TreeActions
                    canAdd
                    busy={busyAction}
                    onAdd={() => onCreateRecord("shots", { episodeId: episode.id })}
                  />
                </summary>
                <div className="tree-children">
                  {shots.length === 0 ? (
                    <EmptyPanel>No shots yet.</EmptyPanel>
                  ) : (
                    shots.map((shot) => {
                      const screens = options.screenInstances.filter(
                        (instance) => instance.shotId === shot.id,
                      );
                      return (
                        <details key={shot.id} className="tree-node" open>
                          <summary>
                            <TreeButton
                              tableId="shots"
                              recordId={shot.id}
                              activeTableId={activeTableId}
                              selectedRecordIds={selectedRecordIds}
                              title={shot.name}
                              meta={`${shot.durationFrames}f · ${screens.length} screen${screens.length === 1 ? "" : "s"}`}
                              onClick={() => select("shots", shot.id)}
                            />
                            <TreeActions />
                          </summary>
                          <div className="tree-children">
                            {screens.length === 0 ? (
                              <EmptyPanel>No screens yet.</EmptyPanel>
                            ) : (
                              screens.map((instance) => (
                                <div key={instance.id} className="tree-leaf-with-children">
                                  <TreeButton
                                    tableId="screen_instances"
                                    recordId={instance.id}
                                    activeTableId={activeTableId}
                                    selectedRecordIds={selectedRecordIds}
                                    title={screenTitle(instance)}
                                    meta={`${instance.startFrame}–${instance.endFrame}f`}
                                    onClick={() => select("screen_instances", instance.id)}
                                  />
                                  {tablesById.get("module_theme_configs") ? (
                                    <div className="tree-children compact">
                                      {moduleConfigRecords
                                        .filter(
                                          (config) =>
                                            config.app_id === instance.appId &&
                                            config.module_id === instance.moduleId,
                                        )
                                        .map((config) => (
                                          <TreeButton
                                            key={config.id}
                                            tableId="module_theme_configs"
                                            recordId={config.id}
                                            activeTableId={activeTableId}
                                            selectedRecordIds={selectedRecordIds}
                                            title={String(config.name ?? "Module config")}
                                            meta="Module theme"
                                            onClick={() =>
                                              select("module_theme_configs", config.id)
                                            }
                                          />
                                        ))}
                                    </div>
                                  ) : null}
                                </div>
                              ))
                            )}
                          </div>
                        </details>
                      );
                    })
                  )}
                </div>
              </details>
            );
          })
        )}
      </div>
    );
  }

  function renderAppsTree() {
    return (
      <div className="project-tree-view">
        <div className="tree-toolbar">
          <span>Apps / Modules</span>
          <TreeActions />
        </div>
        {appRecords.length === 0 ? (
          <EmptyPanel>No apps yet.</EmptyPanel>
        ) : (
          appRecords.map((app) => {
            const configs = moduleConfigRecords.filter(
              (config) => config.app_id === app.id,
            );
            return (
              <details key={app.id} className="tree-node" open>
                <summary>
                  <TreeButton
                    tableId="apps"
                    recordId={app.id}
                    activeTableId={activeTableId}
                    selectedRecordIds={selectedRecordIds}
                    title={String(app.name ?? app.id)}
                    meta={String(app.app_type ?? "app")}
                    onClick={() => select("apps", app.id)}
                  />
                  <TreeActions />
                </summary>
                <div className="tree-children">
                  {configs.length === 0 ? (
                    <EmptyPanel>No module configs yet.</EmptyPanel>
                  ) : (
                    configs.map((config) => (
                      <TreeButton
                        key={config.id}
                        tableId="module_theme_configs"
                        recordId={config.id}
                        activeTableId={activeTableId}
                        selectedRecordIds={selectedRecordIds}
                        title={String(config.name ?? "Module config")}
                        meta={String(config.module_id ?? "module")}
                        onClick={() => select("module_theme_configs", config.id)}
                      />
                    ))
                  )}
                </div>
              </details>
            );
          })
        )}
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
        renderProjectTree()
      ) : browserTab === "apps" ? (
        renderAppsTree()
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
