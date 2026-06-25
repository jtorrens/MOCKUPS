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
    tableId:
      | "productions"
      | "episodes"
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "render_presets",
    parent?: { productionId?: string; episodeId?: string },
  ) => void;
  onDuplicateRecord: (
    tableId:
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "render_presets",
    recordId: string,
  ) => void;
  onDeleteRecord: (
    tableId:
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "render_presets",
    recordId: string,
  ) => void;
}

const PRODUCTION_DATA_TABLE_IDS = new Set([
  "actors",
  "icon_themes",
  "status_bars",
  "navigation_bars",
  "themes",
  "devices",
  "media_assets",
  "render_presets",
  "animation_presets",
]);

function tableById(tables: AppTableDefinition[]) {
  return new Map(tables.map((table) => [table.id, table]));
}

function productionDataIcon(tableId: string) {
  if (tableId === "actors") return "actor";
  if (tableId === "icon_themes") return "icon";
  if (tableId === "status_bars") return "status";
  if (tableId === "navigation_bars") return "navigation";
  if (tableId === "themes") return "theme";
  if (tableId === "devices") return "device";
  if (tableId === "media_assets") return "media";
  if (tableId === "render_presets") return "render";
  if (tableId === "animation_presets") return "animation";
  return "data";
}

function recordTitle(table: AppTableDefinition, record: AppRecord): string {
  return String(record[table.titleColumn] ?? record.id);
}

function shotRenderName(
  production: DebugOptions["productions"][number] | undefined,
  episode: DebugOptions["episodes"][number] | undefined,
  shot: DebugOptions["shots"][number],
) {
  const productionSlug = String(
    production?.slug ?? production?.name ?? "production",
  );
  const episodeSlug = String(episode?.slug ?? episode?.name ?? "episode");
  const shotSlug = String(shot.slug ?? shot.name ?? "shot");
  const version = String(shot.version ?? 1).padStart(2, "0");
  return `${productionSlug}_${episodeSlug}_${shotSlug}_v${version}`;
}

