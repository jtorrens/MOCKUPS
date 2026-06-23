import { createDatabase } from "../persistence/sqlite/createDatabase.js";
import { seedExampleDataset } from "../persistence/sqlite/seedExampleDataset.js";
import {
  createAppRecord,
  loadAppState,
  loadDebugPayload,
  updateAppRecord,
} from "./debugService.js";

type ProductionOption = {
  id: string;
  name: string;
};

type ShotOption = {
  id: string;
  productionId: string;
  episodeId?: string | null;
  ownerActorId?: string | null;
  name: string;
  durationFrames: number;
  fps: number;
};

type ScreenInstanceOption = {
  id: string;
  shotId: string;
  screenType: string;
  moduleId?: string;
  startFrame: number;
  endFrame: number;
  layerOrder: number;
};

type AppRecord = Record<string, unknown> & { id: string };

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

const database = createDatabase(":memory:");
try {
  seedExampleDataset(database);
  const state = loadAppState(database);
  const requiredTables = [
    "productions",
    "episodes",
    "shots",
    "screen_instances",
    "module_instances",
    "actors",
    "themes",
    "module_theme_configs",
    "devices",
    "device_states",
    "media_assets",
    "render_presets",
  ];
  assert(
    requiredTables.every((tableId) =>
      state.tables.some((table) => table.id === tableId),
    ),
    "App shell must expose all required core tabs",
  );

  const productions = state.options.productions as ProductionOption[];
  const episodes = state.options.episodes as {
    id: string;
    productionId: string;
    name: string;
  }[];
  const shots = state.options.shots as ShotOption[];
  const screenInstances =
    state.options.screenInstances as ScreenInstanceOption[];
  const moduleInstanceRecords = state.records.module_instances as AppRecord[];
  const moduleThemeConfigRecords = state.records
    .module_theme_configs as AppRecord[];

  const production = productions[0];
  const episode = episodes.find(
    (candidate) => candidate.productionId === production?.id,
  );
  const shot = shots.find(
    (candidate) => candidate.episodeId === episode?.id,
  );
  const screen = screenInstances.find(
    (candidate) =>
      candidate.shotId === shot?.id && candidate.moduleId === "core.chat",
  );
  assert(
    production && episode && shot && screen,
    "Seeded core.chat episode context must exist",
  );
  assert(
    shot.ownerActorId === "actor_alex",
    "Seeded shot must expose an owner actor",
  );
  const moduleThemeConfig = moduleThemeConfigRecords.find(
    (record) => record.module_id === "core.chat",
  );
  assert(
    moduleThemeConfig,
    "Seeded core.chat module theme config must be editable",
  );
  assert(
    state.inheritedJson.module_theme_configs?.[moduleThemeConfig.id]
      ?.tokens_json,
    "Module theme config tokens must expose inherited global theme tokens",
  );
  const payload = loadDebugPayload(database, {
    productionId: production.id,
    shotId: shot.id,
    screenInstanceId: screen.id,
    frame: Math.max(screen.startFrame, 210),
  });
  assert(payload.editable.moduleData, "Preview payload must include module data");
  assert(payload.resolvedScreen, "Preview payload must include resolved props");
  assert(payload.renderable, "Preview payload must include RenderableNode");

  let invalidJsonFailed = false;
  try {
    updateAppRecord(database, {
      tableId: "module_instances",
      recordId: `${screen.id}:module`,
      patch: { behavior_json: "{" },
    });
  } catch {
    invalidJsonFailed = true;
  }
  assert(invalidJsonFailed, "Malformed JSON must be rejected");

  const moduleRecord = moduleInstanceRecords.find(
    (record) => record.screen_instance_id === screen.id,
  );
  assert(moduleRecord, "Module instance app record must exist");
  const moduleConfig = {
    ...(moduleRecord.behavior_json as Record<string, unknown>),
    debugShowBounds: true,
  };
  updateAppRecord(database, {
    tableId: "module_instances",
    recordId: moduleRecord.id,
    patch: { behavior_json: moduleConfig },
  });
  const savedPayload = loadDebugPayload(database, {
    productionId: production.id,
    shotId: shot.id,
    screenInstanceId: screen.id,
    frame: Math.max(screen.startFrame, 210),
  });
  assert(
    (savedPayload.resolvedScreen as { props?: { debugShowBounds?: boolean } })
      .props?.debugShowBounds === true,
    "Valid autosave must persist and re-resolve preview props",
  );

  const createdProduction = createAppRecord(database, {
    tableId: "productions",
    name: "Smoke Production",
  });
  const createdProductionId = String(createdProduction.record.id);
  const createdProductions = createdProduction.state.options
    .productions as ProductionOption[];
  assert(
    createdProductionId &&
      createdProductions.some((candidate) => candidate.id === createdProductionId),
    "Project browser must be able to create productions",
  );
  const createdEpisode = createAppRecord(database, {
    tableId: "episodes",
    parent: { productionId: createdProductionId },
    name: "Smoke Episode",
  });
  const createdEpisodeId = String(createdEpisode.record.id);
  const createdEpisodes = createdEpisode.state.options.episodes as {
    id: string;
  }[];
  assert(
    createdEpisode.record.production_id === createdProductionId &&
      createdEpisodes.some((candidate) => candidate.id === createdEpisodeId),
    "Project browser must be able to create episodes under a production",
  );
  const createdShot = createAppRecord(database, {
    tableId: "shots",
    parent: { episodeId: createdEpisodeId },
    name: "Smoke Shot",
  });
  const createdShotId = String(createdShot.record.id);
  const createdShots = createdShot.state.options.shots as ShotOption[];
  assert(
    createdShot.record.episode_id === createdEpisodeId &&
      createdShots.some((candidate) => candidate.id === createdShotId),
    "Project browser must be able to create shots under an episode",
  );

  console.log("✓ app API exposes required core tabs");
  console.log("✓ seeded production, shot and core.chat instance are selectable");
  console.log("✓ project browser can create productions, episodes, and shots");
  console.log("✓ malformed JSON update is rejected");
  console.log("✓ valid JSON update persists to SQLite");
  console.log("✓ preview payload re-resolves after save");
  console.log("Core app shell service check succeeded.");
} finally {
  database.close();
}
