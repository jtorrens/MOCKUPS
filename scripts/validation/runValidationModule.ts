import { parseArgs } from "node:util";

import { checkDocumentationContracts } from "./checkDocumentationContracts.js";
import { checkGeneratedArtifacts } from "./checkGeneratedArtifacts.js";
import { checkValidationPipeline } from "./checkValidationPipeline.js";
import {
  createArchitectureValidationContext,
  reportArchitectureValidation,
} from "./validationContext.js";

const { positionals } = parseArgs({
  allowPositionals: true,
  strict: true,
});
const owner = positionals[0] ?? "";
const context = createArchitectureValidationContext();

switch (owner) {
  case "contracts":
    checkDocumentationContracts(context);
    break;
  case "generated":
    checkGeneratedArtifacts(context);
    break;
  case "pipeline":
    checkValidationPipeline(context);
    break;
  default:
    throw new Error(
      `Unknown validation owner '${owner}'. Expected contracts, generated or pipeline.`,
    );
}

reportArchitectureValidation(
  context,
  `Validation owner '${owner}' passed.`,
);
