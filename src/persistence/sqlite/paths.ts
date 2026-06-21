import { resolve } from "node:path";

export const developmentDatabasePath = resolve(
  process.cwd(),
  "data/mockups-dev.sqlite",
);
