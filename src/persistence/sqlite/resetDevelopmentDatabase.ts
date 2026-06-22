import { createDatabase } from "./createDatabase.js";
import { developmentDatabasePath } from "./paths.js";
import { seedExampleDataset } from "./seedExampleDataset.js";

const database = createDatabase(developmentDatabasePath);
seedExampleDataset(database);
database.close();
console.log(`Reset and seeded example dataset at ${developmentDatabasePath}`);
