import {
  regenerateIntegratedComponentScaffoldArtifacts,
} from "../src/development-scaffolding/componentScaffoldArtifacts.js";

const result = regenerateIntegratedComponentScaffoldArtifacts(process.cwd());
console.log(JSON.stringify(result, null, 2));
