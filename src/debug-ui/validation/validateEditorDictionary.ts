import {
  allValueKindControlDefinitions,
  validateValueKindControlRegistry,
} from "../editor-ui/ValueKindControlRegistry.js";
import { ValueRegistry } from "../../domain/value-system/index.js";

const issues = validateValueKindControlRegistry();

if (issues.length) {
  console.error("Editor dictionary validation failed:");
  for (const issue of issues) {
    console.error(`- ${issue.message}`);
  }
  process.exit(1);
}

console.log("Editor dictionary validation OK");
console.log(`- value kinds: ${ValueRegistry.allKinds().length}`);
console.log(`- editor controls: ${allValueKindControlDefinitions().length}`);
