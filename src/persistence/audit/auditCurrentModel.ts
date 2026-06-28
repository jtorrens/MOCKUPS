import { existsSync } from "node:fs";
import Database from "better-sqlite3";
import { ChatModuleDataSchema, ComponentClassSchema } from "../../domain/schemas/index.js";
import { createDatabase, type SQLiteDatabase } from "../sqlite/createDatabase.js";
import { developmentDatabasePath } from "../sqlite/paths.js";
import { seedExampleDataset } from "../sqlite/seedExampleDataset.js";

type Severity = "pass" | "warn" | "fail";

interface AuditIssue {
  severity: Severity;
  scope: string;
  message: string;
}

interface JsonColumn {
  table: string;
  column: string;
  nullable?: boolean;
}

const EXPECTED_TABLES = [
  "productions",
  "media_assets",
  "production_fonts",
  "palette_colors",
  "episodes",
  "icon_themes",
  "status_bars",
  "navigation_bars",
  "component_classes",
  "themes",
  "module_theme_configs",
  "devices",
  "device_states",
  "actors",
  "apps",
  "animation_presets",
  "render_presets",
  "shots",
  "screen_instances",
  "module_instances",
  "screen_events",
  "conversations",
  "conversation_participants",
  "messages",
  "notifications",
  "calls",
  "data_sources",
] as const;

const LEGACY_EMPTY_TABLES = [
  "conversations",
  "conversation_participants",
  "messages",
] as const;

const JSON_COLUMNS: JsonColumn[] = [
  { table: "productions", column: "settings_json", nullable: true },
  { table: "productions", column: "metadata_json", nullable: true },
  { table: "media_assets", column: "dimensions_json", nullable: true },
  { table: "media_assets", column: "metadata_json", nullable: true },
  { table: "production_fonts", column: "files_json" },
  { table: "production_fonts", column: "metadata_json", nullable: true },
  { table: "palette_colors", column: "metadata_json", nullable: true },
  { table: "episodes", column: "metadata_json", nullable: true },
  { table: "icon_themes", column: "mapping_json" },
  { table: "icon_themes", column: "metadata_json", nullable: true },
  { table: "status_bars", column: "config_json" },
  { table: "status_bars", column: "metadata_json", nullable: true },
  { table: "navigation_bars", column: "config_json" },
  { table: "navigation_bars", column: "metadata_json", nullable: true },
  { table: "component_classes", column: "tokens_json" },
  { table: "component_classes", column: "metadata_json", nullable: true },
  { table: "themes", column: "tokens_json" },
  { table: "module_theme_configs", column: "tokens_json" },
  { table: "module_theme_configs", column: "metadata_json", nullable: true },
  { table: "devices", column: "metrics_json" },
  { table: "device_states", column: "state_json" },
  { table: "actors", column: "metadata_json", nullable: true },
  { table: "apps", column: "config_json", nullable: true },
  { table: "apps", column: "metadata_json", nullable: true },
  { table: "animation_presets", column: "parameters_json" },
  { table: "render_presets", column: "codec_json", nullable: true },
  { table: "render_presets", column: "color_json", nullable: true },
  { table: "render_presets", column: "quality_json", nullable: true },
  { table: "render_presets", column: "export_json", nullable: true },
  { table: "shots", column: "canvas_json", nullable: true },
  { table: "shots", column: "metadata_json", nullable: true },
  { table: "screen_instances", column: "device_state_json", nullable: true },
  { table: "screen_instances", column: "data_ref_json", nullable: true },
  { table: "screen_instances", column: "module_data_json", nullable: true },
  { table: "screen_instances", column: "module_config_json", nullable: true },
  { table: "screen_instances", column: "module_tokens_override_json", nullable: true },
  { table: "screen_instances", column: "transform_json" },
  { table: "screen_instances", column: "props_json" },
  { table: "screen_instances", column: "transition_in_json", nullable: true },
  { table: "screen_instances", column: "transition_out_json", nullable: true },
  { table: "module_instances", column: "content_json" },
  { table: "module_instances", column: "behavior_json" },
  { table: "module_instances", column: "animation_json" },
  { table: "module_instances", column: "metadata_json", nullable: true },
  { table: "screen_events", column: "payload_json" },
  { table: "conversations", column: "metadata_json", nullable: true },
  { table: "conversation_participants", column: "metadata_json", nullable: true },
  { table: "messages", column: "style_override_json" },
  { table: "messages", column: "animation_override_json" },
  { table: "messages", column: "layout_override_json" },
  { table: "messages", column: "metadata_json" },
  { table: "notifications", column: "payload_json" },
  { table: "notifications", column: "style_override_json", nullable: true },
  { table: "notifications", column: "metadata_json", nullable: true },
  { table: "calls", column: "payload_json", nullable: true },
  { table: "calls", column: "metadata_json", nullable: true },
  { table: "data_sources", column: "data_json" },
  { table: "data_sources", column: "config_json", nullable: true },
  { table: "data_sources", column: "metadata_json", nullable: true },
];

