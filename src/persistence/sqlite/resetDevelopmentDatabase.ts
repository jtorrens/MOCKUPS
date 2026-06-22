import { existsSync, unlinkSync } from "node:fs";
import { createDatabase } from "./createDatabase.js";
import { developmentDatabasePath } from "./paths.js";
import { seedExampleDataset } from "./seedExampleDataset.js";

if (existsSync(developmentDatabasePath)) {
  unlinkSync(developmentDatabasePath);
}

const database = createDatabase(developmentDatabasePath);
seedExampleDataset(database);
database.close();
console.log(`Reset and seeded example dataset at ${developmentDatabasePath}`);
