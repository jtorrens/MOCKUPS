import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

export type ArchitectureValidationContext = {
  root: string;
  violations: string[];
  resolveRepositoryPath(relativePath: string): {
    fullPath: string;
    normalizedPath: string;
  };
  readText(relativePath: string): string;
  addViolation(filePath: string, message: string): void;
  assertDocumentContains(
    relativePath: string,
    term: string,
    message: string,
  ): void;
};

export function createArchitectureValidationContext(
  root = process.cwd(),
): ArchitectureValidationContext {
  const violations: string[] = [];

  function resolveRepositoryPath(relativePath: string) {
    if (path.isAbsolute(relativePath) || path.win32.isAbsolute(relativePath)) {
      throw new Error(`Absolute repository paths are prohibited: ${relativePath}`);
    }
    const normalizedPath = path.posix.normalize(relativePath.replace(/\\/g, "/"))
      .replace(/^(?:\.\/)+/, "");
    if (!normalizedPath
        || normalizedPath === ".."
        || normalizedPath.startsWith("../")) {
      throw new Error(`Repository path escapes are prohibited: ${relativePath}`);
    }
    if (normalizedPath === "docs/old" || normalizedPath.startsWith("docs/old/")) {
      throw new Error(`Historical archive access is prohibited: ${normalizedPath}`);
    }
    const fullPath = path.resolve(root, ...normalizedPath.split("/"));
    if (fullPath !== root && !fullPath.startsWith(`${root}${path.sep}`)) {
      throw new Error(`Repository path escapes are prohibited: ${relativePath}`);
    }
    return { fullPath, normalizedPath };
  }

  function readText(relativePath: string) {
    const { fullPath } = resolveRepositoryPath(relativePath);
    return readFileSync(fullPath, "utf8");
  }

  function addViolation(filePath: string, message: string) {
    violations.push(`${filePath}: ${message}`);
  }

  function assertDocumentContains(
    relativePath: string,
    term: string,
    message: string,
  ) {
    const source = readText(relativePath);
    if (!source.includes(term)) {
      addViolation(relativePath, message);
    }
  }

  return {
    root,
    violations,
    resolveRepositoryPath,
    readText,
    addViolation,
    assertDocumentContains,
  };
}

export function repositoryFileExists(
  context: ArchitectureValidationContext,
  relativePath: string,
) {
  try {
    return existsSync(context.resolveRepositoryPath(relativePath).fullPath);
  } catch (error) {
    context.addViolation(
      "scripts/validation/validationContext.ts",
      error instanceof Error
        ? error.message
        : `Invalid repository path: ${relativePath}`,
    );
    return false;
  }
}

export function reportArchitectureValidation(
  context: ArchitectureValidationContext,
  successMessage: string,
) {
  if (context.violations.length > 0) {
    console.error("Architecture validation failed:");
    for (const violation of context.violations) {
      console.error(`- ${violation}`);
    }
    process.exitCode = 1;
    return;
  }
  console.log(successMessage);
}
