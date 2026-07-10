import { createDatabase } from "./createDatabase.js";
import { developmentDatabasePath } from "./paths.js";
import { seedExampleDataset } from "./seedExampleDataset.js";

const database = createDatabase(developmentDatabasePath);
const existingProductions = (
  database.prepare("SELECT COUNT(*) AS total FROM productions").get() as {
    total: number;
  }
).total;

if (existingProductions === 0) {
  seedExampleDataset(database);
  console.log(`Seeded example dataset at ${developmentDatabasePath}`);
} else {
  console.log(
    `Skipped seed: ${developmentDatabasePath} already contains ${existingProductions} production(s). Use npm run db:reset to overwrite it.`,
  );
}

database.close();
