import { mkdtempSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { createDatabase } from "../persistence/sqlite/createDatabase.js";
import { seedExampleDataset } from "../persistence/sqlite/seedExampleDataset.js";
import {
  loadDebugPayload,
  updateAppRecord,
} from "./debugService.js";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

const tempDir = mkdtempSync(join(tmpdir(), "mockups-persistence-"));
const databasePath = join(tempDir, "persistence-check.sqlite");

let database = createDatabase(databasePath);
seedExampleDataset(database);

const originalShot = database
  .prepare("SELECT name FROM shots WHERE id = ?")
  .get("shot_lock_to_chat") as { name: string };
const scalarName = `${originalShot.name} persisted`;

updateAppRecord(database, {
  tableId: "shots",
  recordId: "shot_lock_to_chat",
  patch: { name: scalarName },
});

const screenRecord = database
  .prepare("SELECT module_config_json FROM screen_instances WHERE id = ?")
  .get("screen_instance_chat") as { module_config_json: string };
const moduleConfig = JSON.parse(screenRecord.module_config_json) as Record<
  string,
  unknown
>;
updateAppRecord(database, {
  tableId: "screen_instances",
  recordId: "screen_instance_chat",
  patch: {
    module_config_json: {
      ...moduleConfig,
      showKeyboard: true,
      debugShowBounds: true,
    },
  },
});

database.close();

database = createDatabase(databasePath);
try {
  const persistedShot = database
    .prepare("SELECT name FROM shots WHERE id = ?")
    .get("shot_lock_to_chat") as { name: string };
  assert(
    persistedShot.name === scalarName,
    "Scalar edit did not persist after SQLite reopen",
  );

  const persistedScreen = database
    .prepare("SELECT module_config_json FROM screen_instances WHERE id = ?")
    .get("screen_instance_chat") as { module_config_json: string };
  const persistedConfig = JSON.parse(
    persistedScreen.module_config_json,
  ) as Record<string, unknown>;
  assert(
    persistedConfig.showKeyboard === true &&
      persistedConfig.debugShowBounds === true,
    "JSON edit did not persist after SQLite reopen",
  );

  const payload = loadDebugPayload(database, {
    productionId: "production_demo",
    shotId: "shot_lock_to_chat",
    screenInstanceId: "screen_instance_chat",
    frame: 210,
  });
  const props = payload.resolvedScreen as {
    props?: { showKeyboard?: boolean; debugShowBounds?: boolean };
  };
  assert(
    props.props?.showKeyboard === true &&
      props.props.debugShowBounds === true,
    "Preview did not use persisted module_config_json values",
  );

  console.log("✓ scalar edit persisted after SQLite reopen");
  console.log("✓ JSON edit persisted after SQLite reopen");
  console.log("✓ preview payload uses persisted SQLite values");
  console.log(`Persistence check database: ${databasePath}`);
} finally {
  database.close();
}
