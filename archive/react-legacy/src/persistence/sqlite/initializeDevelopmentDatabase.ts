import { createDatabase } from "./createDatabase.js";
import { developmentDatabasePath } from "./paths.js";

const database = createDatabase(developmentDatabasePath);
database.close();
console.log(`Initialized SQLite schema at ${developmentDatabasePath}`);
