import path from "node:path";

export const validationDatabaseEnvironmentVariable =
  "MOCKUPS_VALIDATION_DATABASE";

export function parityDatabasePath(repositoryRoot = process.cwd()) {
  const configured = process.env[validationDatabaseEnvironmentVariable]?.trim();
  return configured
    ? path.resolve(configured)
    : path.join(repositoryRoot, "data", "mockups.sqlite");
}
