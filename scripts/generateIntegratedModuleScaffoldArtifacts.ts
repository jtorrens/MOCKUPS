import { regenerateIntegratedModuleScaffoldArtifacts } from
  "../src/development-scaffolding/moduleScaffoldArtifacts.js";

console.log(JSON.stringify(
  regenerateIntegratedModuleScaffoldArtifacts(process.cwd()),
  null,
  2,
));