function shotDurationFromScreens(
  shot: DebugOptions["shots"][number],
  screens: DebugOptions["screenInstances"],
) {
  if (screens.length === 0) return shot.durationFrames;
  const duration = Math.max(...screens.map((screen) => screen.endFrame));
  return Number.isFinite(duration) && duration > 0
    ? duration
    : shot.durationFrames;
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
      className="project-tree-action-button ui-icon-button"
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

function EmptyPanel({ children }: { children: string }) {
  return <div className="project-tree-empty">{children}</div>;
}

function TreeActions({
  canAdd,
  canDuplicate,
  canDelete,
  busy,
  onAdd,
  onDuplicate,
  onDelete,
}: {
  canAdd?: boolean;
  canDuplicate?: boolean;
  canDelete?: boolean;
  busy?: boolean;
  onAdd?: () => void;
  onDuplicate?: () => void;
  onDelete?: () => void;
}) {
  return (
    <span className="tree-actions">
      <ActionButton disabled={!canAdd || busy} title="Add" onClick={onAdd}>
        +
      </ActionButton>
      <ActionButton
        disabled={!canDuplicate || busy}
        title="Duplicate"
        onClick={onDuplicate}
      >
        ⧉
      </ActionButton>
      <ActionButton
        disabled={!canDelete || busy}
        title="Delete"
        onClick={onDelete}
      >
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
  if (name === "actor") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="8" r="4" />
        <path d="M4.5 20c1.4-4 4-6 7.5-6s6.1 2 7.5 6" />
      </svg>
    );
  }
  if (name === "theme") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 4a8 8 0 1 0 0 16h1.4a2 2 0 0 0 1.5-3.3 1.7 1.7 0 0 1 1.3-2.7H18a6 6 0 0 0-6-10Z" />
        <circle cx="8.2" cy="10" r=".8" />
        <circle cx="11" cy="7.8" r=".8" />
        <circle cx="14.2" cy="9" r=".8" />
      </svg>
    );
  }
  if (name === "icon") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M7 4h10l3 3v10l-3 3H7l-3-3V7l3-3Z" />
        <path d="M9 12h6M12 9v6" />
      </svg>
    );
  }
  if (name === "status") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M5 8h14M7 12h2M11 12h2M15 12h2" />
        <rect x="4" y="5" width="16" height="12" rx="2.5" />
      </svg>
    );
  }
  if (name === "navigation") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <rect x="4" y="5" width="16" height="14" rx="2.5" />
        <path d="M8 16h8M8 12l2.5-2.5L13 12M16 9.5v5" />
      </svg>
    );
  }
  if (name === "device") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <rect x="7" y="3" width="10" height="18" rx="2.5" />
        <path d="M10.5 18h3" />
      </svg>
    );
  }
  if (name === "media") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <rect x="4" y="5" width="16" height="14" rx="2" />
        <path d="m7 16 3.2-3.2 2.3 2.3L15.5 12 20 16" />
        <circle cx="8.5" cy="9" r="1" />
      </svg>
    );
  }
  if (name === "render") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M5 5h14v10H5z" />
        <path d="M8 19h8M12 15v4M9 8l3 2 3-2" />
      </svg>
    );
  }
  if (name === "animation") {
    return (
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M4 12h3l2-5 4 10 2-5h5" />
        <path d="M4 5h16M4 19h16" />
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
  className,
  asRecord,
  onClick,
}: {
  tableId: string;
  recordId: string;
  activeTableId: string;
  selectedRecordIds: Record<string, string>;
  icon?: string;
  title: string;
  meta?: string;
  className?: string;
  asRecord?: boolean;
  onClick: () => void;
}) {
  const isRow = asRecord !== false;
  return (
    <button
      type="button"
      data-testid={`record-${recordId}`}
      className={[
        recordButtonClass(tableId, recordId, activeTableId, selectedRecordIds),
        isRow ? "project-tree-record" : null,
        isRow ? "project-tree-row" : null,
        !icon ? "project-tree-row-no-icon" : null,
        className,
      ]
        .filter(Boolean)
        .join(" ")
        .trim()}
      onClick={(event) => {
        event.stopPropagation();
        onClick();
      }}
    >
      {icon ? (
        <span className="tree-record-icon ui-glyph" aria-hidden="true">
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
  onDuplicateRecord,
  onDeleteRecord,
}: ProjectTreeProps) {
  const [browserTab, setBrowserTab] = useState<
    "" | "project" | "apps" | "data"
  >("");
  const [openDataTables, setOpenDataTables] = useState<Record<string, boolean>>(
    {},
  );
  const [openTreeBranches, setOpenTreeBranches] = useState<
    Record<string, boolean>
  >({});
  const tablesById = tableById(tables);
  const selectedProductionId = selectedRecordIds.productions;
  const productionEpisodes = [
    ...options.episodes.filter(
      (episode) => episode.productionId === selectedProductionId,
    ),
  ].sort((left, right) =>
    left.name.localeCompare(right.name, undefined, {
      numeric: true,
      sensitivity: "base",
    }),
  );
  const dataTables = tables.filter((table) =>
    PRODUCTION_DATA_TABLE_IDS.has(table.id),
  );
  const appRecords = recordsForSelectedProduction("apps");
  const moduleConfigRecords = recordsForSelectedProduction(
    "module_theme_configs",
  );
  const moduleInstanceRecords = records.module_instances ?? [];

  const cardLevelClass = (level: number) =>
    `project-tree-level-${Math.min(level, 4)}`;
  const rowLevelClass = (level: number) =>
    `project-tree-row-level-${Math.min(level, 4)}`;

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

  function branchKey(tableId: string, recordId: string) {
    return `${tableId}:${recordId}`;
  }

  function setBranchOpen(tableId: string, recordId: string, open: boolean) {
    const key = branchKey(tableId, recordId);
    setOpenTreeBranches((current) =>
      current[key] === open ? current : { ...current, [key]: open },
    );
  }

  function isBranchOpen(tableId: string, recordId: string) {
    return Boolean(openTreeBranches[branchKey(tableId, recordId)]);
  }

  function selectAndOpen(tableId: string, recordId: string) {
    select(tableId, recordId);
    setBranchOpen(tableId, recordId, true);
  }

  function confirmDelete(
    tableId:
      | "shots"
      | "icon_themes"
      | "status_bars"
      | "navigation_bars"
      | "themes"
      | "devices"
      | "render_presets",
    recordId: string,
    label: string,
  ) {
    if (window.confirm(`Delete “${label}”? This cannot be undone.`)) {
      onDeleteRecord(tableId, recordId);
    }
  }

  function toggleDataTable(tableId: string, tableRecords: AppRecord[]) {
    setOpenDataTables((current) => ({
      ...current,
      [tableId]: !current[tableId],
    }));
    const selectedRecordId = selectedRecordIds[tableId];
    const selectedRecordExists = tableRecords.some(
      (record) => record.id === selectedRecordId,
    );
    const recordToSelect = selectedRecordExists
      ? selectedRecordId
      : tableRecords[0]?.id;
    if (recordToSelect) {
      select(tableId, String(recordToSelect));
    }
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
        {productionEpisodes.length === 0 ? (
          <EmptyPanel>No episodes yet.</EmptyPanel>
        ) : (
          productionEpisodes.map((episode) => {
            const production = options.productions.find(
              (item) => item.id === selectedProductionId,
            );
            const shots = [
              ...options.shots.filter((shot) => shot.episodeId === episode.id),
            ].sort((left, right) =>
              shotRenderName(production, episode, left).localeCompare(
                shotRenderName(production, episode, right),
                undefined,
                { numeric: true, sensitivity: "base" },
              ),
            );
            return (
              <details
                key={episode.id}
                className={`project-tree-card project-tree-branch ${cardLevelClass(0)}`}
                data-tree-level="0"
                open={isBranchOpen("episodes", episode.id)}
                onToggle={(event) =>
                  setBranchOpen("episodes", episode.id, event.currentTarget.open)
                }
              >
                <summary className="project-tree-summary">
                  <TreeButton
                    tableId="episodes"
                    recordId={episode.id}
                    activeTableId={activeTableId}
                    selectedRecordIds={selectedRecordIds}
                    icon="episode"
                    title={episode.name}
                    meta={`${shots.length} shot${shots.length === 1 ? "" : "s"}`}
                    onClick={() => selectAndOpen("episodes", episode.id)}
                    asRecord
                    className={rowLevelClass(1)}
                  />
                  <TreeActions
                    canAdd
                    busy={busyAction}
                    onAdd={() =>
                      onCreateRecord("shots", { episodeId: episode.id })
                    }
                  />
                </summary>
                <div className="project-tree-children">
                  {shots.length === 0 ? (
                    <EmptyPanel>No shots yet.</EmptyPanel>
                  ) : (
                    shots.map((shot) => {
                      const screens = options.screenInstances.filter(
                        (instance) => instance.shotId === shot.id,
                      );
                      const renderName = shotRenderName(
                        production,
                        episode,
                        shot,
                      );
                      const durationFrames = shotDurationFromScreens(
                        shot,
                        screens,
                      );
                      return (
                        <details
                          key={shot.id}
                          className={`project-tree-card project-tree-branch ${cardLevelClass(1)}`}
                          data-tree-level="1"
                          open={isBranchOpen("shots", shot.id)}
                          onToggle={(event) =>
                            setBranchOpen("shots", shot.id, event.currentTarget.open)
                          }
                        >
                          <summary className="project-tree-summary">
                            <TreeButton
                              tableId="shots"
                              recordId={shot.id}
                              activeTableId={activeTableId}
                              selectedRecordIds={selectedRecordIds}
                              icon="shot"
                              title={renderName}
                              meta={`${durationFrames}f · ${screens.length} screen${screens.length === 1 ? "" : "s"}`}
                              onClick={() => selectAndOpen("shots", shot.id)}
                              asRecord
                              className={rowLevelClass(2)}
                            />
                            <TreeActions
                              canDuplicate
                              canDelete
                              busy={busyAction}
                              onDuplicate={() =>
                                onDuplicateRecord("shots", shot.id)
                              }
                              onDelete={() =>
                                confirmDelete("shots", shot.id, renderName)
                              }
                            />
                          </summary>
                          <div className="project-tree-children">
                            {screens.length === 0 ? (
                              <EmptyPanel>No screens yet.</EmptyPanel>
                            ) : (
                              screens.map((instance) => (
                                <div
                                  key={instance.id}
                                  className={`project-tree-card project-tree-branch project-tree-leaf ${cardLevelClass(2)}`}
                                  data-tree-level="2"
                                >
                                  <TreeButton
                                    tableId="screen_instances"
                                    recordId={instance.id}
                                    activeTableId={activeTableId}
                                    selectedRecordIds={selectedRecordIds}
                                    icon="screen"
                                    title={screenTitle(instance)}
                                    meta={`${instance.startFrame}–${instance.endFrame}f`}
                                    onClick={() =>
                                      select("screen_instances", instance.id)
                                    }
                                    asRecord
                                    className={rowLevelClass(3)}
                                  />
                                  {tablesById.get("module_instances") ? (
                                    <div className="project-tree-children compact">
                                      {moduleInstanceRecords
                                        .filter(
                                          (moduleInstance) =>
                                            moduleInstance.screen_instance_id ===
                                            instance.id,
                                        )
                                        .map((moduleInstance) => (
                                          <TreeButton
                                            key={moduleInstance.id}
                                            tableId="module_instances"
                                            recordId={moduleInstance.id}
                                            activeTableId={activeTableId}
                                            selectedRecordIds={
                                              selectedRecordIds
                                            }
                                            icon="module"
                                            title={String(
                                              moduleInstance.module_id ??
                                                "Module instance",
                                            )}
                                            meta="Module content"
                                            onClick={() =>
                                              select(
                                                "module_instances",
                                                moduleInstance.id,
                                              )
                                            }
                                            className={rowLevelClass(4)}
                                            asRecord
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
        {appRecords.length === 0 ? (
          <EmptyPanel>No apps yet.</EmptyPanel>
        ) : (
          appRecords.map((app) => {
            const configs = moduleConfigRecords.filter(
              (config) => config.app_id === app.id,
            );
            return (
              <details
                key={app.id}
                className={`project-tree-card project-tree-branch ${cardLevelClass(0)}`}
                data-tree-level="0"
                open={isBranchOpen("apps", app.id)}
                onToggle={(event) =>
                  setBranchOpen("apps", app.id, event.currentTarget.open)
                }
              >
                <summary className="project-tree-summary">
                  <TreeButton
                    tableId="apps"
                    recordId={app.id}
                    activeTableId={activeTableId}
                    selectedRecordIds={selectedRecordIds}
                    icon="app"
                    title={String(app.name ?? app.id)}
                    meta={String(app.app_type ?? "app")}
                    onClick={() => selectAndOpen("apps", app.id)}
                    className={rowLevelClass(1)}
                    asRecord
                  />
                </summary>
                <div className="project-tree-children">
                  {configs.length === 0 ? (
                    <EmptyPanel>No module configs yet.</EmptyPanel>
                  ) : (
                    configs.map((config) => (
                      <div
                        key={config.id}
                        className={`project-tree-card project-tree-leaf ${cardLevelClass(1)}`}
                        data-tree-level="1"
                      >
                        <TreeButton
                          tableId="module_theme_configs"
                          recordId={config.id}
                          activeTableId={activeTableId}
                          selectedRecordIds={selectedRecordIds}
                          icon="module"
                          title={String(config.name ?? "Module config")}
                          meta={String(config.module_id ?? "module")}
                          onClick={() =>
                            select("module_theme_configs", config.id)
                          }
                          className={rowLevelClass(2)}
                          asRecord
                        />
                      </div>
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
        {dataTables.map((table) => {
          const tableRecords = recordsForSelectedProduction(table.id);
          return (
            <details
              key={table.id}
              className={`project-tree-card project-tree-branch project-browser-data-table ${cardLevelClass(0)}`}
              data-tree-level="0"
              open={Boolean(openDataTables[table.id])}
              onToggle={(event) => {
                const isOpen = event.currentTarget.open;
                setOpenDataTables((current) =>
                  current[table.id] === isOpen
                    ? current
                    : { ...current, [table.id]: isOpen },
                );
              }}
            >
              <summary className="project-tree-summary">
                <button
                  type="button"
                  role="tab"
                  data-testid={`tab-${table.id}`}
                  className={`project-tree-record project-tree-row ${rowLevelClass(0)}`}
                  onClick={(event) => {
                    event.stopPropagation();
                    toggleDataTable(table.id, tableRecords);
                  }}
                >
                  <span className="tree-record-icon ui-glyph" aria-hidden="true">
                    <TreeIcon name={productionDataIcon(table.id)} />
                  </span>
                  <span className="tree-record-copy">
                    <strong>{table.label}</strong>
                    <small>
                      {tableRecords.length} record
                      {tableRecords.length === 1 ? "" : "s"}
                    </small>
                  </span>
                </button>
                {table.id === "icon_themes" ||
                table.id === "status_bars" ||
                table.id === "navigation_bars" ||
                table.id === "themes" ||
                table.id === "devices" ||
                table.id === "render_presets" ? (
                  <TreeActions
                    canAdd={Boolean(selectedProductionId)}
                    canDuplicate={Boolean(selectedRecordIds[table.id])}
                    canDelete={Boolean(selectedRecordIds[table.id])}
                    busy={busyAction}
                    onAdd={() =>
                      onCreateRecord(
                        table.id as
                          | "icon_themes"
                          | "status_bars"
                          | "navigation_bars"
                          | "themes"
                          | "devices"
                          | "render_presets",
                        {
                          productionId: selectedProductionId,
                        },
                      )
                    }
                    onDuplicate={() =>
                      onDuplicateRecord(
                        table.id as
                          | "icon_themes"
                          | "status_bars"
                          | "navigation_bars"
                          | "themes"
                          | "devices"
                          | "render_presets",
                        selectedRecordIds[table.id],
                      )
                    }
                    onDelete={() => {
                      const selected = tableRecords.find(
                        (record) => record.id === selectedRecordIds[table.id],
                      );
                      if (selected) {
                        confirmDelete(
                          table.id as
                            | "icon_themes"
                            | "status_bars"
                            | "navigation_bars"
                            | "themes"
                            | "devices"
                            | "render_presets",
                          selected.id,
                          recordTitle(table, selected),
                        );
                      }
                    }}
                  />
                ) : null}
              </summary>
              <div className="project-tree-children">
                {tableRecords.length === 0 ? (
                  <EmptyPanel>No records yet.</EmptyPanel>
                ) : (
                  tableRecords.map((record) => (
                    <div
                      key={record.id}
                      className={`project-tree-card project-tree-leaf project-tree-data-record-card ${cardLevelClass(1)}`}
                      data-tree-level="1"
                    >
                      <TreeButton
                        tableId={table.id}
                        recordId={record.id}
                        activeTableId={activeTableId}
                        selectedRecordIds={selectedRecordIds}
                        title={recordTitle(table, record)}
                        meta={String(record.id)}
                        onClick={() => select(table.id, record.id)}
                        className={rowLevelClass(1)}
                        asRecord
                      />
                    </div>
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
    actions?: ReactNode,
  ) {
    const active = browserTab === id;
    const isEditingProject =
      id === "project" && activeTableId === "productions";
    const handleOpen = () => {
      setBrowserTab(active ? "" : id);
      if (!active && id === "project" && selectedProductionId) {
        select("productions", selectedProductionId);
      }
    };
    return (
      <section
        className={`workspace-accordion-card ${active ? "active" : ""} ${
          isEditingProject ? "editing" : ""
        }`}
      >
        <button
          type="button"
          className="workspace-accordion-trigger"
          aria-expanded={active}
          onClick={handleOpen}
        >
          <span className="workspace-accordion-icon ui-glyph" aria-hidden="true">
            <TreeIcon name={icon} />
          </span>
          <span className="workspace-accordion-copy">
            <strong>{title}</strong>
            <small>{subtitle}</small>
          </span>
          {actions ? (
            <span
              className="workspace-accordion-actions"
              onClick={(event) => event.stopPropagation()}
            >
              {actions}
            </span>
          ) : null}
        </button>
        {active ? (
          <div className="workspace-accordion-body">{children}</div>
        ) : null}
      </section>
    );
  }

  return (
    <section className="project-browser-root">
      <div className="project-tree-view">
        <div className="workspace-accordions" aria-label="Workspace">
          {renderWorkspaceAccordion(
            "project",
            "project",
            "Project",
            "Episodes, shots, screens and modules",
            renderProjectTree(),
            <TreeActions
              canAdd={Boolean(selectedProductionId)}
              busy={busyAction}
              onAdd={() =>
                onCreateRecord("episodes", {
                  productionId: selectedProductionId,
                })
              }
            />,
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
      </div>
    </section>
  );
}
