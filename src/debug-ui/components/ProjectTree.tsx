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

function TreeIcon({ name }: { name: string }) {
  if (name === "project") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M4 9.5 12 4l8 5.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1V9.5Z" />
      </svg>
    );
  }
  if (name === "app") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <rect x="5" y="5" width="14" height="14" rx="4" />
        <path d="M9 9h6v6H9z" />
      </svg>
    );
  }
  if (name === "episode") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <rect x="4" y="5" width="16" height="15" rx="3" />
        <path d="M8 3v4M16 3v4M4 10h16" />
      </svg>
    );
  }
  if (name === "shot") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M7 7h2l1.5-2h3L15 7h2a3 3 0 0 1 3 3v6a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3v-6a3 3 0 0 1 3-3Z" />
        <circle cx="12" cy="13" r="3.2" />
      </svg>
    );
  }
  if (name === "screen") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <rect x="4" y="5" width="16" height="12" rx="2" />
        <path d="M9 21h6M12 17v4" />
      </svg>
    );
  }
  if (name === "module") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="m12 3 8 4.5v9L12 21l-8-4.5v-9L12 3Z" />
        <path d="m4 7.5 8 4.5 8-4.5M12 12v9" />
      </svg>
    );
  }
  if (name === "data") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M5 6c0-1.7 3.1-3 7-3s7 1.3 7 3-3.1 3-7 3-7-1.3-7-3Z" />
        <path d="M5 6v6c0 1.7 3.1 3 7 3s7-1.3 7-3V6" />
        <path d="M5 12v6c0 1.7 3.1 3 7 3s7-1.3 7-3v-6" />
      </svg>
    );
  }
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <rect x="5" y="5" width="14" height="14" rx="3" />
      <path d="M9 9h6v6H9z" />
    </svg>
  );
}

function TreeButton({
  tableId,
  recordId,
  activeTableId,
  selectedRecordIds,
  icon,
  title,
  meta,
  onClick,
}: {
  tableId: string;
  recordId: string;
  activeTableId: string;
  selectedRecordIds: Record<string, string>;
  icon?: string;
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
      {icon ? (
        <span className="tree-record-icon" aria-hidden="true">
          <TreeIcon name={icon} />
        </span>
      ) : null}
      <span className="tree-record-copy">
        <strong>{title}</strong>
        {meta ? <small>{meta}</small> : null}
      </span>
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
  const [browserTab, setBrowserTab] = useState<"" | "project" | "apps" | "data">(
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

  function sectionLabel(icon: string, title: string) {
    return (
      <span className="tree-section-title">
        <span aria-hidden="true">
          <TreeIcon name={icon} />
        </span>
        {title}
      </span>
    );
  }

  function renderProjectTree() {
    if (!selectedProductionId) {
      return <EmptyPanel>Select a production first.</EmptyPanel>;
    }
    return (
      <div className="project-tree-view">
        <div className="tree-toolbar">
          {sectionLabel("episode", "Episodes")}
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
                    icon="episode"
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
                              icon="shot"
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
                                    icon="screen"
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
                                            icon="module"
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
          {sectionLabel("app", "Apps / Modules")}
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
                    icon="app"
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
                        icon="module"
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

  function renderDataTree() {
    return (
      <div className="project-tree-view">
        <div className="tree-toolbar">
          {sectionLabel("data", "Production data")}
          <TreeActions />
        </div>
        {dataTables.map((table) => {
          const tableRecords = recordsForSelectedProduction(table.id);
          return (
            <details
              key={table.id}
              className="tree-node"
              open={table.id === activeTableId}
            >
              <summary>
                <button
                  type="button"
                  role="tab"
                  data-testid={`tab-${table.id}`}
                  className={table.id === activeTableId ? "active editing" : ""}
                  onClick={() => onTableChange(table.id)}
                >
                  <span className="tree-record-icon" aria-hidden="true">
                    <TreeIcon name="data" />
                  </span>
                  <span className="tree-record-copy">
                    <strong>{table.label}</strong>
                    <small>
                      {tableRecords.length} record
                      {tableRecords.length === 1 ? "" : "s"}
                    </small>
                  </span>
                </button>
                <TreeActions />
              </summary>
              <div className="tree-children">
                {tableRecords.length === 0 ? (
                  <EmptyPanel>No records yet.</EmptyPanel>
                ) : (
                  tableRecords.map((record) => (
                    <TreeButton
                      key={record.id}
                      tableId={table.id}
                      recordId={record.id}
                      activeTableId={activeTableId}
                      selectedRecordIds={selectedRecordIds}
                      icon="data"
                      title={recordTitle(table, record)}
                      meta={String(record.id)}
                      onClick={() => select(table.id, record.id)}
                    />
                  ))
                )}
              </div>
            </details>
          );
        })}
      </div>
    );
  }

  function renderWorkspaceAccordion(
    id: "project" | "apps" | "data",
    icon: string,
    title: string,
    subtitle: string,
    children: ReactNode,
  ) {
    const active = browserTab === id;
    return (
      <section className={`workspace-accordion-card ${active ? "active" : ""}`}>
        <button
          type="button"
          className="workspace-accordion-trigger"
          aria-expanded={active}
          onClick={() => setBrowserTab(active ? "" : id)}
        >
          <span className="workspace-accordion-icon" aria-hidden="true">
            <TreeIcon name={icon} />
          </span>
          <span className="workspace-accordion-copy">
            <strong>{title}</strong>
            <small>{subtitle}</small>
          </span>
        </button>
        {active ? <div className="workspace-accordion-body">{children}</div> : null}
      </section>
    );
  }

  return (
    <section className="panel project-browser">
      <div className="workspace-accordions" aria-label="Workspace">
        {renderWorkspaceAccordion(
          "project",
          "project",
          "Project",
          "Episodes, shots, screens and modules",
          renderProjectTree(),
        )}
        {renderWorkspaceAccordion(
          "apps",
          "app",
          "Apps",
          "Apps and module defaults",
          renderAppsTree(),
        )}
        {renderWorkspaceAccordion(
          "data",
          "data",
          "Production data",
          "Actors, devices, themes and assets",
          renderDataTree(),
        )}
      </div>
    </section>
  );
}