function add(issues: AuditIssue[], severity: Severity, scope: string, message: string) {
  issues.push({ severity, scope, message });
}

function tableNames(database: SQLiteDatabase) {
  return new Set(
    (
      database
        .prepare("SELECT name FROM sqlite_master WHERE type = 'table'")
        .all() as { name: string }[]
    ).map((row) => row.name),
  );
}

function hasColumn(database: SQLiteDatabase, table: string, column: string) {
  return (
    database.pragma(`table_info(${table})`) as { name: string }[]
  ).some((entry) => entry.name === column);
}

function parseJsonValue(raw: unknown, scope: string, issues: AuditIssue[]) {
  if (typeof raw !== "string") {
    add(issues, "fail", scope, "Expected JSON TEXT.");
    return undefined;
  }
  try {
    return JSON.parse(raw) as unknown;
  } catch (error) {
    add(
      issues,
      "fail",
      scope,
      `Invalid JSON: ${error instanceof Error ? error.message : String(error)}`,
    );
    return undefined;
  }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function asObject(value: unknown) {
  return isObject(value) ? value : {};
}

function auditJsonColumns(
  database: SQLiteDatabase,
  tables: Set<string>,
  issues: AuditIssue[],
) {
  let checked = 0;
  for (const { table, column, nullable } of JSON_COLUMNS) {
    if (!tables.has(table)) continue;
    if (!hasColumn(database, table, column)) {
      add(issues, "fail", `${table}.${column}`, "Expected JSON column is missing.");
      continue;
    }
    const rows = database
      .prepare(`SELECT id, ${column} AS value FROM ${table}`)
      .all() as { id: string; value: unknown }[];
    for (const row of rows) {
      if ((row.value === null || row.value === undefined) && nullable) {
        continue;
      }
      const parsed = parseJsonValue(
        row.value,
        `${table}.${row.id}.${column}`,
        issues,
      );
      if (parsed !== undefined) checked += 1;
    }
  }
  add(issues, "pass", "json", `${checked} JSON values parsed.`);
}

function auditLegacyTables(database: SQLiteDatabase, tables: Set<string>, issues: AuditIssue[]) {
  for (const table of LEGACY_EMPTY_TABLES) {
    if (!tables.has(table)) continue;
    const row = database.prepare(`SELECT COUNT(*) AS total FROM ${table}`).get() as {
      total: number;
    };
    if (row.total === 0) {
      add(issues, "pass", table, "Legacy table exists but has no rows.");
    } else {
      add(
        issues,
        "fail",
        table,
        `Legacy table must be empty for the current chat model; found ${row.total} rows.`,
      );
    }
  }
}

function auditCoreChatModuleTheme(database: SQLiteDatabase, issues: AuditIssue[]) {
  const rows = database
    .prepare(
      `SELECT id, tokens_json
       FROM module_theme_configs
       WHERE module_id = 'core.chat'`,
    )
    .all() as { id: string; tokens_json: string }[];
  for (const row of rows) {
    const tokens = parseJsonValue(
      row.tokens_json,
      `module_theme_configs.${row.id}.tokens_json`,
      issues,
    );
    if (!isObject(tokens)) continue;
    const modes = asObject(tokens.modes);
    for (const mode of ["light", "dark"] as const) {
      const modeRoot = asObject(modes[mode]);
      const chatBubbles = asObject(modeRoot.chatBubbles);
      if ("contentMetaGap" in chatBubbles) {
        add(
          issues,
          "fail",
          `module_theme_configs.${row.id}.modes.${mode}.chatBubbles.contentMetaGap`,
          "contentMetaGap is a single module design value and must not exist inside mode overrides.",
        );
      }
      if ("cursor" in modeRoot) {
        add(
          issues,
          "fail",
          `module_theme_configs.${row.id}.modes.${mode}.cursor`,
          "Cursor config is theme/component-owned and must not live in module theme mode overrides.",
        );
      }
      const header = asObject(modeRoot.header);
      for (const key of [
        "avatarCornerRadius",
        "avatarBorderWidth",
        "avatarBorderColor",
        "avatarShadow",
      ]) {
        if (key in header) {
          add(
            issues,
            "fail",
            `module_theme_configs.${row.id}.modes.${mode}.header.${key}`,
            "Header avatar presentation belongs to component classes, not module theme mode overrides.",
          );
        }
      }
      if ("shadow" in chatBubbles) {
        add(
          issues,
          "fail",
          `module_theme_configs.${row.id}.modes.${mode}.chatBubbles.shadow`,
          "Bubble shadow config must use shadowEnabled plus theme shadows, not embedded shadow tokens.",
        );
      }
    }
    if ("avatars" in tokens) {
      add(
        issues,
        "fail",
        `module_theme_configs.${row.id}.avatars`,
        "Legacy avatars group must be migrated into header/chatBubbles/component classes.",
      );
    }
    if ("cursor" in tokens) {
      add(
        issues,
        "fail",
        `module_theme_configs.${row.id}.cursor`,
        "Cursor config is theme/component-owned and must not live in module theme root.",
      );
    }
    const rootChatBubbles = asObject(tokens.chatBubbles);
    if ("shadow" in rootChatBubbles) {
      add(
        issues,
        "fail",
        `module_theme_configs.${row.id}.chatBubbles.shadow`,
        "Bubble shadow config must use shadowEnabled plus theme shadows, not embedded shadow tokens.",
      );
    }
  }
  add(issues, "pass", "module_theme_configs", `${rows.length} core.chat module theme config rows audited.`);
}

function auditModuleInstances(database: SQLiteDatabase, issues: AuditIssue[]) {
  const rows = database
    .prepare(
      `SELECT id, module_id, module_schema_version, content_json
       FROM module_instances
       WHERE module_id = 'core.chat' AND module_schema_version = 1`,
    )
    .all() as {
      id: string;
      module_id: string;
      module_schema_version: number;
      content_json: string;
    }[];
  for (const row of rows) {
    const content = parseJsonValue(
      row.content_json,
      `module_instances.${row.id}.content_json`,
      issues,
    );
    if (!isObject(content)) continue;
    const parsed = ChatModuleDataSchema.safeParse(content);
    if (!parsed.success) {
      add(
        issues,
        "fail",
        `module_instances.${row.id}.content_json`,
        `Does not match ChatModuleDataSchema: ${parsed.error.issues
          .map((issue) => `${issue.path.join(".")}: ${issue.message}`)
          .join("; ")}`,
      );
      continue;
    }
    if ("participants" in content) {
      add(
        issues,
        "fail",
        `module_instances.${row.id}.content_json.participants`,
        "Participants are legacy for core.chat content; messages should reference actors directly.",
      );
    }
    const rawMessages = Array.isArray(content.messages) ? content.messages : [];
    parsed.data.messages.forEach((message, index) => {
      const rawMessage = rawMessages[index];
      if (isObject(rawMessage) && rawMessage.startFrame !== undefined) {
        add(
          issues,
          "fail",
          `module_instances.${row.id}.content_json.messages[${index}].startFrame`,
          "Legacy absolute startFrame must be migrated to delayAfterPreviousFrames.",
        );
      }
      if (!message.id) {
        add(
          issues,
          "fail",
          `module_instances.${row.id}.content_json.messages[${index}].id`,
          "Messages require stable ids independent from order.",
        );
      }
    });
  }
  add(issues, "pass", "module_instances", `${rows.length} core.chat module instance rows audited.`);
}

function auditComponentClasses(database: SQLiteDatabase, issues: AuditIssue[]) {
  const rows = database
    .prepare("SELECT * FROM component_classes")
    .all() as Record<string, unknown>[];
  for (const row of rows) {
    const tokens = parseJsonValue(
      row.tokens_json,
      `component_classes.${String(row.id)}.tokens_json`,
      issues,
    );
    const metadata =
      row.metadata_json === null || row.metadata_json === undefined
        ? undefined
        : parseJsonValue(
            row.metadata_json,
            `component_classes.${String(row.id)}.metadata_json`,
            issues,
          );
    const parsed = ComponentClassSchema.safeParse({
      ...row,
      tokens_json: tokens,
      ...(metadata !== undefined ? { metadata_json: metadata } : {}),
    });
    if (!parsed.success) {
      add(
        issues,
        "fail",
        `component_classes.${String(row.id)}`,
        `Does not match ComponentClassSchema: ${parsed.error.issues
          .map((issue) => `${issue.path.join(".")}: ${issue.message}`)
          .join("; ")}`,
      );
    }
  }
  add(issues, "pass", "component_classes", `${rows.length} component class rows audited.`);
}

function auditDebugColorSentinel(database: SQLiteDatabase, issues: AuditIssue[]) {
  const paletteRows = database
    .prepare(
      `SELECT id, metadata_json
       FROM palette_colors
       WHERE token = 'debug_red'`,
    )
    .all() as { id: string; metadata_json: string | null }[];
  if (paletteRows.length === 0) {
    add(issues, "fail", "palette_colors.debug_red", "Protected debug color is missing.");
  } else {
    for (const row of paletteRows) {
      try {
        const metadata = row.metadata_json
          ? (JSON.parse(row.metadata_json) as Record<string, unknown>)
          : {};
        if (metadata.protected === true && metadata.hiddenFromPickers === true) {
          add(
            issues,
            "pass",
            `palette_colors.${row.id}`,
            "debug_red is protected and hidden from normal pickers.",
          );
        } else {
          add(
            issues,
            "fail",
            `palette_colors.${row.id}`,
            "debug_red must be protected and hiddenFromPickers.",
          );
        }
      } catch {
        add(issues, "fail", `palette_colors.${row.id}`, "debug_red metadata is invalid JSON.");
      }
    }
  }

  const themeRows = database
    .prepare("SELECT id, tokens_json FROM themes ORDER BY id")
    .all() as { id: string; tokens_json: string }[];
  for (const row of themeRows) {
    try {
      const tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
      const rootColors =
        tokens.colors && typeof tokens.colors === "object" && !Array.isArray(tokens.colors)
          ? (tokens.colors as Record<string, unknown>)
          : {};
      const modes =
        tokens.modes && typeof tokens.modes === "object" && !Array.isArray(tokens.modes)
          ? (tokens.modes as Record<string, unknown>)
          : {};
      const hasRootDebug = rootColors["debug.red"] === "debug_red";
      const hasModeDebug = ["light", "dark"].every((mode) => {
        const modeRoot =
          modes[mode] && typeof modes[mode] === "object" && !Array.isArray(modes[mode])
            ? (modes[mode] as Record<string, unknown>)
            : {};
        const modeColors =
          modeRoot.colors &&
          typeof modeRoot.colors === "object" &&
          !Array.isArray(modeRoot.colors)
            ? (modeRoot.colors as Record<string, unknown>)
            : {};
        return modeColors["debug.red"] === "debug_red";
      });
      add(
        issues,
        hasRootDebug && hasModeDebug ? "pass" : "fail",
        `themes.${row.id}.debug.red`,
        hasRootDebug && hasModeDebug
          ? "Theme exposes debug.red in root/light/dark color tokens."
          : "Theme must expose debug.red in root/light/dark color tokens.",
      );
    } catch {
      add(issues, "fail", `themes.${row.id}.debug.red`, "Theme tokens JSON is invalid.");
    }
  }
}

function auditThemeNotificationColors(database: SQLiteDatabase, issues: AuditIssue[]) {
  const themeRows = database
    .prepare("SELECT id, tokens_json FROM themes ORDER BY id")
    .all() as { id: string; tokens_json: string }[];
  for (const row of themeRows) {
    try {
      const tokens = JSON.parse(row.tokens_json) as Record<string, unknown>;
      const rootNotifications = isObject(tokens.notifications)
        ? tokens.notifications
        : {};
      for (const legacyField of ["background", "titleColor", "bodyColor"]) {
        if (legacyField in rootNotifications) {
          add(
            issues,
            "fail",
            `themes.${row.id}.notifications.${legacyField}`,
            "Notification colors are mode-specific and must live under modes.light/dark.notifications.",
          );
        }
      }
    } catch {
      add(issues, "fail", `themes.${row.id}.notifications`, "Theme tokens JSON is invalid.");
    }
  }
}

function auditChromeBarConfigs(database: SQLiteDatabase, issues: AuditIssue[]) {
  const statusRows = database
    .prepare("SELECT id, config_json FROM status_bars ORDER BY id")
    .all() as { id: string; config_json: string }[];
  for (const row of statusRows) {
    try {
      const config = JSON.parse(row.config_json) as Record<string, unknown>;
      if (!Array.isArray(config.items)) {
        add(
          issues,
          "fail",
          `status_bars.${row.id}.config_json.items`,
          "Status bars must use canonical items[] configuration.",
        );
      }
      for (const legacyKey of ["time", "carrier", "signal", "wifi", "battery", "focus"]) {
        if (legacyKey in config) {
          add(
            issues,
            "fail",
            `status_bars.${row.id}.config_json.${legacyKey}`,
            "Legacy status bar item groups must be migrated into items[].",
          );
        }
      }
    } catch {
      add(issues, "fail", `status_bars.${row.id}.config_json`, "Status bar config JSON is invalid.");
    }
  }

  const navigationRows = database
    .prepare("SELECT id, config_json FROM navigation_bars ORDER BY id")
    .all() as { id: string; config_json: string }[];
  for (const row of navigationRows) {
    try {
      const config = JSON.parse(row.config_json) as Record<string, unknown>;
      add(
        issues,
        Array.isArray(config.items) ? "pass" : "fail",
        `navigation_bars.${row.id}.config_json.items`,
        Array.isArray(config.items)
          ? "Navigation bar uses canonical items[] configuration."
          : "Navigation bars must use canonical items[] configuration.",
      );
    } catch {
      add(
        issues,
        "fail",
        `navigation_bars.${row.id}.config_json`,
        "Navigation bar config JSON is invalid.",
      );
    }
  }
}

function auditForeignKeys(database: SQLiteDatabase, issues: AuditIssue[]) {
  const failures = database.pragma("foreign_key_check") as unknown[];
  if (failures.length === 0) {
    add(issues, "pass", "foreign_keys", "No broken foreign keys.");
  } else {
    add(
      issues,
      "fail",
      "foreign_keys",
      `foreign_key_check returned ${failures.length} failures: ${JSON.stringify(failures)}`,
    );
  }
}

function auditDatabase(database: SQLiteDatabase, label: string): AuditIssue[] {
  const issues: AuditIssue[] = [];
  const tables = tableNames(database);
  for (const table of EXPECTED_TABLES) {
    if (tables.has(table)) {
      add(issues, "pass", `schema.${table}`, "Table exists.");
    } else {
      add(issues, "fail", `schema.${table}`, "Expected table is missing.");
    }
  }
  const version = Number(database.pragma("user_version", { simple: true }));
  if (version === 32) {
    add(issues, "pass", "schema.user_version", "SQLite user_version is 32.");
  } else {
    add(
      issues,
      "fail",
      "schema.user_version",
      `Expected SQLite user_version 32, got ${version}.`,
    );
  }
  auditJsonColumns(database, tables, issues);
  auditForeignKeys(database, issues);
  if (tables.has("palette_colors") && tables.has("themes")) {
    auditDebugColorSentinel(database, issues);
  }
  if (tables.has("themes")) {
    auditThemeNotificationColors(database, issues);
  }
  if (tables.has("status_bars") && tables.has("navigation_bars")) {
    auditChromeBarConfigs(database, issues);
  }
  auditLegacyTables(database, tables, issues);
  if (tables.has("module_theme_configs")) {
    auditCoreChatModuleTheme(database, issues);
  }
  if (tables.has("module_instances")) {
    auditModuleInstances(database, issues);
  }
  if (tables.has("component_classes")) {
    auditComponentClasses(database, issues);
  }
  return issues.map((issue) => ({
    ...issue,
    scope: `${label}.${issue.scope}`,
  }));
}

function printIssues(issues: AuditIssue[]) {
  for (const issue of issues) {
    const prefix =
      issue.severity === "pass"
        ? "PASS"
        : issue.severity === "warn"
          ? "WARN"
          : "FAIL";
    console.log(`${prefix} ${issue.scope}: ${issue.message}`);
  }
  const failCount = issues.filter((issue) => issue.severity === "fail").length;
  const warnCount = issues.filter((issue) => issue.severity === "warn").length;
  const passCount = issues.filter((issue) => issue.severity === "pass").length;
  console.log(
    `Current-model audit completed: ${passCount} pass, ${warnCount} warn, ${failCount} fail.`,
  );
  if (failCount > 0) {
    process.exitCode = 1;
  }
}

const allIssues: AuditIssue[] = [];

const seededDatabase = createDatabase(":memory:");
try {
  seedExampleDataset(seededDatabase);
  allIssues.push(...auditDatabase(seededDatabase, "seed"));
} finally {
  seededDatabase.close();
}

if (existsSync(developmentDatabasePath)) {
  const developmentDatabase = new Database(developmentDatabasePath, {
    readonly: true,
    fileMustExist: true,
  });
  try {
    allIssues.push(...auditDatabase(developmentDatabase, "dev"));
  } finally {
    developmentDatabase.close();
  }
} else {
  add(
    allIssues,
    "warn",
    "dev",
    `Development database not found at ${developmentDatabasePath}; skipped.`,
  );
}

printIssues(allIssues);
