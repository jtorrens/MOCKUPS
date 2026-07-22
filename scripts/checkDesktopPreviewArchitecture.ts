import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";
import Database from "better-sqlite3";
import {
  desktopPreviewComponents,
  type DesktopPreviewComponentManifestEntry,
} from "../src/desktop-preview/desktopPreviewComponents.js";
import { componentRenderableFactories } from "../src/desktop-preview/componentClassRenderableRegistry.js";
import {
  desktopPreviewModules,
  type DesktopPreviewModuleManifestEntry,
} from "../src/desktop-preview/desktopPreviewModules.js";
import { moduleRenderableFactories } from "../src/desktop-preview/moduleRenderableRegistry.js";
import {
  approximateTextWidth,
  approximateWrappedTextLines,
} from "../src/desktop-preview/previewTextHelpers.js";
import { renderableNodeTypes } from "../src/visual/renderable/types.js";

const root = process.cwd();
const previewRoot = path.join(root, "src", "desktop-preview");

const violations: string[] = [];
const retiredTimeFields = [
  "fadeFrames",
  "cursorBlinkFrames",
  "blinkFrames",
  "motionTimeSeconds",
  "textAnimationTimeSeconds",
  "composerTransitionTimeSeconds",
];
const desktopPreviewPaintNodeTypes = new Set([
  "group",
  "icon",
  "image",
  "path",
  "surface",
  "text",
]);
const forbiddenDesktopPreviewNodeTypes = new Set([
  "status_bar",
  "status_bar_zone",
  "status_bar_item",
  "navigation_bar",
  "navigation_bar_zone",
  "navigation_bar_item",
  "navigation_bar_gesture",
  "component_preview_unsupported",
  "design_preview_surface",
  "icon_token",
  "waveform_bar",
]);

function relative(filePath: string) {
  return path.relative(root, filePath).replace(/\\/g, "/");
}

function readText(relativePath: string) {
  return readFileSync(path.join(root, relativePath), "utf8");
}

function addViolation(filePath: string, message: string) {
  violations.push(`${filePath}: ${message}`);
}

function walkFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const fullPath = path.join(directory, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) return walkFiles(fullPath);
    return /\.(ts|tsx)$/.test(entry) ? [fullPath] : [];
  });
}

function walkFilesByExtension(directory: string, extensions: readonly string[]): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const fullPath = path.join(directory, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) return walkFilesByExtension(fullPath, extensions);
    return extensions.some((extension) => entry.endsWith(extension)) ? [fullPath] : [];
  });
}

for (const directory of ["src/desktop-preview", "spikes/desktop-editor-shell/Common", "spikes/desktop-editor-shell/Data", "spikes/desktop-editor-shell/EditorShell"]) {
  for (const filePath of walkFilesByExtension(path.join(root, directory), [".ts", ".tsx", ".cs"])) {
    const source = readFileSync(filePath, "utf8");
    for (const retired of retiredTimeFields) {
      if (source.includes(retired)) addViolation(relative(filePath), `retired time field remains: ${retired}`);
    }
  }
}

function importTargets(source: string) {
  const targets: string[] = [];
  const importPattern = /import\s+(?:type\s+)?(?:[\s\S]*?)\s+from\s+["']([^"']+)["']/g;
  let match: RegExpExecArray | null;
  while ((match = importPattern.exec(source)) !== null) {
    targets.push(match[1] ?? "");
  }
  return targets;
}

function assertNoTerms(relativePath: string, terms: string[]) {
  const source = readText(relativePath);
  for (const term of terms) {
    if (source.includes(term)) {
      addViolation(relativePath, `forbidden central preview term "${term}"`);
    }
  }
}

function assertContains(relativePath: string, term: string, message: string) {
  const source = readText(relativePath);
  if (!source.includes(term)) {
    addViolation(relativePath, message);
  }
}

function assertSourceContains(sourceLabel: string, source: string, term: string, message: string) {
  if (!source.includes(term)) {
    addViolation(sourceLabel, message);
  }
}

function assertSourceDoesNotContain(sourceLabel: string, source: string, term: string, message: string) {
  if (source.includes(term)) {
    addViolation(sourceLabel, message);
  }
}

function assertAnyContains(relativePaths: string[], term: string, message: string) {
  const found = relativePaths.some((relativePath) => {
    const fullPath = path.join(root, relativePath);
    return existsSync(fullPath) && readText(relativePath).includes(term);
  });
  if (!found) {
    addViolation(relativePaths.join(", "), message);
  }
}

function assertDoesNotContain(relativePath: string, term: string, message: string) {
  const source = readText(relativePath);
  if (source.includes(term)) {
    addViolation(relativePath, message);
  }
}

function assertPackageScriptDoesNotContain(scriptName: string, term: string, message: string) {
  const packageJson = JSON.parse(readText("package.json")) as {
    scripts?: Record<string, string>;
  };
  const script = packageJson.scripts?.[scriptName] ?? "";
  if (script.includes(term)) {
    addViolation("package.json", message);
  }
}

function assertFilesDoNotContain(files: readonly string[], term: string, message: string) {
  for (const file of files) {
    const relativePath = relative(file);
    assertDoesNotContain(relativePath, term, message);
  }
}

const currentRepositoryFiles = walkFilesByExtension(
  path.join(root, "spikes", "desktop-editor-shell", "Data"),
  [".cs"],
);
const retiredComponentDefaultsPath = "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassDefaults.cs";
if (existsSync(path.join(root, retiredComponentDefaultsPath))) {
  addViolation(
    retiredComponentDefaultsPath,
    "dormant runtime component seed/default catalogs must not return; use explicit development scaffolding",
  );
}
for (const retiredComponentFactoryTerm of [
  "ComponentSeedRow",
  "ComponentSeedRows",
  "NewComponentSeed",
  "DefaultComponentClassConfigJson",
  "DefaultComponentDesignPreviewJson",
]) {
  assertFilesDoNotContain(
    currentRepositoryFiles,
    retiredComponentFactoryTerm,
    `runtime Data sources must not contain retired component factory ${retiredComponentFactoryTerm}`,
  );
}
for (const retiredModuleFactoryTerm of [
  "DefaultConversationConfigJson",
  "DefaultConversationDesignPreviewJson",
  "SeededComponentVariantReference",
  "ConversationPreviewMessageFields",
  "ApplyConversationRuntimeGroups",
]) {
  assertFilesDoNotContain(
    currentRepositoryFiles,
    retiredModuleFactoryTerm,
    `runtime Data sources must not contain retired Module factory ${retiredModuleFactoryTerm}`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/49_component_definition_source_contract.md",
  "AGENTS must require the Component definition source contract",
);
assertContains(
  "docs/architecture/README.md",
  "49_component_definition_source_contract.md",
  "the architecture index must include contract 49",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/50_module_definition_source_contract.md",
  "AGENTS must require the Module definition source contract",
);
assertContains(
  "docs/architecture/README.md",
  "50_module_definition_source_contract.md",
  "the architecture index must include contract 50",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/51_preview_payload_data_boundary_contract.md",
  "AGENTS must require the Preview payload data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "51_preview_payload_data_boundary_contract.md",
  "the architecture index must include contract 51",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "SpikeDatabase",
  "the Preview payload factory must consume only its typed data source",
);
for (const forbiddenProductionPayloadRepair of [
  "var ownerActor = string.IsNullOrWhiteSpace(ownerActorId)",
  "JsonNode.Parse(instance.AnimationJson) ?? new JsonObject()",
  'message["actor"] = string.IsNullOrWhiteSpace(actorId)',
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
    forbiddenProductionPayloadRepair,
    `Production payload preparation must not restore repaired owner/animation data (${forbiddenProductionPayloadRepair})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "ModuleRuntimeDocumentContracts.PrepareProduction",
  "Production payload preparation must route Module-owned document semantics before generic reference resolution",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the Preview payload data source must own the factory route's database dependency",
);
for (const forbiddenPreviewDataSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
    forbiddenPreviewDataSql,
    `the Preview payload data source must compose current services rather than owning SQL (${forbiddenPreviewDataSql})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_previewPayloadData = new DesignPreviewPayloadDataSource(database)",
  "the Preview controller must reuse one typed payload data source",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/52_module_instance_timeline_data_boundary_contract.md",
  "AGENTS must require the Module Instance timeline data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "52_module_instance_timeline_data_boundary_contract.md",
  "the architecture index must include contract 52",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceTimeline.cs",
  "SpikeDatabase",
  "the common Module Instance timeline must consume only its typed data source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceTimelineDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the timeline data source must own the timeline route's database dependency",
);
for (const forbiddenTimelineDataSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ModuleInstanceTimelineDataSource.cs",
    forbiddenTimelineDataSql,
    `the timeline data source must compose current services rather than owning SQL (${forbiddenTimelineDataSql})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "_timelineDataSource = new ModuleInstanceTimelineDataSource(database)",
  "the Preview payload data source must reuse the typed timeline boundary",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/53_actor_preview_data_boundary_contract.md",
  "AGENTS must require the Actor Preview data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "53_actor_preview_data_boundary_contract.md",
  "the architecture index must include contract 53",
);
for (const actorPreviewConsumer of [
  "spikes/desktop-editor-shell/EditorShell/ActorPreviewInputFactory.cs",
  "spikes/desktop-editor-shell/EditorShell/ActorAvatarPreviewFactory.cs",
  "spikes/desktop-editor-shell/EditorShell/ComponentPreviewRecordInputResolver.cs",
]) {
  assertDoesNotContain(
    actorPreviewConsumer,
    "SpikeDatabase",
    "Actor Preview interpretation and routing must consume only the typed Actor data source",
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ActorPreviewDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the Actor Preview data source must own the Actor Preview database dependency",
);
for (const forbiddenActorPreviewSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ActorPreviewDataSource.cs",
    forbiddenActorPreviewSql,
    `the Actor Preview data source must compose current services rather than owning SQL (${forbiddenActorPreviewSql})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "_actorDataSource = new ActorPreviewDataSource(database)",
  "the Preview payload boundary must compose the typed Actor Preview source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorInlinePreviewControllerFactory.cs",
  "new ActorPreviewDataSource(database)",
  "the inline Actor avatar route must compose the typed Actor Preview source",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/54_production_shot_context_data_boundary_contract.md",
  "AGENTS must require the Production Shot context data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "54_production_shot_context_data_boundary_contract.md",
  "the architecture index must include contract 54",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProductionShotContextService.cs",
  "SpikeDatabase",
  "Production Shot context policy must consume only its typed data source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProductionShotContextDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the Production Shot context data source must own that route's database dependency",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProductionShotContextDataSource.cs",
  "_actorDataSource = new ActorPreviewDataSource(database)",
  "Production Shot context must reuse the typed Actor context boundary",
);
for (const forbiddenProductionContextSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ProductionShotContextDataSource.cs",
    forbiddenProductionContextSql,
    `the Production Shot context data source must compose current services rather than owning SQL (${forbiddenProductionContextSql})`,
  );
}
for (const productionContextConsumer of [
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
]) {
  assertContains(
    productionContextConsumer,
    "new ProductionShotContextService(new ProductionShotContextDataSource(",
    "navigation and Preview must compose the typed Production Shot context boundary",
  );
}
for (const forbiddenProductionPayloadFallback of [
  "if (string.IsNullOrWhiteSpace(shot.OwnerActorId)) return selectedThemeId;",
  "? selectedThemeId\n            : actor.DefaultThemeId",
  "if (string.IsNullOrWhiteSpace(shot.OwnerActorId)) return \"\";",
  "catch (InvalidOperationException)",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
    forbiddenProductionPayloadFallback,
    `Preview payload Production context must not restore a selector or empty-value fallback (${forbiddenProductionPayloadFallback})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "RequiredProductionActorContext(node)",
  "Preview payload Production context must require the exact Shot owner Actor route",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/55_runtime_input_options_data_boundary_contract.md",
  "AGENTS must require the Runtime Input options data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "55_runtime_input_options_data_boundary_contract.md",
  "the architecture index must include contract 55",
);
for (const runtimeInputOptionFactory of [
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputFieldDefinitionFactory.cs",
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputDynamicOptions.cs",
]) {
  assertDoesNotContain(
    runtimeInputOptionFactory,
    "SpikeDatabase",
    "Runtime Input option factories must consume only their typed options data source",
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputOptionsDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the Runtime Input options data source must own the option route's database dependency",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputOptionsDataSource.cs",
  "_actorDataSource = new ActorPreviewDataSource(database)",
  "Runtime Input Actor options must reuse the typed Actor boundary",
);
for (const forbiddenRuntimeInputOptionSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputOptionsDataSource.cs",
    forbiddenRuntimeInputOptionSql,
    `the Runtime Input options data source must compose current services rather than owning SQL (${forbiddenRuntimeInputOptionSql})`,
  );
}
for (const runtimeInputOptionConsumer of [
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
]) {
  assertContains(
    runtimeInputOptionConsumer,
    "_runtimeInputOptions = new RuntimeInputOptionsDataSource(database)",
    "Runtime Input and animation editors must reuse one typed options data source",
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/56_preview_visual_context_data_boundary_contract.md",
  "AGENTS must require the Preview visual context data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "56_preview_visual_context_data_boundary_contract.md",
  "the architecture index must include contract 56",
);
assertContains(
  "spikes/desktop-editor-shell/Common/DevicePreviewMetrics.cs",
  "internal sealed record DevicePreviewMetrics(",
  "resolved Device Preview metrics must be a common top-level DTO",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Records.cs",
  "record DevicePreviewMetrics",
  "resolved Device Preview metrics must not be nested in the database facade",
);
for (const genericWebPreviewFile of [
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
]) {
  assertDoesNotContain(
    genericWebPreviewFile,
    "SpikeDatabase",
    "generic web Preview surfaces must consume common resolved metrics without database coupling",
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/PreviewVisualContextDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the Preview visual context data source must own that route's database dependency",
);
for (const forbiddenPreviewVisualContextSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/PreviewVisualContextDataSource.cs",
    forbiddenPreviewVisualContextSql,
    `the Preview visual context data source must compose current services rather than owning SQL (${forbiddenPreviewVisualContextSql})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_visualContextData = new PreviewVisualContextDataSource(database)",
  "the Preview controller must reuse one typed visual context data source",
);
for (const forbiddenPreviewControllerRead of [
  "_database.GetDeviceOptions",
  "_database.GetThemeOptions",
  "_database.GetProjectSettings",
  "_database.GetDevicePreviewMetrics",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
    forbiddenPreviewControllerRead,
    `the Preview controller must use its typed visual context boundary (${forbiddenPreviewControllerRead})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/57_production_preview_session_data_boundary_contract.md",
  "AGENTS must require the Production Preview session data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "57_production_preview_session_data_boundary_contract.md",
  "the architecture index must include contract 57",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProductionPreviewSessionDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the Production Preview session data source must own that route's database dependency",
);
for (const forbiddenProductionPreviewSessionSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ProductionPreviewSessionDataSource.cs",
    forbiddenProductionPreviewSessionSql,
    `the Production Preview session data source must compose current services rather than owning SQL (${forbiddenProductionPreviewSessionSql})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_productionPreviewData = new ProductionPreviewSessionDataSource(database)",
  "the Preview controller must reuse one typed Production session data source",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "private readonly SpikeDatabase _database",
  "the Preview controller must not retain a general database handle",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_database.",
  "the Preview controller must not bypass its typed data sources",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "GetShotModuleInstanceSlots",
  "the Preview controller must reuse the ordered stable ids from the timeline data source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_timelineDataSource.ShotSlotIds(shotId)",
  "the Preview controller must reuse the typed timeline slot boundary",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/58_component_preview_input_data_boundary_contract.md",
  "AGENTS must require the Component Preview input data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "58_component_preview_input_data_boundary_contract.md",
  "the architecture index must include contract 58",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentPreviewInputDataSource.cs",
  "private readonly SpikeDatabase _database",
  "the Component Preview input data source must own that route's database dependency",
);
for (const forbiddenComponentPreviewInputSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ComponentPreviewInputDataSource.cs",
    forbiddenComponentPreviewInputSql,
    `the Component Preview input data source must compose current services rather than owning SQL (${forbiddenComponentPreviewInputSql})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "_previewInputData = new ComponentPreviewInputDataSource(database)",
  "the isolated Preview input session must reuse one typed input data source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "_inputOptionsData = new RuntimeInputOptionsDataSource(database)",
  "the isolated Preview input session must reuse the Runtime Input options boundary",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "private readonly SpikeDatabase _database",
  "the isolated Preview input session must not retain a general database handle",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "_database.",
  "the isolated Preview input session must not bypass its typed data sources",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentPreviewActions.cs",
  "Func<string, JsonObject> componentVariantRuntimeContract",
  "the action interpreter must receive exact embedded contracts without persistence coupling",
);
for (const forbiddenActionPersistenceDependency of ["SpikeDatabase", "Mockups.DesktopEditorShell.Data"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ComponentPreviewActions.cs",
    forbiddenActionPersistenceDependency,
    `the action interpreter must remain persistence-independent (${forbiddenActionPersistenceDependency})`,
  );
}
for (const componentPreviewActionConsumer of [
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
]) {
  assertContains(
    componentPreviewActionConsumer,
    "_previewInputData.ComponentVariantRuntimeContract",
    "Preview action consumers must use the typed embedded Component contract boundary",
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/59_module_instance_animation_document_boundary_contract.md",
  "AGENTS must require the Module Instance animation document boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "59_module_instance_animation_document_boundary_contract.md",
  "the architecture index must include contract 59",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationDocumentStore.cs",
  "private readonly SpikeDatabase _database",
  "the animation document store must own the editor's database dependency",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationDocumentStore.cs",
  "private readonly ModuleInstanceTimelineDataSource _timelineDataSource",
  "the animation document store must reuse the common timeline data source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationDocumentStore.cs",
  "_database.UpdateModuleInstanceAnimationJson(moduleInstanceId, animationJson)",
  "the animation document store must delegate one explicit complete document write",
);
assertContains(
  "spikes/desktop-editor-shell/Common/ModuleInstanceAnimationDocumentContract.cs",
  "keyframes must be stored in ascending frame order",
  "the common animation document owner must require persisted owner-local keyframe order",
);
for (const animationDocumentConsumer of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs",
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationDocument.cs",
]) {
  assertContains(
    animationDocumentConsumer,
    "ModuleInstanceAnimationDocumentContract.",
    `${animationDocumentConsumer} must consume the common current animation document owner`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "ValidateAnimationJson",
  "the data facade must not retain a parallel animation document validator",
);
for (const forbiddenAnimationDocumentStoreSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationDocumentStore.cs",
    forbiddenAnimationDocumentStoreSql,
    `the animation document store must delegate through current services rather than owning SQL (${forbiddenAnimationDocumentStoreSql})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "_animationDocuments = new ModuleInstanceAnimationDocumentStore(database, _timelineDataSource)",
  "the animation editor must reuse one typed document store and timeline source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "_animationDocuments.SaveAnimationJson(node.Id, document.ToJson())",
  "the animation editor must hand the store one complete prepared animation document",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "private readonly SpikeDatabase _database",
  "the animation editor must not retain a general database handle",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "_database.",
  "the animation editor must not bypass its typed document and timeline boundaries",
);
for (const forbiddenTimelineMutation of ["SaveAnimationJson", "UpdateModuleInstanceAnimationJson"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ModuleInstanceTimelineDataSource.cs",
    forbiddenTimelineMutation,
    `the common timeline source must remain read-only (${forbiddenTimelineMutation})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/60_runtime_input_owner_document_boundary_contract.md",
  "AGENTS must require the Runtime Input owner document boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "60_runtime_input_owner_document_boundary_contract.md",
  "the architecture index must include contract 60",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputOwnerDocumentStore.cs",
  "private readonly SpikeDatabase _database",
  "the Runtime Input owner store must own that route's database dependency",
);
for (const forbiddenRuntimeInputOwnerStoreSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputOwnerDocumentStore.cs",
    forbiddenRuntimeInputOwnerStoreSql,
    `the Runtime Input owner store must delegate through current services rather than owning SQL (${forbiddenRuntimeInputOwnerStoreSql})`,
  );
}
for (const forbiddenRuntimeInputOwnerSemantic of [
  "FieldDefinition",
  "RuntimeInputCollectionDefinition",
  "RuntimeInputForwardingContract",
  "ModuleInstanceAnimationDocument",
  "Avalonia",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputOwnerDocumentStore.cs",
    forbiddenRuntimeInputOwnerSemantic,
    `the Runtime Input owner store must not absorb editor semantics (${forbiddenRuntimeInputOwnerSemantic})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputOwnerDocumentStore.cs",
  "A Module Instance has no isolated Design Preview document.",
  "a Screen must reject isolated Design Preview persistence explicitly",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_ownerDocuments = new RuntimeInputOwnerDocumentStore(database)",
  "the Runtime Inputs editor must reuse one typed owner document store",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "var source = _ownerDocuments.Load(node)",
  "Runtime Input owner resolution must delegate to the typed store",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_previewInputData.ComponentVariantConfig(componentVariantReference)",
  "the Runtime Inputs editor must reuse the Component Preview config boundary",
);
for (const forbiddenRuntimeInputOwnerRead of [
  "_database.GetModuleSettings",
  "_database.GetModuleVariantSettings",
  "_database.GetComponentVariantSettings",
  "_database.GetModuleInstanceVariantSettings",
  "_database.GetModuleInstanceRuntimePreviewJson",
  "_database.GetComponentVariantConfig",
  "_database.GetComponentVariantRuntimeInputs",
  "_database.GetComponentVariantSelectionSettings",
  "_database.UpdateModuleDesignPreviewJson",
  "_database.UpdateComponentClassDesignPreviewJson",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
    forbiddenRuntimeInputOwnerRead,
    `the Runtime Inputs editor must use its typed owner/config boundaries (${forbiddenRuntimeInputOwnerRead})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/61_runtime_input_instance_document_boundary_contract.md",
  "AGENTS must require the Runtime Input instance document boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "61_runtime_input_instance_document_boundary_contract.md",
  "the architecture index must include contract 61",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/62_animation_keyframe_drag_interaction_contract.md",
  "AGENTS must require the animation keyframe drag interaction contract",
);
assertContains(
  "docs/architecture/README.md",
  "62_animation_keyframe_drag_interaction_contract.md",
  "the architecture index must include contract 62",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "var frameUpdateGate = new TimelineFrameUpdateGate()",
  "the animation editor must gate its own synchronous Preview frame feedback",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "if (frameUpdateGate.IsActive) return;",
  "the animation surface must not rebuild during its own captured frame gesture",
);
for (const forbiddenTimelineFrameGateDependency of [
  "Avalonia",
  "SpikeDatabase",
  "Json",
  "PreviewBridge",
  "Renderer",
  "RuntimeAnimationFrameOrigin",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Common/TimelineFrameUpdateGate.cs",
    forbiddenTimelineFrameGateDependency,
    `the timeline frame update gate must remain a generic synchronous boundary (${forbiddenTimelineFrameGateDependency})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/63_dictionary_field_context_data_boundary_contract.md",
  "AGENTS must require the dictionary field context data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "63_dictionary_field_context_data_boundary_contract.md",
  "the architecture index must include contract 63",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorDictionaryFieldServices.cs",
  "_contextData = new DictionaryFieldContextDataSource(database)",
  "the shared dictionary service must compose one typed persisted-context source",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorDictionaryFieldServices.cs",
  "private readonly SpikeDatabase _database",
  "the shared dictionary service must not retain the general database facade",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorDictionaryFieldServices.cs",
  "_database.",
  "the shared dictionary service must use its typed context source",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorDictionaryFieldServices.cs",
  "_contextData.IconTokenAssetPath(IconThemeId(), singleToken)",
  "dictionary icon presentation must consume a resolved token asset path",
);
for (const forbiddenDictionaryContextDependency of [
  "SELECT ",
  "INSERT ",
  "UPDATE ",
  "DELETE FROM",
  "SqliteConnection",
  "Avalonia",
  "FieldDefinition",
  "DictionaryFieldControl",
  "Dialog",
  "RuntimeInputForwardingContract",
  "Override",
  "PreviewBridge",
  "Renderer",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/DictionaryFieldContextDataSource.cs",
    forbiddenDictionaryContextDependency,
    `the dictionary context source must remain a read-only data boundary (${forbiddenDictionaryContextDependency})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/64_embedded_component_document_boundary_contract.md",
  "AGENTS must require the embedded Component document boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "64_embedded_component_document_boundary_contract.md",
  "the architecture index must include contract 64",
);
for (const forbiddenEmbeddedContextDependency of [
  "Mockups.DesktopEditorShell.Data",
  "SpikeDatabase",
  "ActiveVariantName(",
  "CreateFieldValue(",
  "CommitFieldValue(",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/EditorEmbeddedContext.cs",
    forbiddenEmbeddedContextDependency,
    `embedded editor context must remain structural and persistence-independent (${forbiddenEmbeddedContextDependency})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "_embeddedDocuments = new EmbeddedComponentDocumentStore(database)",
  "embedded breadcrumbs must use the typed Component document store",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "private readonly SpikeDatabase _database",
  "the header controller must not retain the general database facade",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "_database.",
  "the header controller must use typed data boundaries",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldValueService.cs",
  "_embeddedDocuments.CreateFieldValue(context, embeddedFieldId)",
  "embedded field reads must delegate to the typed document store",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldValueService.cs",
  "_embeddedDocuments.CommitFieldValue(context, embeddedFieldId, value)",
  "embedded field writes must delegate to the typed document store",
);
for (const forbiddenEmbeddedDocumentStoreDependency of [
  "SELECT ",
  "INSERT ",
  "UPDATE ",
  "DELETE FROM",
  "SqliteConnection",
  "Avalonia",
  "Dialog",
  "RuntimeInputForwardingContract",
  "PreviewBridge",
  "Renderer",
  "Resolver",
  "JsonNode.Parse",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/EmbeddedComponentDocumentStore.cs",
    forbiddenEmbeddedDocumentStoreDependency,
    `the embedded Component document store must remain a narrow domain boundary (${forbiddenEmbeddedDocumentStoreDependency})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/65_editor_presentation_context_data_boundary_contract.md",
  "AGENTS must require the editor presentation context data boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "65_editor_presentation_context_data_boundary_contract.md",
  "the architecture index must include contract 65",
);
for (const editorPresentationContextConsumer of [
  "spikes/desktop-editor-shell/EditorShell/EditorPathBrowser.cs",
  "spikes/desktop-editor-shell/EditorShell/EditorFieldPostCommitEffects.cs",
]) {
  assertContains(
    editorPresentationContextConsumer,
    "new EditorPresentationContextDataSource(database)",
    "shared editor presentation consumers must compose the typed context source",
  );
  assertDoesNotContain(
    editorPresentationContextConsumer,
    "private readonly SpikeDatabase _database",
    "shared editor presentation consumers must not retain the database facade",
  );
  assertDoesNotContain(
    editorPresentationContextConsumer,
    "_database.",
    "shared editor presentation consumers must use the typed context source",
  );
}
for (const forbiddenEditorPresentationContextDependency of [
  "SELECT ",
  "INSERT ",
  "UPDATE ",
  "DELETE FROM",
  "SqliteConnection",
  "Avalonia",
  "System.IO",
  "IStorageProvider",
  "ProjectTreeNode",
  "FieldDefinition",
  "PreviewBridge",
  "Renderer",
  "Resolver",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/EditorPresentationContextDataSource.cs",
    forbiddenEditorPresentationContextDependency,
    `the editor presentation context source must remain a narrow read boundary (${forbiddenEditorPresentationContextDependency})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/66_simplified_editor_retirement_contract.md",
  "AGENTS must require the Simplified Editor retirement contract",
);
assertContains(
  "docs/architecture/README.md",
  "66_simplified_editor_retirement_contract.md",
  "the architecture index must include contract 66",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/67_system_bar_item_authoring_contract.md",
  "AGENTS must require the system bar item authoring contract",
);
assertContains(
  "docs/architecture/README.md",
  "67_system_bar_item_authoring_contract.md",
  "the architecture index must include contract 67",
);
for (const retiredSystemBarItemEditorPath of [
  "spikes/desktop-editor-shell/EditorShell/StatusBarItemsCollectionEditor.cs",
  "spikes/desktop-editor-shell/EditorShell/NavigationBarItemsCollectionEditor.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.StatusNavigationComponents.cs",
]) {
  if (existsSync(path.join(root, retiredSystemBarItemEditorPath))) {
    addViolation(
      retiredSystemBarItemEditorPath,
      "retired bespoke system-bar item persistence/editor source must not return",
    );
  }
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
  '["component.statusBar.items"] = new',
  "Status Bar Items must be a catalogued dictionary field",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
  '["component.navigationBar.items"] = new',
  "Navigation Bar Items must be a catalogued dictionary field",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
  /StatusBarItemsCollection[\s\S]*?CanEditStructure:\s*false/,
  "Status Bar Items must remain a fixed structured collection",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
  /NavigationBarItemsCollection[\s\S]*?CanEditStructure:\s*false/,
  "Navigation Bar Items must remain a fixed structured collection",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryStructuredCollectionControl.cs",
  "canEditStructure: _definition.IsEditable && collection.CanEditStructure",
  "fixed collection structure and Variant locks must be declared through generic collection metadata",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryStructuredCollectionControl.cs",
  "IsEditable: _definition.IsEditable",
  "structured item dictionary fields must inherit the owner Variant lock",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DictionaryStructuredCollectionControl.cs",
  "return new JsonArray();",
  "structured collection parsing must reject invalid current values instead of returning an empty array",
);
for (const [componentPath, contractName] of [
  ["spikes/desktop-editor-shell/Data/StatusBarComponentConfigContract.cs", "StatusBarComponentConfigContract"],
  ["spikes/desktop-editor-shell/Data/NavigationBarComponentConfigContract.cs", "NavigationBarComponentConfigContract"],
] as const) {
  assertContains(
    componentPath,
    "JsonPath.RequiredArray(config, \"items\"",
    `${contractName} must require the persisted items array`,
  );
  assertContains(
    componentPath,
    "duplicate stable id",
    `${contractName} must reject duplicate item ids`,
  );
}
for (const systemBarResolverPath of [
  "src/desktop-preview/statusBarComponentResolver.ts",
  "src/desktop-preview/navigationBarComponentResolver.ts",
]) {
  for (const forbiddenSystemBarResolverFallback of ["optionalNumber", "optionalString", "itemValue("]) {
    assertDoesNotContain(
      systemBarResolverPath,
      forbiddenSystemBarResolverFallback,
      `system-bar resolver must not supply item fallbacks (${forbiddenSystemBarResolverFallback})`,
    );
  }
}
for (const [systemBarRenderablePath, forbiddenFallbacks] of [
  ["src/desktop-preview/statusBarComponentRenderable.ts", ["item.id ||", "item.kind ||", "item.token ||", "numberValue(item.value", "stringValue(item.value"]],
  ["src/desktop-preview/navigationBarComponentRenderable.ts", ["item.id ||", "item.kind ||"]],
] as const) {
  for (const forbiddenFallback of forbiddenFallbacks) {
    assertDoesNotContain(
      systemBarRenderablePath,
      forbiddenFallback,
      `system-bar renderable must consume complete resolved items (${forbiddenFallback})`,
    );
  }
}
{
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (existsSync(databasePath)) {
    const database = new Database(databasePath, { readonly: true, fileMustExist: true });
    try {
      for (const [recordClassId, fieldId] of [
        ["component.status_bar", "component.statusBar.items"],
        ["component.navigation_bar", "component.navigationBar.items"],
      ] as const) {
        const row = database.prepare("SELECT layout_json FROM editor_layouts WHERE record_class_id = ?").get(recordClassId) as { layout_json?: string } | undefined;
        const layout = row?.layout_json ? JSON.parse(row.layout_json) as unknown : null;
        if (!JSON.stringify(layout).includes(`\"${fieldId}\"`)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${recordClassId} must contain the migrated ${fieldId} layout field`,
          );
        }
      }
    } finally {
      database.close();
    }
  }
}
const retiredSimplifiedEditorPath = "spikes/desktop-editor-shell/EditorShell/EditorSimplifiedProjection.cs";
if (existsSync(path.join(root, retiredSimplifiedEditorPath))) {
  addViolation(
    retiredSimplifiedEditorPath,
    "the retired Simplified Editor projection source must not return",
  );
}
const activeEditorShellSources = walkFilesByExtension(
  path.join(root, "spikes/desktop-editor-shell/EditorShell"),
  [".cs"],
);
const activeComponentVariantSources = [
  ...walkFilesByExtension(path.join(root, "src/desktop-preview"), [".ts", ".tsx"]),
  ...walkFilesByExtension(path.join(root, "spikes/desktop-editor-shell/Data"), [".cs"]),
  ...activeEditorShellSources,
  path.join(root, "spikes/desktop-editor-shell/MainWindow.axaml.cs"),
];
for (const retiredComponentVariantTerm of [
  "ComponentPreset",
  "componentPreset",
  "::preset::",
  "presetId",
  "buttonPresetId",
  "presetJsonKey",
  "component.preset",
  "metadata_json.presets",
  '"presets"',
]) {
  assertFilesDoNotContain(
    activeComponentVariantSources,
    retiredComponentVariantTerm,
    `active Component Variant code must not contain retired vocabulary (${retiredComponentVariantTerm})`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/69_component_variant_storage_vocabulary_contract.md",
  "AGENTS must require the Component Variant storage vocabulary contract",
);
assertContains(
  "docs/architecture/README.md",
  "69_component_variant_storage_vocabulary_contract.md",
  "the architecture index must include contract 69",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/70_conversation_message_actor_ownership_contract.md",
  "AGENTS must require the Conversation message Actor ownership contract",
);
assertContains(
  "docs/architecture/README.md",
  "70_conversation_message_actor_ownership_contract.md",
  "the architecture index must include contract 70",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/71_active_code_retirement_contract.md",
  "AGENTS must require the active code retirement contract",
);
assertContains(
  "docs/architecture/README.md",
  "71_active_code_retirement_contract.md",
  "the architecture index must include contract 71",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/72_single_semantic_rule_ownership_contract.md",
  "AGENTS must require the phase 0C semantic ownership contract",
);
assertContains(
  "docs/architecture/README.md",
  "72_single_semantic_rule_ownership_contract.md",
  "the architecture index must include contract 72",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/73_owner_validation_and_preview_document_boundary_contract.md",
  "AGENTS must require the owner validation and Preview document boundary contract",
);
assertContains(
  "docs/architecture/README.md",
  "73_owner_validation_and_preview_document_boundary_contract.md",
  "the architecture index must include contract 73",
);
for (const requiredPreviewObjectDocument of [
  "configJson",
  "designPreviewJson",
  "runtimeContractJson",
  "componentBaseConfigsJson",
  "appConfigJson",
  "instanceJson",
  "themeTokensJson",
]) {
  assertContains(
    "src/desktop-preview/renderablePayloadBoundary.ts",
    `["${requiredPreviewObjectDocument}",`,
    `the web payload boundary must require object document ${requiredPreviewObjectDocument}`,
  );
}
assertContains(
  "src/desktop-preview/renderablePayloadBoundary.ts",
  "payload.iconMappingJson !== undefined",
  "a present optional icon mapping must still be validated",
);
for (const retiredPreviewObjectFallback of ['json || "{}"', "asRecord(JSON.parse("]) {
  assertDoesNotContain(
    "src/desktop-preview/previewJsonHelpers.ts",
    retiredPreviewObjectFallback,
    "the shared Preview object parser must not coerce invalid documents to an empty object",
  );
}
assertDoesNotContain(
  "src/desktop-preview/runtimeInputForwarding.ts",
  "function parseRecord(",
  "Runtime Input forwarding must use the shared strict Preview object parser",
);
for (const moduleConfigContract of [
  "ConversationModuleConfigContract",
  "LockScreenModuleConfigContract",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/CurrentModuleConfigContract.cs",
    `${moduleConfigContract}.Validate(config, context)`,
    `current Module config routing must delegate to ${moduleConfigContract}`,
  );
}
for (const moduleConfigConsumer of [
  "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
]) {
  assertContains(
    moduleConfigConsumer,
    "CurrentModuleConfigContract.Validate(",
    `${moduleConfigConsumer} must consume the record-class-owned Module config contract`,
  );
}
for (const retiredModuleConfigFallback of [
  "JsonNode.Parse(value) as JsonObject ?? new JsonObject()",
  "JsonNode.Parse(value) as JsonArray ?? new JsonArray()",
  '?.ToJsonString() ?? "{}"',
  '?.ToJsonString() ?? "[]"',
  "JsonBoolString(",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
    retiredModuleConfigFallback,
    `Module config editing must not retain a silent document fallback (${retiredModuleConfigFallback})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
  "variant.Config,\n                $\"Module Variant",
  "the Module repository must validate every complete Variant config through its owner",
);
assertContains(
  "spikes/desktop-editor-shell/Common/ModuleAppearanceModeContract.cs",
  "public static string Resolve(JsonObject config, string inheritedMode, string owner)",
  "Module appearance mode validation and inheritance must have one common owner",
);
for (const appearanceModeConsumer of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
]) {
  assertContains(
    appearanceModeConsumer,
    "ModuleAppearanceModeContract.",
    `${appearanceModeConsumer} must consume the common Module appearance mode contract`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  'value is "light" or "dark" ? value : "inherit"',
  "Module appearance writes must reject unknown values instead of coercing them to inherit",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  'config["appearanceMode"]?.GetValue<string>() ?? "inherit"',
  "the Preview controller must not reconstruct the Module appearance mode fallback",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "string ThemeMode,\n    string ComponentBaseConfigsJson",
  "every prepared Design Preview payload must require an explicit resolved Theme mode",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "themeMode = payload.ThemeMode",
  "the web renderer request must transport the effective payload Theme mode unchanged",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "string themeMode,",
  "the web renderer must not accept a second session Theme mode beside the prepared payload",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  'payload.ThemeMode is "dark"',
  "the web renderer must not recompute effective Theme mode",
);
assertContains(
  "spikes/desktop-editor-shell/Common/TypographyStyleValue.cs",
  'JsonPath.ParseRequiredObject(value, "Typography Style value")',
  "Typography Style must parse every non-sentinel value as a required object",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Common/TypographyStyleValue.cs",
  "as JsonObject ?? []",
  "Typography Style must not reinterpret a wrong JSON root as inherited",
);
for (const typographyOwnerConsumer of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
]) {
  assertContains(
    typographyOwnerConsumer,
    "TypographyStyleValue.Parse(",
    `${typographyOwnerConsumer} must consume the strict Typography Style owner`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "ValueKind.TypographyStyle => JsonNode.Parse(value)",
  "Typography Style writes must not bypass the strict value owner",
);
assertContains(
  "spikes/desktop-editor-shell/Common/DesktopChildProcess.cs",
  "public static string ResolveNodeExecutable()",
  "external Node processes must share one platform executable resolver",
);
for (const nodeProcessConsumer of [
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "spikes/desktop-editor-shell/EditorShell/ChromiumPreviewRasterizer.cs",
]) {
  assertContains(
    nodeProcessConsumer,
    "DesktopChildProcess.ResolveNodeExecutable()",
    `${nodeProcessConsumer} must use the common Node executable resolver`,
  );
  assertDoesNotContain(
    nodeProcessConsumer,
    "private static string ResolveNodeExecutable()",
    `${nodeProcessConsumer} must not restore a parallel Node executable resolver`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Common/VariantReferenceId.cs",
  'public const string Separator = "::variant::";',
  "Component and Module Variants must share one full-reference grammar",
);
for (const retiredVariantReferenceHelper of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs", "TryParseComponentVariantNodeId"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs", "ComponentVariantNodeId("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs", "TryParseModuleVariantNodeId"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs", "ModuleVariantNodeId("],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryComponentVariantControl.cs", "VariantSeparator"],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryStructuredCollectionControl.cs", 'IndexOf("::variant::"'],
  ["spikes/desktop-editor-shell/EditorShell/EditorNodeSelectionState.cs", 'EndsWith("::variant::default"'],
  ["spikes/desktop-editor-shell/EditorShell/EditorEmbeddedUsageNavigator.cs", 'EndsWith("::variant::default"'],
] as const) {
  assertDoesNotContain(
    retiredVariantReferenceHelper[0],
    retiredVariantReferenceHelper[1],
    `${retiredVariantReferenceHelper[0]} must not restore a parallel Variant reference grammar`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "internal static FieldOption? PreferredResourceOption(",
  "Preview resource refresh and recovery must share one session selection rule",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "selectedDevice ??= deviceOptions.FirstOrDefault();",
  "Preview must not restore the duplicated Device selection path",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "selectedTheme ??= themeOptions.FirstOrDefault();",
  "Preview must not restore the duplicated Theme selection path",
);
for (const retiredParallelPayloadBuilder of [
  "FromComponentClass(",
  "FromComponentVariant(",
  "FromModule(",
  "FromModuleVariant(",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
    retiredParallelPayloadBuilder,
    `Design Preview payload preparation must not restore parallel builder ${retiredParallelPayloadBuilder}`,
  );
}
for (const sharedPayloadBuilder of ["FromComponentSource(", "FromModuleSource("]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
    sharedPayloadBuilder,
    `Design Preview payload preparation must keep shared builder ${sharedPayloadBuilder}`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "private int ModuleInstanceStartFrame(",
  "Preview navigation must use the common timeline Screen origin",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "ModuleInstanceTimeline.ScreenStartFrame(_timelineDataSource, moduleInstanceId)",
  "payload local-frame preparation must use the common timeline Screen origin",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "startFrame += slot.DurationFrames;",
  "payload data source must not reconstruct Screen origin from slot duration",
);
for (const retiredVariantEnvelopeHelper of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs", "private static JsonObject? FindVariant("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs", "private static string UniqueVariantId("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs", "private static string UniqueModuleVariantId("],
] as const) {
  assertDoesNotContain(
    retiredVariantEnvelopeHelper[0],
    retiredVariantEnvelopeHelper[1],
    `${retiredVariantEnvelopeHelper[0]} must not restore parallel Variant envelope operation ${retiredVariantEnvelopeHelper[1]}`,
  );
}
for (const sharedVariantEnvelopeOperation of ["FindSource(", "UniqueId(", "CreateSource("]) {
  assertContains(
    "spikes/desktop-editor-shell/Common/VariantEnvelopeContract.cs",
    sharedVariantEnvelopeOperation,
    `Variant envelope contract must own ${sharedVariantEnvelopeOperation}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Common/VariantEnvelopeContract.cs",
  'public const string DefaultId = "default";',
  "Component and Module Variants must share one stable Default Variant id",
);
for (const retiredDefaultVariantConstant of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs", "DefaultComponentVariantId"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs", "DefaultModuleVariantId"],
] as const) {
  assertDoesNotContain(
    retiredDefaultVariantConstant[0],
    retiredDefaultVariantConstant[1],
    `${retiredDefaultVariantConstant[0]} must not restore a local Default Variant id`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/PreviewReferenceOverlay.cs",
  "ProjectPathService.ResolveLocalPath(state.SourcePath, state.ProjectMediaRoot)",
  "Preview reference media paths must use the common Project path resolver",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/PreviewReferenceOverlay.cs",
  "private static string ResolvePath(",
  "Preview reference overlay must not restore a parallel Project media path resolver",
);
for (const actorPreviewFactory of [
  "spikes/desktop-editor-shell/EditorShell/ActorPreviewInputFactory.cs",
  "spikes/desktop-editor-shell/EditorShell/ActorAvatarPreviewFactory.cs",
]) {
  assertContains(
    actorPreviewFactory,
    "ActorIdentityText.Initials(",
    `${actorPreviewFactory} must use the common Actor initials identity rule`,
  );
  assertDoesNotContain(
    actorPreviewFactory,
    "private static string Initials(",
    `${actorPreviewFactory} must not restore a local Actor initials rule`,
  );
}
for (const variantCreationOwner of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs",
]) {
  assertDoesNotContain(
    variantCreationOwner,
    '["protected"] = false',
    `${variantCreationOwner} must use the complete common Variant source constructor`,
  );
}
for (const retiredInactiveSource of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassLayouts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.PreviewActions.cs",
  "spikes/desktop-editor-shell/scripts/icon-themes/sync-icon-theme-token.cjs",
]) {
  if (existsSync(path.join(root, retiredInactiveSource))) {
    addViolation(
      retiredInactiveSource,
      "retired inactive source or duplicate must not return to active desktop runtime",
    );
  }
}
assertContains(
  "spikes/desktop-editor-shell/Common/ConversationMessageActorContract.cs",
  'public const string ConversationRecordClassId = "module.core.chat"',
  "the Module runtime-document registry must route Conversation by its exact stable record class",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "ValidateCurrentModuleRuntimeDocuments(connection)",
  "startup validation must enforce current Module runtime-document contracts read-only",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
  "ValidateModuleInstanceRuntimeContent(connection, moduleInstanceId, content)",
  "Module Instance content writes must enforce their owning runtime-document contract",
);
for (const retiredSimplifiedEditorTerm of [
  "EditorSimplified",
  "EditorPresentationMode",
  "SimplifiedProjection",
  "SimplifiedSlotFieldIds",
  "CreateSimplified",
  "Show in Simplified",
]) {
  assertFilesDoNotContain(
    activeEditorShellSources,
    retiredSimplifiedEditorTerm,
    `the retired Simplified Editor route must not return (${retiredSimplifiedEditorTerm})`,
  );
}
assertFilesDoNotContain(
  activeEditorShellSources,
  "SaveEditorLayout(",
  "ordinary editor code must not persist layout metadata while opening or rebuilding an editor",
);
assertContains(
  "spikes/desktop-editor-shell/Data/EditorLayoutRepository.cs",
  "document.Count != 1 || document[\"cards\"] is not JsonArray",
  "current editor layout reads must require the exact cards-only root",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "json_each.key <> 'cards'",
  "startup validation must reject additional editor layout root properties",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "'simplified'",
  "startup validation must reject the retired Simplified Editor metadata",
);
{
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (existsSync(databasePath)) {
    const database = new Database(databasePath, { readonly: true, fileMustExist: true });
    try {
      const invalidLayouts = database.prepare(`
        SELECT record_class_id
        FROM editor_layouts
        WHERE COALESCE(json_type(layout_json, '$.cards'), '') <> 'array'
           OR EXISTS (
             SELECT 1
             FROM json_each(editor_layouts.layout_json)
             WHERE json_each.key <> 'cards'
           )
      `).all() as { record_class_id: string }[];
      for (const layout of invalidLayouts) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `${layout.record_class_id} must use the current cards-only editor layout root`,
        );
      }
      const animationRows = database.prepare(
        "SELECT id, animation_json FROM module_instances ORDER BY id",
      ).all() as { id: string; animation_json: string }[];
      for (const row of animationRows) {
        const animation = JSON.parse(row.animation_json) as {
          tracks?: Array<{ id?: unknown; keyframes?: Array<{ frame?: unknown }> }>;
        };
        for (const track of animation.tracks ?? []) {
          const frames = (track.keyframes ?? []).map((keyframe) => keyframe.frame);
          if (frames.some((frame, index) =>
            typeof frame !== "number"
            || index > 0 && frame < (frames[index - 1] as number))) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `Module Instance '${row.id}' track '${String(track.id ?? "")}' keyframes must remain in persisted ascending frame order`,
            );
          }
        }
      }
    } finally {
      database.close();
    }
  }
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputInstanceDocumentStore.cs",
  "private readonly SpikeDatabase _database",
  "the Runtime Input instance store must own that route's database dependency",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputInstanceDocumentStore.cs",
  "private readonly ModuleInstanceAnimationDocumentStore _animationDocuments",
  "the Runtime Input instance store must compose the animation document boundary",
);
for (const forbiddenRuntimeInputInstanceStoreSql of ["SELECT ", "INSERT ", "UPDATE ", "DELETE FROM", "SqliteConnection"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputInstanceDocumentStore.cs",
    forbiddenRuntimeInputInstanceStoreSql,
    `the Runtime Input instance store must delegate through current services rather than owning SQL (${forbiddenRuntimeInputInstanceStoreSql})`,
  );
}
for (const forbiddenRuntimeInputInstanceSemantic of [
  "FieldDefinition",
  "RuntimeInputCollectionDefinition",
  "RuntimeInputForwardingContract",
  "ProjectTreeNode",
  "Avalonia",
  "Guid.NewGuid",
  "RuntimeAnimationFrameOrigin",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputInstanceDocumentStore.cs",
    forbiddenRuntimeInputInstanceSemantic,
    `the Runtime Input instance store must not absorb editor or temporal semantics (${forbiddenRuntimeInputInstanceSemantic})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_instanceDocuments = new RuntimeInputInstanceDocumentStore(database)",
  "the Runtime Inputs editor must reuse one typed instance document store",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "private readonly SpikeDatabase _database",
  "the Runtime Inputs editor must not retain a general database handle",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_database.",
  "the Runtime Inputs editor must not bypass its typed owner and instance stores",
);
for (const requiredRuntimeInputInstanceOperation of [
  "_instanceDocuments.UpdateRuntimeValue",
  "_instanceDocuments.AddCollectionItem",
  "_instanceDocuments.InsertCollectionItemAfter",
  "_instanceDocuments.DuplicateCollectionItem",
  "_instanceDocuments.MoveCollectionItem",
  "_instanceDocuments.DeleteCollectionItem",
  "_instanceDocuments.UpdateCollectionValues",
  "_instanceDocuments.AnimationJson",
  "_instanceDocuments.SaveAnimationJson",
]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
    requiredRuntimeInputInstanceOperation,
    `the Runtime Inputs editor must delegate persisted instance operations (${requiredRuntimeInputInstanceOperation})`,
  );
}
assertFilesDoNotContain(
  currentRepositoryFiles,
  "EnsureVariantArray",
  "current Component Variants must never synthesize a missing Variant array",
);
assertFilesDoNotContain(
  currentRepositoryFiles,
  "ComponentVariantIsLocked",
  "current Component Variants must not infer lock state from an id",
);
assertFilesDoNotContain(
  currentRepositoryFiles,
  "ParseJsonObject(string.IsNullOrWhiteSpace",
  "current persisted JSON roots must reject blank documents instead of parsing an empty object",
);
assertFilesDoNotContain(
  currentRepositoryFiles,
  "variant.ConfigJson == \"{}\"",
  "a selected Component Variant config must not fall back to class config",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/35_current_json_and_variant_contract.md",
  "AGENTS must require the current JSON and Variant contract",
);
assertContains(
  "docs/architecture/README.md",
  "35_current_json_and_variant_contract.md",
  "the architecture index must include contract 35",
);
const jsonRootInventorySource = readText("spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs");
const executableJsonRoots = new Map(
  [...jsonRootInventorySource.matchAll(/\("([a-z_]+)", "([a-z_]+)", "(object|array)"\)/g)]
    .map((match) => [`${match[1]}.${match[2]}`, match[3]]),
);
const documentedJsonRoots = new Map<string, string>();
let documentedRootKind = "";
for (const line of readText("docs/architecture/35_current_json_and_variant_contract.md").split(/\r?\n/)) {
  if (line === "object" || line === "array") {
    documentedRootKind = line;
    continue;
  }
  const entry = /^  ([a-z_]+\.[a-z_]+)$/.exec(line)?.[1];
  if (entry && documentedRootKind) documentedJsonRoots.set(entry, documentedRootKind);
}
for (const [entry, rootKind] of executableJsonRoots) {
  if (documentedJsonRoots.get(entry) !== rootKind) {
    addViolation(
      "docs/architecture/35_current_json_and_variant_contract.md",
      `${entry} must document its executable ${rootKind} root`,
    );
  }
}
for (const [entry, rootKind] of documentedJsonRoots) {
  if (executableJsonRoots.get(entry) !== rootKind) {
    addViolation(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
      `${entry} must have the documented ${rootKind} startup root`,
    );
  }
}
function assertDesktopJsonRootsAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    for (const [entry, rootKind] of executableJsonRoots) {
      const [table, column] = entry.split(".");
      const rows = database.prepare(`SELECT rowid AS owner_rowid, ${column} AS json FROM ${table}`).all() as {
        owner_rowid: number;
        json: string;
      }[];
      for (const row of rows) {
        let parsed: unknown;
        try {
          parsed = JSON.parse(row.json);
        } catch {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${entry} row ${row.owner_rowid} contains malformed JSON`,
          );
          continue;
        }
        const matches = rootKind === "array"
          ? Array.isArray(parsed)
          : typeof parsed === "object" && parsed !== null && !Array.isArray(parsed);
        if (!matches) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${entry} row ${row.owner_rowid} must have a ${rootKind} root`,
          );
        }
      }
    }
  } finally {
    database.close();
  }
}
assertDesktopJsonRootsAreCanonical();
assertContains(
  "spikes/desktop-editor-shell/Common/VariantEnvelopeContract.cs",
  "RequiredBoolean",
  "the shared Variant envelope must require explicit boolean flags",
);
assertContains(
  "spikes/desktop-editor-shell/Common/VariantEnvelopeContract.cs",
  "must contain the stable Default Variant id '{DefaultId}'",
  "the shared Variant envelope must require the stable Default id",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProductionPreviewRuntimeResolver.cs",
  "catch",
  "Production payload parsing must reject invalid current JSON instead of returning an empty object",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewTestValues.cs",
  "return new JsonObject();",
  "Design Preview Test Values must not hide invalid current JSON behind an empty object",
);

function assertSharedEditorSurfacesHaveNoConcreteEditors() {
  const sharedSurfaces = [
    "spikes/desktop-editor-shell/MainWindow.axaml.cs",
    "spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorBreadcrumbBar.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorCardHostController.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorContextStrip.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorGroupBlock.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  ];
  const concreteEditorTypes = [
    "IconThemeTokensCollectionEditor",
    "NavigationBarItemsCollectionEditor",
    "RuntimeInputsCollectionEditor",
    "ShotModuleInstancesCollectionEditor",
    "StatusBarItemsCollectionEditor",
  ];
  const concreteComponentLiterals = [
    '"audio"',
    '"avatar"',
    '"bubble"',
    '"button"',
    '"label"',
    '"navigation_bar"',
    '"status_bar"',
    '"text_input"',
    '"video"',
  ];

  for (const relativePath of sharedSurfaces) {
    for (const term of [...concreteEditorTypes, ...concreteComponentLiterals]) {
      assertDoesNotContain(
        relativePath,
        term,
        `shared editor surface contains concrete editor/component knowledge: ${term}`,
      );
    }
  }
}

function assertMatches(relativePath: string, pattern: RegExp, message: string) {
  const source = readText(relativePath);
  if (!pattern.test(source)) {
    addViolation(relativePath, message);
  }
}

function assertSourceMatches(sourceLabel: string, source: string, pattern: RegExp, message: string) {
  if (!pattern.test(source)) {
    addViolation(sourceLabel, message);
  }
}

function quotedStringsFromBlock(relativePath: string, pattern: RegExp, message: string) {
  const source = readText(relativePath);
  const match = pattern.exec(source);
  if (!match) {
    addViolation(relativePath, message);
    return new Set<string>();
  }
  return new Set([...match[1].matchAll(/"([^"]+)"/g)].map((item) => item[1] ?? ""));
}

function assertStringSetEquals(
  relativePath: string,
  actual: Set<string>,
  expected: Set<string>,
  label: string,
) {
  for (const value of expected) {
    if (!actual.has(value)) {
      addViolation(relativePath, `${label} is missing primitive "${value}"`);
    }
  }
  for (const value of actual) {
    if (!expected.has(value)) {
      addViolation(relativePath, `${label} contains non-allowlisted primitive "${value}"`);
    }
  }
}

function assertPropertyBlockDoesNotContain(
  relativePath: string,
  propertyName: string,
  term: string,
  message: string,
) {
  const source = readText(relativePath);
  const match = new RegExp(`public bool ${propertyName} =>[\\s\\S]*?;`).exec(source);
  if (!match) {
    addViolation(relativePath, `could not find ${propertyName} permission block`);
    return;
  }
  if (match[0].includes(term)) {
    addViolation(relativePath, message);
  }
}

function assertPropertyBlockContainsKind(
  relativePath: string,
  propertyName: string,
  kind: string,
  expected: boolean,
  message: string,
) {
  const source = readText(relativePath);
  const match = new RegExp(`public bool ${propertyName} =>[\\s\\S]*?;`).exec(source);
  if (!match) {
    addViolation(relativePath, `could not find ${propertyName} permission block`);
    return;
  }
  const contains = new RegExp(`ProjectTreeNodeKind\\.${kind}(?![A-Za-z0-9_])`).test(match[0]);
  if (contains !== expected) addViolation(relativePath, message);
}

function assertDesktopDatabaseTableIsEmpty(tableName: string, message: string) {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) {
    return;
  }

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const table = database
      .prepare("SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?")
      .get(tableName) as { name: string } | undefined;
    if (!table) {
      return;
    }

    const row = database.prepare(`SELECT COUNT(*) AS count FROM ${tableName}`).get() as { count: number };
    if (row.count > 0) {
      addViolation("data/desktop-editor-spike.sqlite", `${message}; found ${row.count} row(s) in ${tableName}`);
    }
  } finally {
    database.close();
  }
}

function assertDesktopComponentVariantReferencesAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, project_id, component_type, config_json, metadata_json FROM component_classes")
      .all() as {
        id: string;
        project_id: string;
        component_type: string;
        config_json: string;
        metadata_json: string;
      }[];
    const rowsById = new Map(rows.map((row) => [row.id, row]));
    const componentVariantReferenceKeys = new Set([
      "variantReference",
      "buttonVariantReference",
      "bubbleVariant",
      "headerAvatarVariant",
      "keyboardVariant",
      "textInputBarVariant",
    ]);
    const variantsByClassId = new Map<string, Set<string>>();
    const variantsByClassIdAndMetadata = new Map<string, Record<string, unknown>[]>();
    for (const row of rows) {
      const metadata = parseRequiredJsonObject(row.metadata_json, `component ${row.id}.metadata_json`);
      const variants = completeVariantEnvelopes(metadata.variants, `component ${row.id}.metadata_json.variants`);
      variantsByClassIdAndMetadata.set(row.id, variants);
      variantsByClassId.set(row.id, new Set(variants.map((variant) => variant.id as string)));
    }

    const validateValue = (owner: (typeof rows)[number], value: unknown, pathLabel: string) => {
      if (Array.isArray(value)) {
        value.forEach((item, index) => validateValue(owner, item, `${pathLabel}[${index}]`));
        return;
      }
      if (typeof value !== "object" || value === null) return;

      for (const [key, child] of Object.entries(value)) {
        const childPath = pathLabel ? `${pathLabel}.${key}` : key;
        if (componentVariantReferenceKeys.has(key) && typeof child === "string" && child.trim()) {
          const match = /^(?<classId>[A-Za-z0-9_.-]+)::variant::(?<variantId>[A-Za-z0-9_.-]+)$/.exec(child);
          const targetClassId = match?.groups?.classId ?? "";
          const variantId = match?.groups?.variantId ?? "";
          const target = rowsById.get(targetClassId);
          if (!match || !target || target.project_id !== owner.project_id || !variantsByClassId.get(targetClassId)?.has(variantId)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `component ${owner.id} has invalid full variant reference at ${childPath}: ${child}`,
            );
          }
        }
        validateValue(owner, child, childPath);
      }
    };

    for (const row of rows) {
      const classConfig = parseRequiredJsonObject(row.config_json, `component ${row.id}.config_json`);
      validateValue(row, classConfig, "config");
      const variants = variantsByClassIdAndMetadata.get(row.id) ?? [];
      variants.forEach((variant, index) => validateValue(row, variant, `metadata.variants[${index}]`));
      const defaultVariant = variants.find((variant) => variant.id === "default");
      if (!defaultVariant || JSON.stringify(defaultVariant.config) !== JSON.stringify(classConfig)) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `component ${row.id} config_json must mirror its canonical Default variant`,
        );
      }
    }
  } finally {
    database.close();
  }
}

assertDesktopComponentVariantReferencesAreCanonical();

function jsonParse(value: string): unknown {
  return JSON.parse(value) as unknown;
}

function parseRequiredJsonObject(value: string, owner: string): Record<string, unknown> {
  if (!value.trim()) {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} is blank`);
    return {};
  }
  try {
    const parsed = JSON.parse(value) as unknown;
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner} must be a JSON object`);
      return {};
    }
    return parsed as Record<string, unknown>;
  } catch {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} contains malformed JSON`);
    return {};
  }
}

function completeVariantEnvelopes(value: unknown, owner: string): Record<string, unknown>[] {
  if (!Array.isArray(value) || value.length === 0) {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} must be a non-empty Variant array`);
    return [];
  }
  const variants: Record<string, unknown>[] = [];
  const ids = new Set<string>();
  for (const [index, candidate] of value.entries()) {
    if (typeof candidate !== "object" || candidate === null || Array.isArray(candidate)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] must be an object`);
      continue;
    }
    const variant = candidate as Record<string, unknown>;
    const id = typeof variant.id === "string" ? variant.id : "";
    const name = typeof variant.name === "string" ? variant.name : "";
    if (!id.trim() || !/^[\p{L}\p{N}_.-]+$/u.test(id)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has an invalid stable id`);
    } else if (ids.has(id)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner} contains duplicate Variant id ${id}`);
    }
    ids.add(id);
    if (!name.trim()) addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no display name`);
    if (typeof variant.protected !== "boolean") addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no explicit protected flag`);
    if (typeof variant.locked !== "boolean") addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no explicit locked flag`);
    if (typeof variant.config !== "object" || variant.config === null || Array.isArray(variant.config)) {
      addViolation("data/desktop-editor-spike.sqlite", `${owner}[${index}] has no object config snapshot`);
    }
    variants.push(variant);
  }
  const defaults = variants.filter((variant) => variant.id === "default");
  if (defaults.length !== 1 || defaults[0]?.protected !== true) {
    addViolation("data/desktop-editor-spike.sqlite", `${owner} must contain exactly one protected Default Variant`);
  }
  return variants;
}

function assertDesktopModuleVariantEnvelopesAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const modules = database.prepare("SELECT id, metadata_json FROM modules").all() as {
      id: string;
      metadata_json: string;
    }[];
    for (const module of modules) {
      const metadata = parseRequiredJsonObject(module.metadata_json, `module ${module.id}.metadata_json`);
      completeVariantEnvelopes(metadata.variants, `module ${module.id}.metadata_json.variants`);
    }
  } finally {
    database.close();
  }
}

assertDesktopModuleVariantEnvelopesAreCanonical();

function jsonRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? value as Record<string, unknown>
    : {};
}

function jsonArray(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function assertDesktopRuntimeInputValueKindsAreCanonical() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const valueKindSource = readText("spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs");
  const valueKindBlock = /internal enum ValueKind\s*\{([\s\S]*?)\}/.exec(valueKindSource);
  const valueKinds = new Set(
    (valueKindBlock?.[1] ?? "")
      .split(",")
      .map((value) => value.trim())
      .filter(Boolean),
  );
  const inputKinds = new Set([
    "text",
    "number",
    "integerPair",
    "boolean",
    "option",
    "recordReference",
    "componentVariant",
    "themeToken",
    "icon",
    "iconList",
    "multilineText",
    "mediaFilePath",
    "behaviorTiming",
    "collection",
  ]);
  const kindForValueKind = new Map<string, string>();
  for (const valueKind of ["Integer", "Decimal", "HueDegrees", "Alpha"]) kindForValueKind.set(valueKind, "number");
  kindForValueKind.set("IntegerPair", "integerPair");
  kindForValueKind.set("Boolean", "boolean");
  kindForValueKind.set("OptionToken", "option");
  kindForValueKind.set("RecordReference", "recordReference");
  kindForValueKind.set("ComponentVariant", "componentVariant");
  kindForValueKind.set("ThemeToken", "themeToken");
  kindForValueKind.set("IconToken", "icon");
  for (const valueKind of ["IconTokenList", "IconSlots"]) kindForValueKind.set(valueKind, "iconList");
  kindForValueKind.set("StringMultiline", "multilineText");
  kindForValueKind.set("MediaFilePath", "mediaFilePath");
  kindForValueKind.set("StructuredCollection", "collection");
  kindForValueKind.set("BehaviorTiming", "behaviorTiming");
  for (const valueKind of [
    "StringSingleLine",
    "StringReadOnly",
    "DirectoryPath",
    "ImageFilePath",
    "ThemeTokenPair",
    "TypographyStyle",
    "TypographySystemStyle",
    "HexColor",
    "PaletteColorToken",
    "PaletteColorPair",
    "PaletteColorAlphaPair",
    "EmbeddedComponent",
    "ComponentInputBindings",
    "AlignmentPlacement",
    "Motion",
    "MotionTiming",
  ]) kindForValueKind.set(valueKind, "text");
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database.prepare(`
      SELECT 'component class' AS owner_type, id, config_json, design_preview_json, metadata_json
      FROM component_classes
      UNION ALL
      SELECT 'module', id, config_json, design_preview_json, metadata_json
      FROM modules
    `).all() as {
      owner_type: string;
      id: string;
      config_json: string;
      design_preview_json: string;
      metadata_json: string;
    }[];
    const visit = (owner: string, value: unknown, pathLabel: string) => {
      if (Array.isArray(value)) {
        value.forEach((item, index) => visit(owner, item, `${pathLabel}[${index}]`));
        return;
      }
      if (typeof value !== "object" || value === null) return;
      const definition = value as Record<string, unknown>;
      if (typeof definition.id === "string"
          && typeof definition.jsonKey === "string"
          && typeof definition.kind === "string") {
        if (!inputKinds.has(definition.kind)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${owner}.${pathLabel} uses unsupported runtime input kind "${definition.kind}"`,
          );
        }
        if (typeof definition.valueKind !== "string" || !valueKinds.has(definition.valueKind)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${owner}.${pathLabel} uses missing or non-canonical valueKind "${String(definition.valueKind ?? "")}"`,
          );
        } else if (kindForValueKind.get(definition.valueKind) !== definition.kind) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${owner}.${pathLabel} kind "${definition.kind}" does not match valueKind "${definition.valueKind}"`,
          );
        }
      }
      for (const [key, child] of Object.entries(definition)) {
        visit(owner, child, pathLabel ? `${pathLabel}.${key}` : key);
      }
    };
    for (const row of rows) {
      for (const [column, json] of [
        ["config_json", row.config_json],
        ["design_preview_json", row.design_preview_json],
        ["metadata_json", row.metadata_json],
      ] as const) {
        visit(`${row.owner_type} ${row.id}.${column}`, JSON.parse(json), "$");
      }
    }
  } finally {
    database.close();
  }
}

function walkJson(value: unknown, visit: (value: unknown, pathLabel: string) => void, pathLabel = "") {
  visit(value, pathLabel);
  if (Array.isArray(value)) {
    value.forEach((item, index) => walkJson(item, visit, `${pathLabel}[${index}]`));
    return;
  }
  if (typeof value !== "object" || value === null) return;
  for (const [key, child] of Object.entries(value)) {
    walkJson(child, visit, pathLabel ? `${pathLabel}.${key}` : key);
  }
}

function assertDesktopDatabaseDoesNotContainRetiredTokens() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const retiredRadiusTokens = new Set([
    "theme.radii.control",
    "theme.radii.card",
    "theme.radii.panel",
    "theme.radii.surface",
    "theme.radii.pill",
    "theme.radii.avatar",
  ]);
  const retiredKeyboardKeys = new Set([
    "backgroundColorToken",
    "backgroundAlpha",
    "keyBackgroundColorToken",
    "specialKeyBackgroundColorToken",
    "pressedKeyBackgroundColorToken",
    "keyTextColorToken",
    "keyBorderColorToken",
    "popoverBackgroundColorToken",
    "specialKeyTextScale",
  ]);
  const retiredComponentVariantKeys = new Set([
    "presetId",
    "buttonPresetId",
    "presetJsonKey",
  ]);
  const retiredComponentVariantValues = new Set([
    "ComponentPreset",
    "componentPreset",
    "component.preset",
  ]);

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const componentRows = database
      .prepare("SELECT id, component_type, config_json, design_preview_json, metadata_json FROM component_classes")
      .all() as { id: string; component_type: string; config_json: string; design_preview_json: string; metadata_json: string }[];
    for (const row of componentRows) {
      const metadata = jsonRecord(jsonParse(row.metadata_json));
      if ("presets" in metadata) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `${row.id}.metadata_json still contains the retired Component presets root`,
        );
      }
      for (const [column, json] of [
        ["config_json", row.config_json],
        ["design_preview_json", row.design_preview_json],
        ["metadata_json", row.metadata_json],
      ] as const) {
        if (json.includes("theme.typography.fontFamily")) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.${column} still uses retired typography font-family sentinel "theme.typography.fontFamily"; use "theme"`,
          );
        }
        walkJson(jsonParse(json), (value, pathLabel) => {
          const key = pathLabel.split(".").pop()?.replace(/\[\d+\]$/, "") ?? "";
          if (retiredComponentVariantKeys.has(key)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.${column}.${pathLabel} still uses retired Component Variant key "${key}"`,
            );
          }
          if (typeof value === "string"
            && (retiredComponentVariantValues.has(value) || value.includes("::preset::"))) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.${column}.${pathLabel} still uses retired Component Variant value "${value}"`,
            );
          }
          if (typeof value === "string" && retiredRadiusTokens.has(value)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.${column}.${pathLabel} still references retired radius token "${value}"`,
            );
          }
        });
      }
      if (row.component_type === "keyboard") {
        walkJson(jsonParse(row.config_json), (_value, pathLabel) => {
          const key = pathLabel.split(".").pop()?.replace(/\[\d+\]$/, "") ?? "";
          if (retiredKeyboardKeys.has(key)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.config_json.${pathLabel} still uses retired keyboard-owned field "${key}"`,
            );
          }
        });
        walkJson(jsonParse(row.metadata_json), (_value, pathLabel) => {
          const key = pathLabel.split(".").pop()?.replace(/\[\d+\]$/, "") ?? "";
          if (retiredKeyboardKeys.has(key)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.metadata_json.${pathLabel} still uses retired keyboard-owned field "${key}"`,
            );
          }
        });
      }
    }

    const crossDomainDocuments = database.prepare(`
      SELECT 'module ' || id || '.config_json' AS owner, config_json AS document FROM modules
      UNION ALL SELECT 'module ' || id || '.design_preview_json', design_preview_json FROM modules
      UNION ALL SELECT 'module ' || id || '.metadata_json', metadata_json FROM modules
      UNION ALL SELECT 'module instance ' || id || '.transition_json', transition_json FROM module_instances
      UNION ALL SELECT 'module instance ' || id || '.content_json', content_json FROM module_instances
      UNION ALL SELECT 'module instance ' || id || '.behavior_json', behavior_json FROM module_instances
      UNION ALL SELECT 'module instance ' || id || '.animation_json', animation_json FROM module_instances
      UNION ALL SELECT 'module instance ' || id || '.metadata_json', metadata_json FROM module_instances
      UNION ALL SELECT 'theme ' || id || '.tokens_json', tokens_json FROM themes
      UNION ALL SELECT 'theme ' || id || '.metadata_json', metadata_json FROM themes
    `).all() as { owner: string; document: string }[];
    for (const row of crossDomainDocuments) {
      walkJson(jsonParse(row.document), (value, pathLabel) => {
        const key = pathLabel.split(".").pop()?.replace(/\[\d+\]$/, "") ?? "";
        if (retiredComponentVariantKeys.has(key)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.owner}.${pathLabel} still uses retired Component Variant key "${key}"`,
          );
        }
        if (typeof value === "string"
          && (retiredComponentVariantValues.has(value) || value.includes("::preset::"))) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.owner}.${pathLabel} still uses retired Component Variant value "${value}"`,
          );
        }
      });
    }

    const themeComponentReferences = database
      .prepare("SELECT id, status_bar_id, navigation_bar_id FROM themes")
      .all() as { id: string; status_bar_id: string; navigation_bar_id: string }[];
    for (const row of themeComponentReferences) {
      for (const [column, reference] of [
        ["status_bar_id", row.status_bar_id],
        ["navigation_bar_id", row.navigation_bar_id],
      ] as const) {
        if (reference.includes("::preset::")) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `theme ${row.id}.${column} still uses a retired Component Variant reference`,
          );
        }
      }
    }

    const themeRows = database
      .prepare("SELECT id, tokens_json FROM themes")
      .all() as { id: string; tokens_json: string }[];
    for (const row of themeRows) {
      const radii = jsonRecord(jsonRecord(jsonParse(row.tokens_json)).radii);
      for (const retired of ["control", "card", "panel", "surface", "pill", "avatar"]) {
        if (retired in radii) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.tokens_json.radii still contains retired radius key "${retired}"`,
          );
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopRuntimeCollectionsAreConsistent() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, design_preview_json FROM modules UNION ALL SELECT id, design_preview_json FROM component_classes")
      .all() as { id: string; design_preview_json: string }[];
    for (const row of rows) {
      const preview = jsonRecord(jsonParse(row.design_preview_json));
      const collections = jsonArray(preview.collections).map(jsonRecord);
      for (const collection of collections) {
        const jsonKey = typeof collection.jsonKey === "string" ? collection.jsonKey : "";
        const sourceKey = typeof collection.sourceCollectionJsonKey === "string"
          ? collection.sourceCollectionJsonKey
          : "";
        if (!sourceKey) continue;
        const sourceExists = Array.isArray(preview[sourceKey]);
        const sourceItems = jsonArray(preview[sourceKey]).map(jsonRecord);
        if (!sourceExists) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json collection "${jsonKey}" declares missing source "${sourceKey}"`,
          );
        }
        const ids = new Set<string>();
        for (const [index, item] of sourceItems.entries()) {
          const id = typeof item.id === "string" ? item.id : "";
          if (!id) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.${sourceKey}[${index}] must have stable id for sourced runtime overrides`,
            );
          } else if (ids.has(id)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.${sourceKey} has duplicate item id "${id}"`,
            );
          }
          ids.add(id);
        }
        const testValues = jsonRecord(preview.testValues);
        for (const overrideItem of jsonArray(testValues[jsonKey]).map(jsonRecord)) {
          const id = typeof overrideItem.id === "string" ? overrideItem.id : "";
          if (id && !ids.has(id)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.testValues.${jsonKey} has stale override for missing source item "${id}"`,
            );
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopPreviewActionsAreDeclarative() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, design_preview_json FROM modules UNION ALL SELECT id, design_preview_json FROM component_classes")
      .all() as { id: string; design_preview_json: string }[];
    const actionInventory = new Set<string>();
    for (const row of rows) {
      const preview = jsonRecord(jsonParse(row.design_preview_json));
      const assertAction = (action: JsonRecord, path: string, isItemAction: boolean) => {
        const id = typeof action.id === "string" ? action.id : "";
        const label = typeof action.label === "string" ? action.label : "";
        const timeJsonKey = typeof action.timeJsonKey === "string" ? action.timeJsonKey : "";
        const hasDuration = typeof action.durationInputId === "string"
          || typeof action.durationBehaviorTimingInputId === "string"
          || typeof action.durationCollectionJsonKey === "string"
          || typeof action.durationSeconds === "number"
          || typeof action.durationThemeToken === "string"
          || typeof action.durationMotionConfigPath === "string"
          || action.durationOwnerTimeline === true;
        if (!id || !label) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} must declare id and label`,
          );
        }
        if (isItemAction && (!timeJsonKey || typeof action.playInputId !== "string" || !hasDuration)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} item action must declare playInputId, a duration source and timeJsonKey`,
          );
        }
        if (!isItemAction && (typeof action.playInputId !== "string" || !timeJsonKey || !hasDuration)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} must declare playInputId, a duration source and timeJsonKey`,
          );
        }
        if ("durationThemeToken" in action
          && (typeof action.durationThemeToken !== "string" || !action.durationThemeToken.startsWith("theme."))) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path}.durationThemeToken must be a theme.* token`,
          );
        }
        if ("durationBehaviorTimingInputId" in action) {
          const inputId = typeof action.durationBehaviorTimingInputId === "string"
            ? action.durationBehaviorTimingInputId
            : "";
          const inputs = Array.isArray(preview.inputs) ? preview.inputs.map(jsonRecord) : [];
          const input = inputs.find((candidate) => candidate.id === inputId);
          if (!inputId || input?.valueKind !== "BehaviorTiming") {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.${path}.durationBehaviorTimingInputId must reference a BehaviorTiming input`,
            );
          }
        }
        if (action.completionBehavior !== "reset"
          && action.completionBehavior !== "holdFinal") {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path}.completionBehavior must be reset or holdFinal`,
          );
        }
        if (("durationCollectionJsonKey" in action || "durationItemNumberKeys" in action) && !timeJsonKey) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} duration action must declare timeJsonKey`,
          );
        }
        if ("durationCollectionJsonKey" in action && !Array.isArray(action.durationItemNumberKeys)) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json.${path} collection duration action must declare durationItemNumberKeys[]`,
          );
        }
      };
      for (const [index, action] of jsonArray(preview.actions).map(jsonRecord).entries()) {
        assertAction(action, `actions[${index}]`, false);
        actionInventory.add(`root:${String(preview.componentType ?? "module")}:${String(action.id ?? "")}`);
      }
      for (const [collectionIndex, collection] of jsonArray(preview.collections).map(jsonRecord).entries()) {
        for (const [actionIndex, action] of jsonArray(collection.itemActions).map(jsonRecord).entries()) {
          assertAction(action, `collections[${collectionIndex}].itemActions[${actionIndex}]`, true);
          const values = jsonArray(action.visibleWhenItemValues).join(",");
          actionInventory.add(
            `item:${String(collection.jsonKey ?? "")}:${String(action.id ?? "")}:${String(action.visibleWhenItemJsonKey ?? "")}:${values}`,
          );
        }
      }
    }
    for (const required of [
      "root:module:playConversation",
      "root:keyboard:in",
      "root:audio:play",
      "root:media:play",
      "root:media:fullScreen",
      "root:bubble:writeOn",
      "root:bubble:play",
      "root:bubble:fullScreen",
      "item:messages:playVideo:mediaType:video",
      "item:messages:playAudio:mediaType:audio",
    ]) {
      if (!actionInventory.has(required)) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `required declarative Test Values action is missing: ${required}`,
        );
      }
    }
  } finally {
    database.close();
  }
}

function assertComponentEditorLayoutsUseKnownFields() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const catalog = readFileSync(
    path.join(root, "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs"),
    "utf8",
  );
  const knownFields = new Set(
    [...catalog.matchAll(/^\s*\["([^"]+)"\]\s*=/gm)].map((match) => match[1]),
  );
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database.prepare(
      "SELECT record_class_id, layout_json FROM editor_layouts WHERE record_class_id LIKE 'component.%'",
    ).all() as { record_class_id: string; layout_json: string }[];
    for (const row of rows) {
      const layout = jsonRecord(jsonParse(row.layout_json));
      for (const card of jsonArray(layout.cards).map(jsonRecord)) {
        for (const group of jsonArray(card.groups).map(jsonRecord)) {
          for (const field of jsonArray(group.fields).map(jsonRecord)) {
            const fieldId = typeof field.id === "string" ? field.id : "";
            if (fieldId.startsWith("component.") && !knownFields.has(fieldId)) {
              addViolation(
                "data/desktop-editor-spike.sqlite",
                `${row.record_class_id} editor layout references unknown field ${fieldId}`,
              );
            }
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopConversationPreviewDoesNotUseLegacyMessageKeys() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const legacyMessageKeys = new Set([
    "message1Text",
    "message2Text",
    "message3Text",
    "message2StatusState",
    "message2StatusText",
  ]);
  const perMessageTimingKeys = new Set([
    "bubbleRevealMode",
    "textInputVisible",
    "keyboardVisible",
    "textReveal",
    "writeOnDurationFrames",
  ]);
  const requiredMessageTimingKeys = new Set([
    "writeOnTiming",
    "postWriteOnHoldFrames",
  ]);

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const rows = database
      .prepare("SELECT id, design_preview_json FROM modules UNION ALL SELECT id, design_preview_json FROM component_classes")
      .all() as { id: string; design_preview_json: string }[];
    for (const row of rows) {
      const preview = jsonRecord(jsonParse(row.design_preview_json));
      for (const key of legacyMessageKeys) {
        if (key in preview) {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `${row.id}.design_preview_json still uses legacy direct conversation preview key "${key}"`,
          );
        }
      }
      for (const [index, message] of jsonArray(preview.messages).map(jsonRecord).entries()) {
        for (const key of requiredMessageTimingKeys) {
          if (!(key in message)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.messages[${index}] is missing per-message timing key "${key}"`,
            );
          }
        }
        for (const key of perMessageTimingKeys) {
          if (key in message) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.design_preview_json.messages[${index}] still uses per-message timing key "${key}"`,
            );
          }
        }
      }
    }

    const instanceRows = database
      .prepare("SELECT id, content_json FROM module_instances")
      .all() as { id: string; content_json: string }[];
    for (const row of instanceRows) {
      const content = jsonRecord(jsonParse(row.content_json));
      for (const [index, message] of jsonArray(content.messages).map(jsonRecord).entries()) {
        for (const key of requiredMessageTimingKeys) {
          if (!(key in message)) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.content_json.messages[${index}] is missing per-message timing key "${key}"`,
            );
          }
        }
        for (const key of perMessageTimingKeys) {
          if (key in message) {
            addViolation(
              "data/desktop-editor-spike.sqlite",
              `${row.id}.content_json.messages[${index}] still uses per-message timing key "${key}"`,
            );
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertGenericTextWrappingIsConservative() {
  const sample = "Mensaje de prueba 😎😍 en dos líneas";
  const fontSize = 18;
  const maxWidth = 250;
  const lines = approximateWrappedTextLines(sample, fontSize, maxWidth);
  if (lines.length < 2) {
    addViolation(
      "src/desktop-preview/previewTextHelpers.ts",
      "generic text wrapping must wrap latin text with emoji before it reaches the frame edge",
    );
    return;
  }

  for (const [index, line] of lines.entries()) {
    if (approximateTextWidth(line, fontSize) > maxWidth) {
      addViolation(
        "src/desktop-preview/previewTextHelpers.ts",
        `generic text wrapping produced over-wide line ${index + 1}: "${line}"`,
      );
    }
  }
}

assertDesktopDatabaseDoesNotContainRetiredTokens();
assertDesktopRuntimeCollectionsAreConsistent();
assertDesktopPreviewActionsAreDeclarative();
assertComponentEditorLayoutsUseKnownFields();
assertDesktopConversationPreviewDoesNotUseLegacyMessageKeys();
assertGenericTextWrappingIsConservative();
for (const filePath of walkFiles(previewRoot)) {
  const file = relative(filePath);
  if (file !== "src/desktop-preview/previewTextHelpers.ts"
      && readFileSync(filePath, "utf8").includes("approximateText")) {
    addViolation(file, "production preview layout must use resolved production-font measurement");
  }
}
assertMatches(
  "src/desktop-preview/conversationModuleRenderable.ts",
  /childRenderable\(\s*payload,[\s\S]*?"keyboard"[\s\S]*?\{\s*text: composer\.text,[\s\S]*?currentCharacter: composer\.currentCharacter,[\s\S]*?motionElapsedMs,/,
  "Conversation must pass shared module motionElapsedMs to Keyboard runtime inputs",
);
for (const legacyConversationKey of [
  "message1Text",
  "message2Text",
  "message3Text",
  "message2StatusState",
  "message2StatusText",
  "instanceMessages",
]) {
  assertDoesNotContain(
    "src/desktop-preview/conversationModuleRenderable.ts",
    legacyConversationKey,
    `Conversation renderer must consume the canonical messages[] runtime collection instead of legacy key "${legacyConversationKey}"`,
  );
}

if (existsSync(path.join(previewRoot, "webPreviewBridge.ts"))) {
  addViolation(
    "src/desktop-preview/webPreviewBridge.ts",
    "central web preview bridge must not exist",
  );
}

for (const removedLegacyPath of [
  "src/debug-ui",
  "src/debug-server",
  "src/electron",
  "src/remotion",
  "src/visual/adapters/react",
  "src/visual/renderable/helpers.ts",
  "src/visual/layout",
  "src/visual/modules",
  "src/visual/validation",
  "src/domain",
  "src/persistence",
  "src/icon-themes/importDevelopmentIconTheme.ts",
  "src/desktop-preview/systemBarComponentContract.ts",
  "src/desktop-preview/systemBarPreviewResolver.ts",
  "src/desktop-preview/systemBarRenderables.ts",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassNormalization.cs",
  "index.html",
  "remotion.config.ts",
  "vite.config.ts",
]) {
  if (existsSync(path.join(root, removedLegacyPath))) {
    addViolation(
      removedLegacyPath,
      "legacy React/debug/remotion render route must not be restored in this repository",
    );
  }
}

for (const filePath of walkFiles(previewRoot)) {
  const source = readFileSync(filePath, "utf8");
  for (const legacyImport of ["../domain/", "../persistence/"]) {
    if (source.includes(legacyImport)) {
      addViolation(
        relative(filePath),
        `desktop preview must not import active behavior from historical ${legacyImport} code`,
      );
    }
  }
}

assertNoTerms("src/desktop-preview/renderDesignPreviewHtml.tsx", [
  "label",
  "avatar",
  "buttonIcon",
  "audio",
  "textBox",
  "textInputBar",
  "keyboard",
  "media",
  "video",
  "statusBar",
  "navigationBar",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
  "component_text_box",
  "component_text_input",
  "component_keyboard",
  "component_media",
  "component_video",
  "status_bar",
  "navigation_bar",
]);
assertDoesNotContain(
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
  "../visual/adapters/react/RenderableReactAdapter.js",
  "desktop design preview must use the clean desktop HTML adapter, not the legacy React renderable adapter",
);
for (const legacyScriptTerm of [
  "validate:examples",
  "validate:resolver",
  "validate:sqlite",
  "db:reset",
  "db:seed",
  "audit:current-model",
  "db:normalize-current-model",
]) {
  assertPackageScriptDoesNotContain(
    "test",
    legacyScriptTerm,
    `desktop test path must not call legacy script ${legacyScriptTerm}`,
  );
}
assertNoTerms("src/desktop-preview/DesktopRenderableHtmlAdapter.tsx", [
  "component_preview_unsupported",
  "design_preview_surface",
  "icon_token",
  "message_bubble",
  "audio_message",
  "button_icon",
  "status_bar",
  "status_bar_item",
  "navigation_bar",
  "navigation_bar_item",
  "keyboard_key",
  "text_input_bar_",
  "video_message",
  "status_indicators",
]);
assertDoesNotContain(
  "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
  "./previewColorHelpers.js",
  "desktop HTML adapter must not import token/color resolution helpers",
);
assertDoesNotContain(
  "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
  "iconTokenLabel",
  "desktop HTML adapter icon fallback must not use token-specific naming",
);
assertContains(
  "src/visual/renderable/types.ts",
  "export interface RenderableMetadata",
  "renderable metadata must stay explicitly typed",
);
assertContains(
  "src/visual/renderable/types.ts",
  "export const renderableNodeTypes",
  "renderable primitive list must stay explicit and shared",
);
assertContains(
  "src/visual/renderable/types.ts",
  "export type RenderableNodeType = (typeof renderableNodeTypes)[number];",
  "renderable node type must be derived from the shared primitive list",
);
assertContains(
  "src/visual/renderable/types.ts",
  "type: RenderableNodeType;",
  "renderable node type must not accept arbitrary strings",
);
assertDoesNotContain(
  "src/visual/renderable/types.ts",
  "metadata?: Record<string, unknown>",
  "renderable nodes must not expose arbitrary metadata",
);
assertContains(
  "src/visual/renderable/schema.ts",
  "const RenderableMetadataSchema",
  "renderable schema must validate metadata through an explicit schema",
);
assertDoesNotContain(
  "src/visual/renderable/schema.ts",
  "metadata: z.record(z.string(), z.unknown()).optional()",
  "renderable schema must not accept arbitrary metadata",
);
assertContains(
  "src/visual/renderable/schema.ts",
  "const RenderableNodeTypeSchema = z.enum(renderableNodeTypes);",
  "renderable schema must validate node type through the shared primitive list",
);
assertContains(
  "src/visual/renderable/schema.ts",
  "type: RenderableNodeTypeSchema,",
  "renderable schema must validate node type with the explicit primitive enum",
);
assertStringSetEquals(
  "src/visual/renderable/types.ts",
  quotedStringsFromBlock(
    "src/visual/renderable/types.ts",
    /export const renderableNodeTypes\s*=\s*\[([\s\S]*?)\]\s*as const;/,
    "renderable primitive list must be parseable",
  ),
  desktopPreviewPaintNodeTypes,
  "shared renderable primitive list",
);

assertNoTerms("src/desktop-preview/componentRenderableCommon.ts", [
  "label",
  "avatar",
  "buttonIcon",
  "audio",
  "textBox",
  "textInputBar",
  "keyboard",
  "media",
  "video",
  "statusBar",
  "navigationBar",
  "waveform",
  "badge",
  "component_label",
  "component_avatar",
  "component_button",
  "component_audio",
  "component_text_box",
  "component_text_input",
  "component_keyboard",
  "component_media",
  "component_video",
]);
assertContains(
  "src/desktop-preview/componentResolverCommon.ts",
  "from \"./previewComponentContracts.js\"",
  "component resolver common must re-export shared contracts instead of defining them locally",
);
assertContains(
  "src/desktop-preview/componentResolverCommon.ts",
  "from \"./previewValueHelpers.js\"",
  "component resolver common must re-export shared value helpers instead of defining them locally",
);
assertContains(
  "src/desktop-preview/componentResolverCommon.ts",
  "from \"./previewJsonHelpers.js\"",
  "component resolver common must re-export shared JSON helpers instead of defining them locally",
);

const centralCommonFiles = new Set([
  "src/desktop-preview/componentRenderableCommon.ts",
  "src/desktop-preview/componentResolverCommon.ts",
  "src/desktop-preview/designPreviewPayload.ts",
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
]);

for (const relativePath of centralCommonFiles) {
  const imports = importTargets(readText(relativePath));
  for (const target of imports) {
    if (
      /Component(Resolver|Renderable)\.js$/.test(target) ||
      /systemBar.*\.js$/.test(target)
    ) {
      addViolation(relativePath, `central/common file imports concrete preview module "${target}"`);
    }
  }
}

const registryFiles = new Set([
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "src/desktop-preview/designPreviewRenderableRegistry.ts",
  "src/desktop-preview/moduleRenderableRegistry.ts",
]);

const sharedPreviewHelperFiles = new Set([
  "src/desktop-preview/previewColorHelpers.ts",
  "src/desktop-preview/previewComponentContracts.ts",
  "src/desktop-preview/previewGeometryHelpers.ts",
  "src/desktop-preview/previewJsonHelpers.ts",
  "src/desktop-preview/previewSurfaceHelpers.ts",
  "src/desktop-preview/previewValueHelpers.ts",
]);

const filesystemAllowedPreviewFiles = new Set([
  "src/desktop-preview/previewAssetResolver.ts",
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
  "src/desktop-preview/renderPreviewRasterServer.ts",
]);

const manifestEntries = Object.entries(desktopPreviewComponents);
const moduleManifestEntries = Object.entries(desktopPreviewModules);

function moduleFile(entry: DesktopPreviewComponentManifestEntry, kind: "resolver" | "renderable") {
  return `src/desktop-preview/${entry[kind].replace(/^\.\//, "")}.ts`;
}

function moduleImport(entry: DesktopPreviewComponentManifestEntry, kind: "resolver" | "renderable") {
  return `${entry[kind]}.js`;
}

function moduleEntrypointFile(entry: DesktopPreviewModuleManifestEntry, kind: "resolver" | "renderable") {
  return `src/desktop-preview/${entry[kind].replace(/^\.\//, "")}.ts`;
}

function moduleEntrypointImport(entry: DesktopPreviewModuleManifestEntry, kind: "resolver" | "renderable") {
  return `${entry[kind]}.js`;
}

for (const [componentClass, entry] of manifestEntries) {
  for (const kind of ["contract", "resolver", "renderable"] as const) {
    const filePath = path.join(previewRoot, `${entry[kind].replace(/^\.\//, "")}.ts`);
    if (!existsSync(filePath)) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `manifest entry "${componentClass}" points to missing ${kind} file "${entry[kind]}"`,
      );
    }
  }

  for (const child of entry.embeds) {
    if (!desktopPreviewComponents[child]) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `manifest entry "${componentClass}" embeds unknown component "${child}"`,
      );
    }
  }
}

for (const [moduleClass, entry] of moduleManifestEntries) {
  for (const kind of ["resolver", "renderable"] as const) {
    const filePath = path.join(previewRoot, `${entry[kind].replace(/^\.\//, "")}.ts`);
    if (!existsSync(filePath)) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `module manifest entry "${moduleClass}" points to missing ${kind} file "${entry[kind]}"`,
      );
    }
  }
  for (const child of entry.embeds) {
    if (!desktopPreviewComponents[child]) {
      addViolation(
        "src/desktop-preview/desktopPreviewManifest.json",
        `module manifest entry "${moduleClass}" embeds unknown component "${child}"`,
      );
    }
  }
}

for (const [, entry] of manifestEntries) {
  const renderableFile = moduleFile(entry, "renderable");
  assertDoesNotContain(
    renderableFile,
    "componentType:",
    "component renderables must not emit component identity metadata into the final paint tree",
  );
  assertDoesNotContain(
    renderableFile,
    "systemBarType:",
    "system component renderables must not emit system-bar identity metadata into the final paint tree",
  );
}
for (const [, entry] of manifestEntries) {
  const renderableFile = moduleFile(entry, "renderable");
  for (const legacyTerm of [
    "message_bubble",
    "audio_message",
    "button_icon",
    "text_input_bar",
    "keyboard_key",
    "video_message",
  ]) {
    assertDoesNotContain(
      renderableFile,
      legacyTerm,
      `component renderables must not emit legacy desktop/runtime term "${legacyTerm}"`,
    );
  }
}

const routedComponentClasses = new Set(Object.keys(componentRenderableFactories));
for (const [componentClass, entry] of manifestEntries) {
  if (!routedComponentClasses.has(componentClass)) {
    addViolation(
      "src/desktop-preview/componentClassRenderableRegistry.ts",
      `component class "${componentClass}" is missing from component renderable registry`,
    );
  }
}

const routedModuleClasses = new Set(Object.keys(moduleRenderableFactories));
for (const [moduleClass] of moduleManifestEntries) {
  if (!routedModuleClasses.has(moduleClass)) {
    addViolation(
      "src/desktop-preview/moduleRenderableRegistry.ts",
      `module class "${moduleClass}" is missing from module renderable registry`,
    );
  }
}
for (const moduleClass of routedModuleClasses) {
  if (!desktopPreviewModules[moduleClass]) {
    addViolation(
      "src/desktop-preview/moduleRenderableRegistry.ts",
      `module renderable registry contains unknown class "${moduleClass}"`,
    );
  }
}

for (const [componentClass, entry] of manifestEntries) {
  if (!["component", "atom", "system"].includes(entry.category)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `component "${componentClass}" has unsupported category "${entry.category}"`,
    );
  }
  if (!["functional", "structural"].includes(entry.migrationStatus)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `component "${componentClass}" has unsupported migration status "${entry.migrationStatus}"`,
    );
  }
  if (new Set(entry.embeds).size !== entry.embeds.length) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `component "${componentClass}" has duplicate embedded dependencies`,
    );
  }
}
for (const [moduleClass, entry] of moduleManifestEntries) {
  if (!entry.label.trim()) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `module "${moduleClass}" has no label`,
    );
  }
  if (new Set(entry.embeds).size !== entry.embeds.length) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `module "${moduleClass}" has duplicate embedded dependencies`,
    );
  }
}

for (const registryPath of registryFiles) {
  assertNoTerms(registryPath, [
    "applyRuntimeInputForwarding",
    "runtimeContractJson:",
    "designPreviewJson:",
    "backgroundColor:",
    "previewFrame.screen",
  ]);
}
assertContains(
  "src/desktop-preview/componentRenderableBoundary.ts",
  "resolveRenderablePayload(payload)",
  "component payload preparation must happen before component registry routing",
);
assertContains(
  "src/desktop-preview/moduleRenderableBoundary.ts",
  "resolveRenderablePayload(payload)",
  "module payload preparation must happen before module registry routing",
);
assertContains(
  "src/desktop-preview/renderablePayloadBoundary.ts",
  "applyRuntimeInputForwarding(payload)",
  "runtime forwarding must belong to the payload boundary",
);
for (const recursivePayloadPath of [
  "src/desktop-preview/previewPayloadHelpers.ts",
  "src/desktop-preview/lockScreenModuleRenderable.ts",
  "src/desktop-preview/conversationModuleRenderable.ts",
  "src/desktop-preview/notificationsComponentResolver.ts",
]) {
  assertDoesNotContain(
    recursivePayloadPath,
    "runtimeContractJson:",
    "embedded component composition must preserve the complete temporal owner envelope",
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "var runtimePreviewJson = runtimePreview.ToJsonString();",
  "desktop payloads must materialize one explicit effective temporal owner envelope",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "RuntimeContractJson = preview.ToJsonString()",
  "isolated Design Test Values must update the effective temporal owner envelope without persistence",
);
for (const componentClass of routedComponentClasses) {
  if (!desktopPreviewComponents[componentClass]) {
    addViolation(
      "src/desktop-preview/componentClassRenderableRegistry.ts",
      `component renderable registry contains unknown class "${componentClass}"`,
    );
  }
}

const allowedComponentImports: Record<string, Set<string>> = {};
const desktopPreviewPaintTreeSourceFiles = new Set([
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "src/desktop-preview/designPreviewRenderableRegistry.ts",
  "src/desktop-preview/renderDesignPreviewHtml.tsx",
]);
const manifestComponentEntrypointImports = new Set<string>();
for (const [, entry] of manifestEntries) {
  desktopPreviewPaintTreeSourceFiles.add(moduleFile(entry, "renderable"));
  manifestComponentEntrypointImports.add(moduleImport(entry, "resolver"));
  manifestComponentEntrypointImports.add(moduleImport(entry, "renderable"));
  for (const kind of ["resolver", "renderable"] as const) {
    const filePath = moduleFile(entry, kind);
    const allowed = allowedComponentImports[filePath] ?? new Set<string>();
    for (const child of entry.embeds) {
      const childEntry = desktopPreviewComponents[child];
      if (childEntry) {
        allowed.add(moduleImport(childEntry, kind));
      }
    }
    allowedComponentImports[filePath] = allowed;
  }
}

const manifestModuleEntrypointImports = new Set<string>();
const allowedOwningModuleImports: Record<string, Set<string>> = {};
for (const [, entry] of moduleManifestEntries) {
  desktopPreviewPaintTreeSourceFiles.add(moduleEntrypointFile(entry, "renderable"));
  manifestModuleEntrypointImports.add(moduleEntrypointImport(entry, "resolver"));
  manifestModuleEntrypointImports.add(moduleEntrypointImport(entry, "renderable"));
  const ownerFiles = [
    moduleEntrypointFile(entry, "resolver"),
    moduleEntrypointFile(entry, "renderable"),
  ];
  const ownerImports = new Set([
    moduleEntrypointImport(entry, "resolver"),
    moduleEntrypointImport(entry, "renderable"),
  ]);
  for (const ownerFile of ownerFiles) allowedOwningModuleImports[ownerFile] = ownerImports;
  for (const kind of ["resolver", "renderable"] as const) {
    const filePath = moduleEntrypointFile(entry, kind);
    const allowed = allowedComponentImports[filePath] ?? new Set<string>();
    for (const child of entry.embeds) {
      const childEntry = desktopPreviewComponents[child];
      if (childEntry) {
        allowed.add(moduleImport(childEntry, "resolver"));
        allowed.add(moduleImport(childEntry, "renderable"));
      }
    }
    allowedComponentImports[filePath] = allowed;
  }
}

for (const filePath of walkFiles(previewRoot)) {
  const relativePath = relative(filePath);
  const source = readFileSync(filePath, "utf8");
  if (!sharedPreviewHelperFiles.has(relativePath)) {
    for (const helperName of [
      "applyNeutralTint",
      "asRecord",
      "colorForMode",
      "cssColorWithAlpha",
      "numberValue",
      "parseObject",
      "renderScale",
      "requiredAlpha",
      "requiredBoolean",
      "requiredNumber",
      "requiredNumberPair",
      "requiredNumberValue",
      "requiredPlacement",
      "requiredRecord",
      "requiredString",
      "resolvePaletteColor",
      "resolveSurfaceStyle",
      "selectedColor",
      "stringValue",
      "tokenValueForMode",
      "variants",
    ]) {
      if (new RegExp(`function\\s+${helperName}\\s*\\(`).test(source)) {
        addViolation(
          relativePath,
          `shared preview helper "${helperName}" must be imported from common helpers, not redefined locally`,
        );
      }
    }
  }

  if (desktopPreviewPaintTreeSourceFiles.has(relativePath) && /role:\s*["']/.test(source)) {
    addViolation(
      relativePath,
      "desktop preview paint tree nodes must not emit role metadata; use generic node types and marks only",
    );
  }
  if (desktopPreviewPaintTreeSourceFiles.has(relativePath) && /metadata:\s*\{\s*\.\.\./.test(source)) {
    addViolation(
      relativePath,
      "desktop preview paint tree metadata must not spread component contracts into final nodes",
    );
  }
  if (desktopPreviewPaintTreeSourceFiles.has(relativePath)) {
    const metadataPattern = /metadata:\s*\{[\s\S]*?\}/g;
    let metadataMatch: RegExpExecArray | null;
    while ((metadataMatch = metadataPattern.exec(source)) !== null) {
      const metadataSource = metadataMatch[0];
      for (const forbiddenMetadataKey of [
        "charging",
        "componentType",
        "kind",
        "label",
        "order",
        "route",
        "value",
        "zone",
      ]) {
        if (new RegExp(`\\b${forbiddenMetadataKey}\\s*:`).test(metadataSource)) {
          addViolation(
            relativePath,
            `desktop preview paint tree metadata must not contain component semantic key "${forbiddenMetadataKey}"`,
          );
        }
      }
    }
  }

  const nodeTypePattern = /type:\s*["']([^"']+)["']/g;
  let nodeTypeMatch: RegExpExecArray | null;
  while ((nodeTypeMatch = nodeTypePattern.exec(source)) !== null) {
    const nodeType = nodeTypeMatch[1] ?? "";
    if (nodeType.startsWith("component_")) {
      addViolation(
        relativePath,
        `component-specific renderable node type "${nodeType}" must be a generic primitive type`,
      );
    }
    if (forbiddenDesktopPreviewNodeTypes.has(nodeType)) {
      addViolation(
        relativePath,
        `desktop preview semantic node type "${nodeType}" must be emitted as generic primitives`,
      );
    }
    if (
      desktopPreviewPaintTreeSourceFiles.has(relativePath)
      && !desktopPreviewPaintNodeTypes.has(nodeType)
    ) {
      addViolation(
        relativePath,
        `desktop preview paint node type "${nodeType}" is not in the generic primitive allowlist`,
      );
    }
  }
  if (registryFiles.has(relativePath)) continue;

  const imports = importTargets(source);
  for (const target of imports) {
    if (
      (target === "node:fs" || target === "node:fs/promises")
      && !filesystemAllowedPreviewFiles.has(relativePath)
    ) {
      addViolation(
        relativePath,
        `filesystem access "${target}" belongs in preview asset/request boundary helpers only`,
      );
    }
  }

  for (const target of imports) {
    const isConcreteComponentImport = manifestComponentEntrypointImports.has(target);
    if (!isConcreteComponentImport) continue;

    const allowed = allowedComponentImports[relativePath];
    if (!allowed?.has(target)) {
      addViolation(
        relativePath,
        `concrete component import "${target}" is not a declared embedded-component dependency`,
      );
    }
  }

  for (const target of imports) {
    if (!manifestModuleEntrypointImports.has(target)) continue;
    if (relativePath !== "src/desktop-preview/moduleRenderableRegistry.ts"
        && !allowedOwningModuleImports[relativePath]?.has(target)) {
      addViolation(
        relativePath,
        `concrete module entrypoint import "${target}" is allowed only in the module registry`,
      );
    }
  }
}

const payloadSource = readText("src/desktop-preview/designPreviewPayload.ts");
{
  const adapterSource = readText("src/desktop-preview/DesktopRenderableHtmlAdapter.tsx");
  assertStringSetEquals(
    "src/visual/renderable/types.ts",
    new Set(renderableNodeTypes),
    desktopPreviewPaintNodeTypes,
    "runtime renderable primitive list",
  );
  if (!adapterSource.includes("const supportedNodeTypes = new Set(renderableNodeTypes);")) {
    addViolation(
      "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
      "desktop renderer must use the shared renderable primitive list",
    );
  }
}
if (payloadSource.includes("device:")) {
  addViolation(
    "src/desktop-preview/designPreviewPayload.ts",
    "design preview payload must expose previewFrame, not device",
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "device =",
  "web design preview renderer must serialize previewFrame, not a device object",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "previewFrame = new",
  "web design preview renderer must provide previewFrame geometry to the web renderer",
);
for (const componentType of Object.keys(desktopPreviewComponents)) {
  assertDoesNotContain(
    "src/visual/renderable/rasterFramePlan.ts",
    componentType,
    `generic raster frame planning must not contain component-specific knowledge (${componentType})`,
  );
}
assertContains(
  "src/visual/renderable/rasterFramePlan.ts",
  'kind: "hold"',
  "raster frame planning must represent visually unchanged frames without duplicate bitmaps",
);
assertContains(
  "src/desktop-preview/renderDesignPreviewHtmlServer.ts",
  "assets: compact.assets",
  "the persistent renderer must return compact HTML with an incremental asset manifest",
);
assertContains(
  "src/desktop-preview/renderPreviewRasterServer.ts",
  'format: "webp"',
  "the shared Chromium raster boundary must support WebP preview frames",
);
assertContains(
  "src/desktop-preview/renderPreviewRasterServer.ts",
  'type: "png"',
  "the shared Chromium raster boundary must preserve a PNG lossless GFX path",
);
assertContains(
  "src/visual/renderable/rasterFramePlan.ts",
  'kind: "tiles"',
  "raster frame planning must represent localized visual changes as tiles",
);
if (payloadSource.includes('"statusBar"') || payloadSource.includes('"navigationBar"')) {
  addViolation(
    "src/desktop-preview/designPreviewPayload.ts",
    "status/navigation bars must route as componentClass, not top-level preview kinds",
  );
}

const desktopPersistenceDataPaths = readdirSync(path.join(root, "spikes/desktop-editor-shell/Data"))
  .filter((entry) => entry.endsWith(".cs"))
  .map((entry) => `spikes/desktop-editor-shell/Data/${entry}`);
for (const relativePath of desktopPersistenceDataPaths) {
  const source = readText(relativePath);
  if (/\b(?:Ensure|Normalize|Retire|Migrate)\w*\s*\(\s*SqliteConnection\b/.test(source)) {
    addViolation(
      relativePath,
      "normal runtime data code must not retain database-wide compatibility or repair routines",
    );
  }
  if (/\bSeed\w*IfEmpty\s*\(/.test(source)) {
    addViolation(
      relativePath,
      "normal runtime data code must not seed partially missing current data",
    );
  }
}
assertContains(
  "spikes/desktop-editor-shell/Data/SqliteProjectContext.cs",
  "Mode = SqliteOpenMode.ReadOnly",
  "the shared SQLite context must open existing databases read-only until strict current validation succeeds",
);
for (const [contractType, implementationType] of [
  ["IEditorLayoutRepository", "EditorLayoutRepository"],
  ["IProjectEpisodeRepository", "ProjectEpisodeRepository"],
  ["IShotRepository", "ShotRepository"],
  ["IRenderPresetRepository", "RenderPresetRepository"],
  ["IPaletteRepository", "PaletteRepository"],
  ["IDeviceRepository", "DeviceRepository"],
  ["IActorRepository", "ActorRepository"],
  ["IThemeRepository", "ThemeRepository"],
  ["IProductionFontRepository", "ProductionFontRepository"],
  ["IIconThemeRepository", "IconThemeRepository"],
  ["IAppModuleRepository", "AppModuleRepository"],
  ["IModuleInstanceThemeContextService", "ModuleInstanceThemeContextService"],
  ["IReferenceUsageService", "ReferenceUsageService"],
] as const) {
  assertContains(
    "spikes/desktop-editor-shell/Data/PersistenceContracts.cs",
    `interface ${contractType}`,
    `persistence contract ${contractType} must remain explicit`,
  );
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
    implementationType === "ProjectEpisodeRepository"
      ? "new ProjectEpisodeRepository(_context, _shotRepository)"
      : `new ${implementationType}(_context)`,
    `SpikeDatabase must delegate through ${implementationType}`,
  );
}
for (const facadePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.EditorLayouts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectsEpisodes.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RenderPresets.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Palette.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Devices.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Actors.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProductionFonts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemeSearch.cs",
]) {
  for (const forbiddenPersistenceDetail of [".CreateCommand()", "SELECT ", "INSERT INTO ", "UPDATE ", "DELETE FROM "]) {
    assertDoesNotContain(
      facadePath,
      forbiddenPersistenceDetail,
      `${facadePath} must delegate instead of owning extracted repository SQL or row mapping`,
    );
  }
}
for (const [repositoryPath, ownedTable] of [
  ["spikes/desktop-editor-shell/Data/EditorLayoutRepository.cs", "editor_layouts"],
  ["spikes/desktop-editor-shell/Data/ProjectEpisodeRepository.cs", "episodes"],
  ["spikes/desktop-editor-shell/Data/ShotRepository.cs", "shots"],
  ["spikes/desktop-editor-shell/Data/RenderPresetRepository.cs", "render_presets"],
  ["spikes/desktop-editor-shell/Data/PaletteRepository.cs", "palette_colors"],
  ["spikes/desktop-editor-shell/Data/DeviceRepository.cs", "devices"],
  ["spikes/desktop-editor-shell/Data/ActorRepository.cs", "actors"],
  ["spikes/desktop-editor-shell/Data/ThemeRepository.cs", "themes"],
  ["spikes/desktop-editor-shell/Data/ProductionFontRepository.cs", "production_fonts"],
  ["spikes/desktop-editor-shell/Data/IconThemeRepository.cs", "icon_themes"],
  ["spikes/desktop-editor-shell/Data/AppModuleRepository.cs", "apps"],
] as const) {
  assertContains(
    repositoryPath,
    ownedTable,
    `${repositoryPath} must own its declared persistence slice`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "SqliteProjectContext",
  "MainWindow must not receive persistence infrastructure or repositories",
);
for (const ownedResourceTable of ["palette_colors", "devices", "actors", "production_fonts", "icon_themes"]) {
  for (const sqlOperation of ["INSERT INTO", "UPDATE", "DELETE FROM"]) {
    assertDoesNotContain(
      "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
      `${sqlOperation} ${ownedResourceTable}`,
      `tree orchestration must delegate ${ownedResourceTable} lifecycle writes to its repository`,
    );
  }
}
for (const sqlOperation of ["INSERT INTO", "UPDATE", "DELETE FROM"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
    `${sqlOperation} themes`,
    "tree orchestration must delegate Theme lifecycle writes to ThemeRepository",
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs",
  "?? themes.FirstOrDefault((row) => row.ProjectId == projectId)",
  "Theme token inspection must require the exact selected Theme instead of falling back to the first Project Theme",
);
for (const resourceRepositoryPath of [
  "spikes/desktop-editor-shell/Data/PaletteRepository.cs",
  "spikes/desktop-editor-shell/Data/DeviceRepository.cs",
  "spikes/desktop-editor-shell/Data/ActorRepository.cs",
  "spikes/desktop-editor-shell/Data/ThemeRepository.cs",
  "spikes/desktop-editor-shell/Data/ProductionFontRepository.cs",
  "spikes/desktop-editor-shell/Data/IconThemeRepository.cs",
]) {
  assertDoesNotContain(
    resourceRepositoryPath,
    "MainWindow",
    `${resourceRepositoryPath} must not import or construct the desktop shell`,
  );
  assertDoesNotContain(
    resourceRepositoryPath,
    "LIKE $needle",
    `${resourceRepositoryPath} must not copy the broad inferred Usage scanner`,
  );
  assertDoesNotContain(
    resourceRepositoryPath,
    "ReferenceSearchTables",
    `${resourceRepositoryPath} must not infer Usage from text-column discovery`,
  );
}
for (const forbiddenProductionFontRepositoryConcern of [
  "System.IO",
  "File.",
  "Directory.",
  "ProductionFontFace",
  "ProjectTreeNode",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ProductionFontRepository.cs",
    forbiddenProductionFontRepositoryConcern,
    `ProductionFontRepository must not own filesystem, Preview or tree concern ${forbiddenProductionFontRepositoryConcern}`,
  );
}
for (const productionFontDocumentConsumer of [
  "spikes/desktop-editor-shell/Data/ProductionFontRepository.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProductionFonts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
]) {
  assertContains(
    productionFontDocumentConsumer,
    "ProductionFontFilesContract.ParseRequired",
    `${productionFontDocumentConsumer} must consume the shared current Production Font file document`,
  );
}
for (const forbiddenProductionFontDocumentFallback of [
  ".OfType<JsonObject>()",
  "int.TryParse(weightText",
  "? weight : 400",
  "== \"italic\" ? \"italic\" : \"normal\"",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ProductionFonts.cs",
    forbiddenProductionFontDocumentFallback,
    `Production Font readers must not filter or infer current file member '${forbiddenProductionFontDocumentFallback}'`,
  );
}
for (const requiredProductionFontDocumentTerm of [
  "JsonPath.RequiredString(file, \"fileName\"",
  "JsonPath.RequiredString(file, \"relativePath\"",
  "JsonPath.RequiredString(file, \"style\"",
  "JsonPath.RequiredInteger(file, \"weight\"",
  "duplicate relativePath",
  "normalized safe relative path",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Common/ProductionFontFilesContract.cs",
    requiredProductionFontDocumentTerm,
    `Production Font file documents must require ${requiredProductionFontDocumentTerm}`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/42_production_font_persistence_contract.md",
  "AGENTS must require the Production Font persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "42_production_font_persistence_contract.md",
  "the active architecture index must include the Production Font persistence contract",
);
for (const forbiddenIconThemeRepositoryConcern of [
  "System.IO",
  "File.",
  "Directory.",
  "Svg",
  "Process",
  "ProjectTreeNode",
  "IconThemeToken",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/IconThemeRepository.cs",
    forbiddenIconThemeRepositoryConcern,
    `IconThemeRepository must not own asset, Preview or tree concern ${forbiddenIconThemeRepositoryConcern}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "has no explicit SVG file reference",
  "Icon Theme token reads must fail when the current mapping has no explicit file",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "tokenObject[\"file\"] = file",
  "Icon Theme token reads must not repair a missing file reference",
);
assertContains(
  "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
  "modules",
  "AppModuleRepository must own both App and Module definition tables",
);
for (const [definitionFacadePath, forbiddenDefinitionPersistence] of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "UPDATE apps"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "UPDATE modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "FROM apps"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "FROM modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs", "UPDATE modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs", "UPDATE modules"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "UPDATE apps"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "UPDATE modules"],
] as const) {
  assertDoesNotContain(
    definitionFacadePath,
    forbiddenDefinitionPersistence,
    `${definitionFacadePath} must delegate App/Module definition persistence instead of retaining ${forbiddenDefinitionPersistence}`,
  );
}
for (const forbiddenAppModuleRepositoryConcern of [
  "MainWindow",
  "ProjectTreeNode",
  "RuntimeInputForwardingContract",
  "ModuleInstance",
  "Renderable",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
    forbiddenAppModuleRepositoryConcern,
    `AppModuleRepository must not own UI, Runtime or render concern ${forbiddenAppModuleRepositoryConcern}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/AppModuleRepository.cs",
  "has no explicit default Variant",
  "Module definition persistence must reject metadata without the protected current default Variant id",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/44_app_module_definition_persistence_contract.md",
  "AGENTS must require the App and Module definition persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "44_app_module_definition_persistence_contract.md",
  "the active architecture index must include the App and Module definition persistence contract",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/45_editor_session_view_state_contract.md",
  "AGENTS must require the editor session view state contract",
);
assertContains(
  "docs/architecture/README.md",
  "45_editor_session_view_state_contract.md",
  "the active architecture index must include the editor session view state contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "new ComponentClassRepository(_context)",
  "SpikeDatabase must construct the focused Component Class repository",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "_componentClassRepository.UpdateConfigAndMetadata(",
  "Component Class coordinated document writes must delegate to the repository",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs",
  "_componentClassRepository.UpdateMetadata(",
  "Component Variant document writes must delegate prepared metadata to the repository",
);
for (const componentClassFacadePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassReferences.cs",
] as const) {
  for (const forbiddenComponentClassSql of [
    "FROM component_classes",
    "UPDATE component_classes",
    "INSERT INTO component_classes",
    "DELETE FROM component_classes",
  ]) {
    assertDoesNotContain(
      componentClassFacadePath,
      forbiddenComponentClassSql,
      `${componentClassFacadePath} must delegate Component Class persistence instead of retaining ${forbiddenComponentClassSql}`,
    );
  }
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  'ProjectTreeNodeKind.ComponentClass => "component_classes"',
  "tree node writes must delegate Component Classes to their repository",
);
for (const forbiddenComponentClassRepositoryConcern of [
  "MainWindow",
  "ProjectTreeNode",
  "ComponentClassFieldCatalog",
  "EmbeddedComponent",
  "RuntimeInput",
  "Renderable",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ComponentClassRepository.cs",
    forbiddenComponentClassRepositoryConcern,
    `ComponentClassRepository must not own UI, field, composition, Runtime or render concern ${forbiddenComponentClassRepositoryConcern}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/ComponentClassRepository.cs",
  "has no explicit default Variant",
  "Component Class persistence must reject metadata without an explicit current default Variant id",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/46_component_class_definition_persistence_contract.md",
  "AGENTS must require the Component Class definition persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "46_component_class_definition_persistence_contract.md",
  "the active architecture index must include the Component Class definition persistence contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "new ModuleInstanceRepository(_context)",
  "SpikeDatabase must construct the focused Module Instance repository",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "_moduleInstanceRepository.UpdateDuration(",
  "timeline coordination must delegate prepared Module Instance durations",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs",
  "_moduleInstanceRepository.UpdateVariantDocuments(",
  "Module Variant changes must delegate prepared Module Instance documents",
);
for (const moduleInstanceFacadePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
] as const) {
  for (const forbiddenModuleInstanceSql of [
    "FROM module_instances",
    "UPDATE module_instances",
    "INSERT INTO module_instances",
    "DELETE FROM module_instances",
  ]) {
    assertDoesNotContain(
      moduleInstanceFacadePath,
      forbiddenModuleInstanceSql,
      `${moduleInstanceFacadePath} must delegate Module Instance persistence instead of retaining ${forbiddenModuleInstanceSql}`,
    );
  }
}
for (const forbiddenModuleInstanceRepositoryConcern of [
  "MainWindow",
  "ProjectTreeNode",
  "RuntimeInputForwardingContract",
  "RuntimeTimeline",
  "EffectiveModuleInstanceContract",
  "ValueKind",
  "Renderable",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ModuleInstanceRepository.cs",
    forbiddenModuleInstanceRepositoryConcern,
    `ModuleInstanceRepository must not own UI, Runtime, timing or render concern ${forbiddenModuleInstanceRepositoryConcern}`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/47_module_instance_persistence_contract.md",
  "AGENTS must require the Module Instance persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "47_module_instance_persistence_contract.md",
  "the active architecture index must include the Module Instance persistence contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ProjectEpisodeRepository.cs",
  "_shotRepository.DuplicateForEpisode(",
  "Episode duplication must delegate complete child Shot rows to ShotRepository",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "_shotRepository.UpdateDuration(",
  "Shot duration coordination must delegate its prepared positive duration",
);
for (const shotFacadePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "spikes/desktop-editor-shell/Data/ProjectEpisodeRepository.cs",
] as const) {
  for (const forbiddenShotSql of [
    "FROM shots",
    "JOIN shots",
    "UPDATE shots",
    "INSERT INTO shots",
    "DELETE FROM shots",
  ]) {
    assertDoesNotContain(
      shotFacadePath,
      forbiddenShotSql,
      shotFacadePath + " must delegate Shot persistence instead of retaining " + forbiddenShotSql,
    );
  }
}
for (const forbiddenShotRepositoryConcern of [
  "MainWindow",
  "ProjectTreeNode",
  "ModuleInstance",
  "RuntimeTimeline",
  "RequireShotOwnerChange",
  "Theme",
  "Renderable",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ShotRepository.cs",
    forbiddenShotRepositoryConcern,
    "ShotRepository must not own UI, Production context, timing or render concern "
      + forbiddenShotRepositoryConcern,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/48_shot_persistence_contract.md",
  "AGENTS must require the Shot persistence contract",
);
assertContains(
  "docs/architecture/README.md",
  "48_shot_persistence_contract.md",
  "the active architecture index must include the Shot persistence contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "metadata has no explicit iconSet contract",
  "Icon Theme runtime generation must require explicit current iconSet metadata",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/43_icon_theme_persistence_and_asset_contract.md",
  "AGENTS must require the Icon Theme persistence and asset contract",
);
assertContains(
  "docs/architecture/README.md",
  "43_icon_theme_persistence_and_asset_contract.md",
  "the active architecture index must include the Icon Theme persistence and asset contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
  "has no resolvable Theme context",
  "Module Instance Theme context must fail explicitly when no Theme resolves",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
  "?? \"{}\"",
  "Module Instance Theme context must not return a plausible empty document",
);
for (const inferredThemeContext of ["COALESCE(", "ORDER BY t.name", "JOIN apps a"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
    inferredThemeContext,
    `Module Instance Theme context must not restore inferred fallback ${inferredThemeContext}`,
  );
}
for (const [relativePath, requiredGuard] of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs", "RequireShotContext(connection, shot.Id)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "RequireShotOwnerChange(connection, shotId, value)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Actors.cs", "RequireActorThemeChange(connection, actorId, value)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs", "module instance without explicit Shot owner Theme context"],
] as const) {
  assertContains(
    relativePath,
    requiredGuard,
    `${relativePath} must preserve explicit Shot owner Theme context`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "ORDER BY t.name, t.id",
  "duration synchronization must not infer a Theme from project ordering",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ModuleInstanceThemeContextService.cs",
  "JOIN actors actor ON actor.id = s.owner_actor_id",
  "Module Instance Theme context must resolve through the exact Shot owner Actor",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Schema.cs",
  "owner_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT",
  "Shot owner Actor must remain a required restricted foreign key without an empty default",
);
for (const [relativePath, explicitShotCreationTerm] of [
  ["spikes/desktop-editor-shell/EditorShell/EditorAddChildWorkflow.cs", "new ShotCreationDialog(_owner, _database).Show(parent)"],
  ["spikes/desktop-editor-shell/EditorShell/ShotCreationDialog.cs", "SelectedItem = null"],
  ["spikes/desktop-editor-shell/EditorShell/ShotCreationDialog.cs", "GetRequiredActorOptions(project.Id)"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "must be created through AddShot"],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs", "RequireEpisodeActor(connection, episode.Id, actorId)"],
  ["spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs", "GetRequiredActorOptions(settings.ProjectId)"],
] as const) {
  assertContains(
    relativePath,
    explicitShotCreationTerm,
    `${relativePath} must retain explicit non-empty Shot Actor selection`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/40_theme_persistence_and_context_contract.md",
  "AGENTS must require the Theme persistence and context contract",
);
assertContains(
  "docs/architecture/README.md",
  "40_theme_persistence_and_context_contract.md",
  "the architecture index must include contract 40",
);
assertContains(
  "AGENTS.md",
  "docs/architecture/41_explicit_shot_production_context_contract.md",
  "AGENTS must require the explicit Shot Production context contract",
);
assertContains(
  "docs/architecture/README.md",
  "41_explicit_shot_production_context_contract.md",
  "the architecture index must include contract 41",
);
for (const retiredUsageInference of [
  "LIKE $needle",
  " LIKE ",
  "sqlite_master",
  "PRAGMA table_info",
  "ReferenceSearchTables",
  "TextColumns(",
  "LabelColumn(",
  "JsonContainsString",
  "ReferenceKindForSource",
  "IsProductionUsageSource",
  "AddUsageIfContains",
  "ReferenceSearchValue",
]) {
  for (const usagePath of [
    "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsage.cs",
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsageDetails.cs",
  ]) {
    assertDoesNotContain(
      usagePath,
      retiredUsageInference,
      `Usage must not restore inferred reference behavior ${retiredUsageInference}`,
    );
  }
}
for (const explicitUsageContract of [
  "RecordReferenceKinds",
  "ModuleComponentReferencePaths",
  "ThemeColorTokenCatalog.ColorTokens",
  "ComponentClassFieldCatalog.All()",
  "ReferenceUsageScope.Production",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
    explicitUsageContract,
    `Usage must retain explicit typed contract ${explicitUsageContract}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "_referenceUsageService.BuildIndex(connection)",
  "tree Used state must consume the shared explicit Usage edge set",
);
for (const typedUsageDetail of ["ReferenceUsageScope Scope", "usage.Scope,"]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ReferenceUsageDetails.cs",
    typedUsageDetail,
    `Usage details must retain typed edge scope ${typedUsageDetail}`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  "MainWindow",
  "the Usage service must not import or construct the desktop shell",
);
for (const contextualUsageNavigation of [
  "ReferenceUsageScope.Design => EditorWorkspace.Design",
  "ReferenceUsageScope.Production => EditorWorkspace.Production",
  "usage.SourceNodeId",
  "usage.EmbeddedUsage",
]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/EditorReferenceUsageNavigator.cs",
    contextualUsageNavigation,
    `Usage navigation must retain typed context ${contextualUsageNavigation}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "SelectReferenceNodeInWorkspace",
  "the shell must provide generic workspace-aware Usage selection",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "_navigationRenderer.BringNodeIntoView",
  "Usage navigation must reveal its selected tree node",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeCommandController.cs",
  "new EditorReferenceUsageDialog(_owner, _isDark()).Show(node, usages)",
  "blocked deletion must preserve typed Usage links",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ReferenceUsageCollectionEditor.cs",
  "EditorReferenceUsageLink.Create",
  "the Usage card must use the shared contextual link surface",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeCommandController.cs",
  "GetReferenceUsages(node)",
  "blocked deletion must not flatten typed Usage edges into prose",
);
for (const productionDataTreeContract of [
  "productionDataRoot.AddChild(actorsRoot);",
  "productionDataRoot.AddChild(devicesRoot);",
  "productionDataRoot.AddChild(productionFontsRoot);",
  "productionDataRoot.AddChild(renderPresetsRoot);",
  "systemDataRoot.AddChild(themesRoot);",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
    productionDataTreeContract,
    `Production Data tree must retain ${productionDataTreeContract}`,
  );
}
for (const productionWorkspaceKind of [
  "ProjectTreeNodeKind.ProductionDataRoot",
  "or ProjectTreeNodeKind.DevicesRoot or ProjectTreeNodeKind.Device",
  "or ProjectTreeNodeKind.ProductionFontsRoot or ProjectTreeNodeKind.ProductionFont",
  "or ProjectTreeNodeKind.RenderPresetsRoot or ProjectTreeNodeKind.RenderPreset",
]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/EditorNavigationMetadata.cs",
    productionWorkspaceKind,
    `Production workspace metadata must retain ${productionWorkspaceKind}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  'ProjectTreeNodeKind.Actor, "Actor", reader.GetString(1), ReferenceUsageScope.Production',
  "Actor-owned Usage must navigate to the Production workspace",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNavigationRenderer.cs",
  "node.CanAddChild && node.Parent?.Kind == ProjectTreeNodeKind.ProductionDataRoot",
  "grouped Production Data sections must retain their explicit Add actions",
);
for (const resourceNavigationContract of [
  "Actors, Devices, Production Fonts and Render Presets",
  "copy current records | regenerate from current seeds | create empty",
  "never fall back to records from another Project",
]) {
  assertContains(
    "docs/architecture/39_design_production_resource_navigation_contract.md",
    resourceNavigationContract,
    `resource navigation contract must retain ${resourceNavigationContract}`,
  );
}
assertContains(
  "AGENTS.md",
  "docs/architecture/39_design_production_resource_navigation_contract.md",
  "AGENTS must require the current Design/Production resource navigation contract",
);
assertContains(
  "docs/architecture/README.md",
  "39_design_production_resource_navigation_contract.md",
  "the architecture index must include contract 39",
);
assertContains(
  "spikes/desktop-editor-shell/Data/CurrentDatabaseMaintenance.cs",
  "File.Copy(sourcePath, outputPath, overwrite: false)",
  "current database provisioning must preserve an already-validated source byte-for-byte",
);
for (const retiredPersistenceCommand of [
  "--migrate-database",
  "--create-schema-v1",
  "--validate-schema-v1",
]) {
  assertDoesNotContain(
    "package.json",
    retiredPersistenceCommand,
    `retired persistence command ${retiredPersistenceCommand} must not return`,
  );
}
const desktopDatabasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
type CurrentComponentClassRow = {
  id: string;
  component_type: string;
  record_class_id: string;
  config_json: string;
  design_preview_json: string;
  metadata_json: string;
};
type CurrentModuleClassRow = {
  id: string;
  record_class_id: string;
};
let currentComponentClassRows: CurrentComponentClassRow[] = [];
let currentModuleClassRows: CurrentModuleClassRow[] = [];
let editorLayoutSource = "";
let componentContractSource = "";
let moduleContractSource = "";
const editorLayoutRecordClassIds = new Set<string>();
const editorLayoutsByRecordClass = new Map<string, string>();
if (!existsSync(desktopDatabasePath)) {
  addViolation("data/desktop-editor-spike.sqlite", "committed current database is missing");
} else {
  const database = new Database(desktopDatabasePath, { readonly: true, fileMustExist: true });
  try {
    const parityShots = database
      .prepare("SELECT id, episode_id FROM shots ORDER BY id")
      .all() as { id: string; episode_id: string }[];
    if (parityShots.length !== 1
      || parityShots[0]?.id !== "shot_001"
      || parityShots[0]?.episode_id !== "episode_001") {
      addViolation(
        "data/desktop-editor-spike.sqlite",
        "canonical parity data must retain only episode_001 / shot_001",
      );
    }
    const parityModuleInstances = database
      .prepare("SELECT id, shot_id FROM module_instances ORDER BY id")
      .all() as { id: string; shot_id: string }[];
    if (parityModuleInstances.length !== 2
      || parityModuleInstances.some((row) => row.shot_id !== "shot_001")) {
      addViolation(
        "data/desktop-editor-spike.sqlite",
        "canonical parity Module Instances must be the two authored Screens owned by shot_001",
      );
    }
    currentComponentClassRows = database
      .prepare("SELECT id, component_type, record_class_id, config_json, design_preview_json, metadata_json FROM component_classes")
      .all() as CurrentComponentClassRow[];
    componentContractSource = currentComponentClassRows
      .flatMap((row) => [row.config_json, row.design_preview_json, row.metadata_json])
      .join("\n");
    currentModuleClassRows = database
      .prepare("SELECT id, record_class_id FROM modules")
      .all() as CurrentModuleClassRow[];
    const layouts = database
      .prepare("SELECT record_class_id, layout_json FROM editor_layouts ORDER BY record_class_id")
      .all() as { record_class_id: string; layout_json: string }[];
    for (const layout of layouts) {
      editorLayoutRecordClassIds.add(layout.record_class_id);
      editorLayoutsByRecordClass.set(layout.record_class_id, layout.layout_json);
    }
    editorLayoutSource = layouts.map((layout) => layout.layout_json).join("\n");
    moduleContractSource = (database
      .prepare("SELECT config_json, design_preview_json, metadata_json FROM modules ORDER BY id")
      .all() as { config_json: string; design_preview_json: string; metadata_json: string }[])
      .flatMap((row) => [row.config_json, row.design_preview_json, row.metadata_json])
      .join("\n");
  } finally {
    database.close();
  }
}
const currentComponentClasses = new Set(
  currentComponentClassRows.map((row) => row.component_type),
);
for (const row of currentComponentClassRows) {
  const componentClass = row.component_type;
  if (!desktopPreviewComponents[componentClass]) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current component class "${componentClass}" is missing from desktop preview manifest`,
    );
  }
  if (!routedComponentClasses.has(componentClass)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current component class "${componentClass}" is missing from desktop preview registry`,
    );
  }
  if (!editorLayoutRecordClassIds.has(row.record_class_id)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current component class "${componentClass}" is missing editor layout "${row.record_class_id}"`,
    );
  }

  if (componentClass === "bubble") {
    const metadata = jsonRecord(jsonParse(row.metadata_json));
    const configs = [jsonParse(row.config_json), ...jsonArray(metadata.variants).map((variant) => jsonRecord(variant).config)];
    configs.forEach((config, index) => {
      const gapToken = jsonRecord(jsonRecord(jsonRecord(config).bubble).status).gapToken;
      if (typeof gapToken !== "string" || !gapToken.startsWith("theme.spacing.")) {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `Bubble current config ${index} must declare status.gapToken as a spacing token`,
        );
      }
    });
  }
}
for (const componentClass of Object.keys(desktopPreviewComponents)) {
  if (!currentComponentClasses.has(componentClass)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `manifest component class "${componentClass}" is absent from the committed current database`,
    );
  }
}
const currentModuleClasses = new Set(currentModuleClassRows.map((row) => row.record_class_id));
for (const row of currentModuleClassRows) {
  if (!desktopPreviewModules[row.record_class_id]) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current module "${row.id}" uses class "${row.record_class_id}" missing from the module manifest`,
    );
  }
  if (!routedModuleClasses.has(row.record_class_id)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `current module class "${row.record_class_id}" is missing from module registry`,
    );
  }
}
for (const moduleClass of Object.keys(desktopPreviewModules)) {
  if (!currentModuleClasses.has(moduleClass)) {
    addViolation(
      "src/desktop-preview/desktopPreviewManifest.json",
      `manifest module class "${moduleClass}" is absent from the committed current database`,
    );
  }
}

assertNoTerms("spikes/desktop-editor-shell/MainWindow.axaml.cs", [
  "Current class values",
  "ProjectTreeNodeKind.",
  "ValueKind.",
  "new TextBox",
  "new ComboBox",
  "new CheckBox",
  "IconTheme",
  "IconToken",
  "SvgReplace",
  "ColorPicker",
  "ActorAvatar",
  "actor.avatar",
]);
{
  const mainWindowSource = readText("spikes/desktop-editor-shell/MainWindow.axaml.cs");
  const directDatabaseCalls = [...mainWindowSource.matchAll(/_database\.([A-Za-z0-9_]+)/g)]
    .map((match) => match[1])
    .filter((call): call is string => typeof call === "string");
  const forbiddenDirectDatabaseCalls = directDatabaseCalls.filter((call) => call !== "LoadProjectTree");
  if (forbiddenDirectDatabaseCalls.length > 0) {
    addViolation(
      "spikes/desktop-editor-shell/MainWindow.axaml.cs",
      `MainWindow must remain shell-only; direct database calls are limited to LoadProjectTree, found ${[...new Set(forbiddenDirectDatabaseCalls)].join(", ")}`,
    );
  }
}
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "ProjectTreeNodeKind.ComponentClassesRoot",
  "component class root must not expose Add; parent component classes are internal",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "ProjectTreeNodeKind.StatusBarsRoot",
  "legacy status bar root must not expose Add; system bars are Component Variants",
);
assertPropertyBlockContainsKind(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "AppsRoot",
  false,
  "Apps root must not expose Add; App definitions are development-owned",
);
for (const propertyName of ["CanDuplicate", "CanDelete"]) {
  for (const kind of ["App", "Module"]) {
    assertPropertyBlockContainsKind(
      "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
      propertyName,
      kind,
      false,
      `${kind} definitions must not expose ${propertyName.replace("Can", "")}`,
    );
  }
}
for (const kind of ["App", "Module", "ModuleVariant"]) {
  assertPropertyBlockContainsKind(
    "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
    "CanRenameDirectly",
    kind,
    true,
    `${kind} must expose Rename without changing its stable id`,
  );
}
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanAddChild",
  "ProjectTreeNodeKind.NavigationBarsRoot",
  "legacy navigation bar root must not expose Add; system bars are Component Variants",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDuplicate",
  "ProjectTreeNodeKind.ComponentClass",
  "parent component classes must not expose Duplicate; use Variants instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDuplicate",
  "ProjectTreeNodeKind.StatusBar",
  "legacy status bars must not expose Duplicate; use Component Variants instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDuplicate",
  "ProjectTreeNodeKind.NavigationBar",
  "legacy navigation bars must not expose Duplicate; use Component Variants instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDelete",
  "ProjectTreeNodeKind.ComponentClass",
  "parent component classes must not expose Delete; they are internal",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDelete",
  "ProjectTreeNodeKind.StatusBar",
  "legacy status bars must not expose Delete; use Component Variants instead",
);
assertPropertyBlockDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanDelete",
  "ProjectTreeNodeKind.NavigationBar",
  "legacy navigation bars must not expose Delete; use Component Variants instead",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "var statusBars = QueryStatusBarRows(connection);",
  "project tree must not load legacy status bars as navigation nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "var navigationBars = QueryNavigationBarRows(connection);",
  "project tree must not load legacy navigation bars as navigation nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "foreach (var statusBar in statusBars.OrderBy",
  "project tree must not add legacy status bar nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "foreach (var navigationBar in navigationBars.OrderBy",
  "project tree must not add legacy navigation bar nodes",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "if (parent.Kind == ProjectTreeNodeKind.StatusBarsRoot)",
  "legacy status bar add workflow must not remain; use Component Variants instead",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "if (parent.Kind == ProjectTreeNodeKind.NavigationBarsRoot)",
  "legacy navigation bar add workflow must not remain; use Component Variants instead",
);
for (const forbiddenLegacyTreeTerm of [
  "StatusBarsRoot",
  "NavigationBarsRoot",
  "ProjectTreeNodeKind.StatusBar",
  "ProjectTreeNodeKind.NavigationBar",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
    forbiddenLegacyTreeTerm,
    `legacy system bar tree term ${forbiddenLegacyTreeTerm} must not return; use Component Variants`,
  );
}
for (const forbiddenLegacyLayoutTerm of [
  "recordClassId == \"status_bar\"",
  "recordClassId == \"navigation_bar\"",
]) {
  if (editorLayoutSource.includes(forbiddenLegacyLayoutTerm)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `legacy system bar layout term ${forbiddenLegacyLayoutTerm} must not return; use component layouts`,
    );
  }
}
for (const forbiddenComponentInputControl of [
  "EditorInstantComboBox",
  "new ComboBox",
  "new TextBox",
  "new CheckBox",
  "new ToggleSwitch",
  "new NumericUpDown",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
    forbiddenComponentInputControl,
    `component inputs must use dictionary controls, not local ${forbiddenComponentInputControl}`,
  );
}
for (const recordReferenceSpecializationPath of [
  "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs",
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
]) {
  for (const forbiddenRecordReferenceSpecialization of [
    "ActorReference",
    "ComponentInputKind.Actor",
    "ValueKind.Actor",
  ]) {
    assertDoesNotContain(
      recordReferenceSpecializationPath,
      forbiddenRecordReferenceSpecialization,
      "record references must stay generic through recordReference + tableId",
    );
  }
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "ProjectTreeNodeKind.StatusBar => fieldId.StartsWith(\"statusBar.\"",
  "legacy status bars must not be exposed through record-class field editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "ProjectTreeNodeKind.NavigationBar => fieldId.StartsWith(\"navigationBar.\"",
  "legacy navigation bars must not be exposed through record-class field editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "UpdateStatusBarField",
  "legacy status bar field writes must not remain in generic record editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldValueService.cs",
  "UpdateNavigationBarField",
  "legacy navigation bar field writes must not remain in generic record editing",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "SeedStatusBarsIfEmpty",
  "desktop database initialization must not seed legacy status_bars rows; use status_bar Component Variants",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.cs",
  "SeedNavigationBarsIfEmpty",
  "desktop database initialization must not seed legacy navigation_bars rows; use navigation_bar Component Variants",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Schema.cs",
  "CREATE TABLE IF NOT EXISTS status_bars",
  "desktop schema must not recreate legacy status_bars; use status_bar component variants",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Schema.cs",
  "CREATE TABLE IF NOT EXISTS navigation_bars",
  "desktop schema must not recreate legacy navigation_bars; use navigation_bar component variants",
);
for (const forbiddenSchemaMigrationTerm of [
  "AddColumnIfMissing",
  "EnsureShotColumns",
  "EnsureAppColumns",
  "EnsureModuleColumns",
  "EnsureModuleInstanceColumns",
  "EnsureComponentClassColumns",
  "MigrateScreenInstancesToModuleInstances",
]) {
  assertFilesDoNotContain(
    walkFilesByExtension(path.join(root, "spikes/desktop-editor-shell/Data"), [".cs"]),
    forbiddenSchemaMigrationTerm,
    `schema v1 startup must not keep historical schema migration helper ${forbiddenSchemaMigrationTerm}`,
  );
}
assertDesktopDatabaseTableIsEmpty(
  "status_bars",
  "desktop database must not contain legacy status_bars rows; use status_bar component variants",
);
assertDesktopDatabaseTableIsEmpty(
  "navigation_bars",
  "desktop database must not contain legacy navigation_bars rows; use navigation_bar component variants",
);
for (const legacyTextBoxComponentInput of [
  "\"leftIconRowPresetId\"",
  "\"rightIconRowPresetId\"",
]) {
  assertSourceDoesNotContain(
    "data/desktop-editor-spike.sqlite",
    componentContractSource,
    legacyTextBoxComponentInput,
    "current text box embedded component inputs must use Variant slots, not legacy Variant id fields",
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Common/DeviceMetricRules.cs",
  "JsonPath.NumberAt(metrics,",
  "device preview metric reads must be strict; defaults belong in seed/import normalization, not preview rendering",
);
for (const forbiddenLegacySystemBarMethod of [
  "GetStatusBarSettings",
  "GetStatusBarFieldValue",
  "GetStatusBarItems",
  "UpdateStatusBarField",
  "UpdateStatusBarItem",
  "QueryStatusBarRows",
  "GetNavigationBarSettings",
  "GetNavigationBarFieldValue",
  "GetNavigationBarItems",
  "UpdateNavigationBarField",
  "UpdateNavigationBarItem",
  "QueryNavigationBarRows",
  "GetStatusBarComponentItems",
  "UpdateStatusBarComponentItem",
  "GetNavigationBarComponentItems",
  "UpdateNavigationBarComponentItem",
]) {
  assertFilesDoNotContain(
    currentRepositoryFiles,
    forbiddenLegacySystemBarMethod,
    `legacy system bar database method ${forbiddenLegacySystemBarMethod} must not return; use Component Class Variants`,
  );
}
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "./systemBar",
  "system bars must be declared as explicit status/navigation component modules, not shared manifest entrypoints",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "./systemBar",
  "component registry must route status/navigation through their explicit component modules",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"audio\": {",
  "desktop preview component manifest must use the current audio component type",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"textBox\": {",
  "desktop preview component manifest must route text box as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"textInputBar\": {",
  "desktop preview component manifest must route text input bar as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"keyboard\": {",
  "desktop preview component manifest must route keyboard as an owning component module",
);
assertContains(
  "src/desktop-preview/desktopPreviewManifest.json",
  "\"media\": {",
  "desktop preview component manifest must route media as an owning component module",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "audio: (payload)",
  "component renderable registry must route the current audio component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "textBox: (payload)",
  "component renderable registry must route the current text box component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "textInputBar: (payload)",
  "component renderable registry must route the current text input bar component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "keyboard: (payload)",
  "component renderable registry must route the current keyboard component type",
);
assertContains(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "media: (payload)",
  "component renderable registry must route the current media component type",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "audio_message",
  "legacy audio_message component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "button_icon",
  "legacy button_icon component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "text_input_bar",
  "legacy text_input_bar component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/desktopPreviewManifest.json",
  "video_message",
  "legacy video_message component type must not return to the preview manifest",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "audio_message",
  "legacy audio_message component type must not return to the preview registry",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "button_icon",
  "legacy button_icon component type must not return to the preview registry",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "text_input_bar",
  "legacy text_input_bar component type must not return to the preview registry",
);
assertDoesNotContain(
  "src/desktop-preview/componentClassRenderableRegistry.ts",
  "video_message",
  "legacy video_message component type must not return to the preview registry",
);
for (const legacyComponentTypeFile of [
  "archive/react-legacy/src/domain/schemas/componentClass.ts",
  "archive/react-legacy/src/domain/fields/componentClassFields.ts",
  "archive/react-legacy/src/domain/repository/fixtures/exampleDataset.ts",
  "archive/react-legacy/src/domain/resolvers/resolveChatScreen.ts",
]) {
  for (const legacyComponentType of [
    "audio_message",
    "button_icon",
    "text_input_bar",
    "video_message",
  ]) {
    assertDoesNotContain(
      legacyComponentTypeFile,
      legacyComponentType,
      `legacy component type ${legacyComponentType} must not return to ${legacyComponentTypeFile}`,
    );
  }
}
for (const legacySeededComponentType of [
  "audio_message",
  "button_icon",
  "text_input_bar",
  "video_message",
]) {
  assertDoesNotContain(
    "archive/react-legacy/src/persistence/sqlite/createDatabase.ts",
    `componentType: "${legacySeededComponentType}"`,
    `legacy component type ${legacySeededComponentType} must not be seeded as componentType`,
  );
  assertDoesNotContain(
    "archive/react-legacy/src/persistence/sqlite/createDatabase.ts",
    `type: "${legacySeededComponentType}"`,
    `legacy component type ${legacySeededComponentType} must not be seeded as component_type`,
  );
}
for (const componentType of Object.keys(desktopPreviewComponents)) {
  assertContains(
    "archive/react-legacy/src/domain/schemas/componentClass.ts",
    `"${componentType}"`,
    `component class schema must include manifest component type ${componentType}`,
  );
  assertContains(
    "archive/react-legacy/src/domain/fields/componentClassFields.ts",
    `"${componentType}"`,
    `component class field options must include manifest component type ${componentType}`,
  );
}
for (const legacyComponentRecordClassId of [
  "component.button_icon",
  "component.text_input_bar",
]) {
  const filePath = "spikes/desktop-editor-shell/EditorShell/EmbeddedComponentSlotCatalog.cs";
  assertDoesNotContain(
    filePath,
    legacyComponentRecordClassId,
    `legacy component record class id ${legacyComponentRecordClassId} must not return to ${filePath}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs",
  "ComponentVariant",
  "embedded Component Variant selection must have a dedicated dictionary value kind",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  "ValueKind.ComponentVariant",
  "Component Variant fields must use their dedicated dictionary control",
);
{
  const fieldDefinitionSource = readText("spikes/desktop-editor-shell/EditorShell/FieldDefinition.cs");
  const valueKindBlock = /internal enum ValueKind\s*\{([\s\S]*?)\}/.exec(fieldDefinitionSource);
  const valueKinds = new Set(
    (valueKindBlock?.[1] ?? "")
      .split(",")
      .map((value) => value.trim())
      .filter(Boolean),
  );
  const registrySource = readText("spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs");
  const registeredKinds = new Set(
    [...registrySource.matchAll(/\[ValueKind\.([A-Za-z0-9_]+)\]\s*=/g)]
      .map((match) => match[1] ?? "")
      .filter(Boolean),
  );
  for (const valueKind of valueKinds) {
    if (!registeredKinds.has(valueKind)) {
      addViolation(
        "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
        `dictionary registry is missing explicit ValueKind.${valueKind}`,
      );
    }
  }
  for (const registeredKind of registeredKinds) {
    if (!valueKinds.has(registeredKind)) {
      addViolation(
        "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
        `dictionary registry contains unknown ValueKind.${registeredKind}`,
      );
    }
  }
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  ": new DictionaryTextControl(definition, value)",
  "dictionary registry must fail for an unregistered ValueKind instead of falling back to text",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  "uses unregistered dictionary value kind",
  "dictionary registry must report an unregistered ValueKind visibly",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
  "public static ValueKind RequireCompatible(string kind, string valueKind, string owner)",
  "runtime input kind and valueKind must share one exact semantic owner",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
  "public static JsonNode CreateDefaultValue(JsonObject definition, string owner)",
  "runtime input defaults must be parsed by the same exact ValueKind owner",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
  "public static JsonNode ParseValue(ValueKind valueKind, string value, string owner)",
  "runtime editor values must serialize through the exact ValueKind owner",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
  "public static void ValidateRuntimeValue(JsonObject definition, JsonNode? value, string owner)",
  "persisted Runtime values must validate through the exact definition owner",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
  "public static void ValidateValue(ValueKind valueKind, JsonNode value, string owner)",
  "current dictionary nodes must validate through the exact ValueKind owner",
);
for (const runtimeInputKindConsumer of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
]) {
  assertContains(
    runtimeInputKindConsumer,
    "RuntimeInputValueKindContract.RequireCompatible(",
    `${runtimeInputKindConsumer} must consume the exact Runtime Input kind/valueKind owner`,
  );
}
for (const runtimeDefaultConsumer of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleVariants.cs",
]) {
  assertContains(
    runtimeDefaultConsumer,
    "RuntimeInputValueKindContract.CreateDefaultValue(",
    `${runtimeDefaultConsumer} must consume the exact Runtime Input default owner`,
  );
}
for (const runtimeCollectionConsumer of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
]) {
  assertContains(
    runtimeCollectionConsumer,
    "RuntimeCollectionDocumentContract.",
    `${runtimeCollectionConsumer} must consume the stable Runtime collection document owner`,
  );
}
for (const runtimeValueConsumer of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewTestValues.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryComponentInputBindingsControl.cs",
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
]) {
  assertContains(
    runtimeValueConsumer,
    "RuntimeInputValueKindContract.",
    `${runtimeValueConsumer} must consume the exact Runtime value owner`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleRuntimeDocuments.cs",
  "ValidateCurrentRuntimeValues(",
  "startup and Runtime writes must validate current scalar and collection-field values",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewTestValues.cs",
  "ComponentInputKind.Number when double.TryParse",
  "Design Test Values must not retain a second permissive Runtime value serializer",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "bool.TryParse(value, out var boolean) && boolean",
  "keyframe authoring must not coerce an invalid boolean to false",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
  "RequireDeclaredRuntimeCollection(moduleInstanceId, collectionJsonKey, content)",
  "every persisted collection mutation must require the exact declared collection",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleRuntimeDocuments.cs",
  "ValidateCurrentRuntimeCollections(",
  "startup and Runtime writes must validate declared collection documents",
);
for (const runtimeCollectionWriteFallback of [
  "content[collectionJsonKey] as JsonArray ?? new JsonArray()",
  "currentIndex < 0 ? items.Count",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ModuleInstances.cs",
    runtimeCollectionWriteFallback,
    `Runtime collection writes must not repair or redirect invalid intent (${runtimeCollectionWriteFallback})`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "RuntimeDefaultValue(",
  "runtime reconciliation must not retain a second permissive default parser",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/BehaviorTimingValue.cs",
  "catch",
  "Behavior Timing must not catch invalid current values and return a plausible default",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/BehaviorTimingValue.cs",
  'JsonPath.ParseRequiredObject(json, "Behavior Timing value")',
  "Behavior Timing must require its current object document",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "CurrentRuntimeInputKinds",
  "startup validation must not retain a parallel Runtime Input kind vocabulary",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "ParseValueKind(",
  "Runtime Input presentation must not retain a parallel valueKind parser",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "_ => ComponentInputKind.Text",
  "runtime input kind parsing must not silently fall back to text",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "obj.TryGetPropertyValue(RuntimeInputForwardingContract.StorageKey, out var forwardedNode)",
  "startup must validate every present forwarding envelope instead of ignoring a wrong root",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputForwardingContract.cs",
  "private static JsonObject? ForwardedDefinitions(JsonObject owner)",
  "desktop payload preparation must own the strict forwarding envelope",
);
for (const forwardingFallback of [
  "preview.DeepClone() as JsonObject ?? new JsonObject()",
  "state[sourceRuntimeContractJsonKey] as JsonObject ?? new JsonObject()",
  "definition.DeepClone() as JsonObject ?? new JsonObject()",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputForwardingContract.cs",
    forwardingFallback,
    `desktop forwarding must not manufacture a plausible document (${forwardingFallback})`,
  );
}
assertContains(
  "src/desktop-preview/runtimeInputForwarding.ts",
  "forwarding !== undefined && !isRecord(forwarding)",
  "web payload forwarding must reject a present non-object envelope",
);
for (const compoundDictionaryControl of [
  "spikes/desktop-editor-shell/EditorShell/DictionaryComponentInputBindingsControl.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryStructuredCollectionControl.cs",
  "spikes/desktop-editor-shell/EditorShell/IconSlotsControl.cs",
]) {
  assertContains(
    compoundDictionaryControl,
    "RuntimeInputValueKindContract.ParseValue(",
    `${compoundDictionaryControl} must consume the shared compound ValueKind parser`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
  "RuntimeCollectionDocumentContract.Validate(items, owner)",
  "structured dictionary arrays must preserve stable item ids through the shared owner",
);
for (const retiredCompoundFallback of [
  [
    "spikes/desktop-editor-shell/EditorShell/DictionaryComponentInputBindingsControl.cs",
    'JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value)',
  ],
  [
    "spikes/desktop-editor-shell/EditorShell/DictionaryStructuredCollectionControl.cs",
    "JsonNode.Parse(value) as JsonObject ?? new JsonObject()",
  ],
  [
    "spikes/desktop-editor-shell/EditorShell/IconSlotsControl.cs",
    'JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value)',
  ],
] as const) {
  assertDoesNotContain(
    retiredCompoundFallback[0],
    retiredCompoundFallback[1],
    `${retiredCompoundFallback[0]} must not reconstruct an invalid compound document`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewTestValues.cs",
  "private static JsonObject? TestValues(JsonObject preview)",
  "Design Test Values must own their optional strict transient envelope",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewTestValues.cs",
  "RuntimeCollectionDocumentContract.Validate(",
  "Design Test Value collections must reuse the stable collection document owner",
);
for (const transientTestValueFallback of [
  'preview["testValues"] as JsonObject ?? new JsonObject()',
  "testValues[collection.JsonKey] as JsonArray ?? new JsonArray()",
  "itemIndex.ToString(CultureInfo.InvariantCulture)",
  "item.DeepClone() as JsonObject ?? new JsonObject()",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/DesignPreviewTestValues.cs",
    transientTestValueFallback,
    `Design Test Values must not repair or position-bind invalid transient data (${transientTestValueFallback})`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "testValues[collectionJsonKey] as JsonArray ?? new JsonArray()",
  "the Preview input session must reject a present wrong-root transient collection",
);
for (const componentValueOwnerCall of [
  "RuntimeInputValueKindContract.ValidateValue(descriptor.ValueKind, node, owner)",
  "RuntimeInputValueKindContract.ParseValue(",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
    componentValueOwnerCall,
    `Component fields must consume their exact dictionary ValueKind owner (${componentValueOwnerCall})`,
  );
}
for (const retiredComponentFieldFallback of [
  "ValueKind.Boolean => JsonValue.Create(StringToBool(value))",
  "ValueKind.Integer => NumberNode(value)",
  'JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value)',
  'JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value)',
  "node is JsonObject\n                ? node.ToJsonString()\n                : descriptor.DefaultValue",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
    retiredComponentFieldFallback,
    `Component field reads/writes must not reconstruct invalid current data (${retiredComponentFieldFallback})`,
  );
}
for (const strictEmbeddedDocumentMessage of [
  "must be an object.",
  "overrides must be an object.",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
    strictEmbeddedDocumentMessage,
    `embedded Component documents must reject present wrong roots (${strictEmbeddedDocumentMessage})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Common/JsonPath.cs",
  'return ParseRequiredNumberNode(value, "Numeric value");',
  "record numeric writes must use the common required finite-number parser",
);
for (const retiredNumericWriteFallback of [
  "? decimalValue : 0",
  "? integerValue : 0",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Common/JsonPath.cs",
    retiredNumericWriteFallback,
    `numeric document writes must not coerce invalid text to zero (${retiredNumericWriteFallback})`,
  );
}
for (const strictBooleanRepository of [
  "spikes/desktop-editor-shell/Data/PaletteRepository.cs",
  "spikes/desktop-editor-shell/Data/ActorRepository.cs",
]) {
  assertContains(
    strictBooleanRepository,
    "BooleanText.ParseRequired(value, fieldId)",
    `${strictBooleanRepository} must reject invalid persisted boolean text`,
  );
}
for (const retiredBooleanWriteFallback of [
  ["spikes/desktop-editor-shell/Data/PaletteRepository.cs", "BooleanText.Parse(value) ? 1 : 0"],
  ["spikes/desktop-editor-shell/Data/PaletteRepository.cs", 'UpdateMetadata(connection, colorId, "protected", BooleanText.Parse(value))'],
  ["spikes/desktop-editor-shell/Data/PaletteRepository.cs", 'UpdateMetadata(connection, colorId, "hiddenFromPickers", BooleanText.Parse(value))'],
  ["spikes/desktop-editor-shell/Data/ActorRepository.cs", 'JsonValue.Create(BooleanText.Parse(value))'],
] as const) {
  assertDoesNotContain(
    retiredBooleanWriteFallback[0],
    retiredBooleanWriteFallback[1],
    `${retiredBooleanWriteFallback[0]} must not coerce invalid persisted boolean text to false`,
  );
}
for (const requiredResourceScalarHelper of [
  "public static string RequiredStringAt(",
  "public static string RequiredNumberString(",
  "public static string RequiredBooleanString(",
  "public static string RequiredNumberPair(",
  "public static string RequiredStringPair(",
]) {
  assertContains(
    "spikes/desktop-editor-shell/Common/JsonPath.cs",
    requiredResourceScalarHelper,
    `current resource fields must use exact JSON scalar helpers (${requiredResourceScalarHelper})`,
  );
}
for (const strictResourceReader of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Devices.cs", "JsonPath.RequiredNumberString("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Actors.cs", "JsonPath.RequiredBooleanString("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", "JsonPath.RequiredNumberString("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs", "JsonPath.RequiredStringAt("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs", "JsonPath.RequiredNumberString("],
] as const) {
  assertContains(
    strictResourceReader[0],
    strictResourceReader[1],
    `${strictResourceReader[0]} must consume exact current resource scalar shapes`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Devices.cs",
  "OptionalDynamicIslandPair(",
  "Device Dynamic Island absence must remain an explicit optional contract",
);
for (const retiredResourceReadFallback of [
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Infrastructure.cs", "MetricPair("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Devices.cs", "JsonNumberString("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Actors.cs", "JsonBool("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Actors.cs", "JsonNumberString("],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.ProjectContent.cs", 'JsonNumberString(metadata, ["icon", "scale"], "1")'],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs", ': "{}"'],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs", ': "light"'],
  ["spikes/desktop-editor-shell/Data/SpikeDatabase.Themes.cs", ': "normal"'],
  ["spikes/desktop-editor-shell/Common/DeviceMetricRules.cs", "value.TryGetValue<string>(out var text)"],
  ["spikes/desktop-editor-shell/Data/PaletteRepository.cs", "if (value.TryGetValue<string>(out var text))"],
] as const) {
  assertDoesNotContain(
    retiredResourceReadFallback[0],
    retiredResourceReadFallback[1],
    `${retiredResourceReadFallback[0]} must not reconstruct an invalid current resource value (${retiredResourceReadFallback[1]})`,
  );
}
for (const strictPairValueKindCase of [
  "ValueKind.IntegerPair => JsonValue.Create(ParseIntegerPair(value, owner))",
  "ValueKind.ThemeTokenPair or ValueKind.PaletteColorPair =>",
  "ValueKind.PaletteColorAlphaPair => JsonValue.Create(",
  "ParseBoundedDecimal(valueKind, value, owner)",
]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
    strictPairValueKindCase,
    `dictionary pair/range values must use their exact ValueKind owner (${strictPairValueKindCase})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/Common/PaletteAlphaPair.cs",
  "public static PaletteAlphaPair ParseRequired(string value, string context)",
  "Palette color-alpha pairs must own their complete required envelope",
);
for (const retiredPaletteAlphaFallback of [
  'parts.Length == 2 ? SplitPair(parts[1], "1", "1")',
  "return TryParseAlpha(value, out var parsed) ? parsed : 1",
  "SplitPair(string value, string firstFallback, string secondFallback)",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Common/PaletteAlphaPair.cs",
    retiredPaletteAlphaFallback,
    `Palette color-alpha current values must not reconstruct missing members (${retiredPaletteAlphaFallback})`,
  );
}
for (const strictPairControl of [
  "spikes/desktop-editor-shell/EditorShell/DictionaryIntegerPairControl.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryThemeTokenPairControl.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryPalettePairControl.cs",
]) {
  assertContains(
    strictPairControl,
    "DictionaryFieldPairText.ParseRequired(",
    `${strictPairControl} must consume the exact pair ValueKind owner`,
  );
  assertDoesNotContain(
    strictPairControl,
    "DictionaryFieldPairText.Split(",
    `${strictPairControl} must not split an invalid current pair into empty members`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryFieldPairText.cs",
  "PairFieldLabelsContract.Require(",
  "pair controls must require explicit presentation labels",
);
for (const retiredPairLabelInference of [
  'EndsWith(".size"',
  'EndsWith(".position"',
  'EndsWith(".vertical"',
  'EndsWith(".horizontal"',
  'EndsWith(".modes"',
  'StartsWith("theme."',
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/DictionaryFieldPairText.cs",
    retiredPairLabelInference,
    `pair labels must not be inferred from a field id (${retiredPairLabelInference})`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputValueKindContract.cs",
  "public static PairFieldLabels? ReadPairLabels(",
  "Runtime Input pair labels must be validated by the shared current-definition owner",
);
for (const retiredRuntimePairLabelFallback of [
  'JsonString(item, "pairFirstLabel", "W")',
  'JsonString(item, "pairSecondLabel", "H")',
  'JsonString(field, "pairFirstLabel", "W")',
  'JsonString(field, "pairSecondLabel", "H")',
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
    retiredRuntimePairLabelFallback,
    `Runtime Input pair labels must remain explicit (${retiredRuntimePairLabelFallback})`,
  );
}
for (const strictPrimitiveControl of [
  ["spikes/desktop-editor-shell/EditorShell/DictionaryBooleanControl.cs", "BooleanText.ParseRequired("],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryAlphaControl.cs", "PaletteAlphaPair.ParseAlphaRequired("],
  ["spikes/desktop-editor-shell/EditorShell/HueDegreesControl.cs", "NormalizeHueRequired("],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryIconTokenListControl.cs", "RuntimeInputValueKindContract.ParseValue("],
] as const) {
  assertContains(
    strictPrimitiveControl[0],
    strictPrimitiveControl[1],
    `${strictPrimitiveControl[0]} must reject invalid assigned current values`,
  );
}
for (const retiredPrimitiveControlFallback of [
  ["spikes/desktop-editor-shell/EditorShell/DictionaryBooleanControl.cs", "BooleanText.Parse(value)"],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryAlphaControl.cs", "TryParseAlpha(value, out var parsed) ? parsed : 1"],
  ["spikes/desktop-editor-shell/EditorShell/HueDegreesControl.cs", "NumericText.ClampedDouble(value, 0, 0, 360)"],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryIconTokenListControl.cs", 'string.IsNullOrWhiteSpace(value) ? "[]" : value'],
] as const) {
  assertDoesNotContain(
    retiredPrimitiveControlFallback[0],
    retiredPrimitiveControlFallback[1],
    `${retiredPrimitiveControlFallback[0]} must not manufacture a plausible current control value`,
  );
}
for (const strictNumericControl of [
  "spikes/desktop-editor-shell/EditorShell/DictionaryIntegerControl.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryDecimalControl.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryNumberSliderControl.cs",
]) {
  assertContains(
    strictNumericControl,
    "DictionaryNumericValueContract.ParseRequired(",
    `${strictNumericControl} must validate assigned current values and declared ranges`,
  );
}
for (const retiredNumericControlFallback of [
  ["spikes/desktop-editor-shell/EditorShell/DictionaryIntegerControl.cs", "NumericText.Integer(value, fallback)"],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryDecimalControl.cs", "NumericText.Decimal(value, fallback)"],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryNumberSliderControl.cs", "NumericText.Integer(value, 0)"],
  ["spikes/desktop-editor-shell/EditorShell/DictionaryNumberSliderControl.cs", "NumericText.Decimal(value, 0)"],
] as const) {
  assertDoesNotContain(
    retiredNumericControlFallback[0],
    retiredNumericControlFallback[1],
    `${retiredNumericControlFallback[0]} must not coerce invalid assigned numeric text to zero`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryNumberSliderControl.cs",
  "DictionaryNumericValueContract.TryParseDraft(",
  "the numeric slider must keep invalid interactive drafts separate from current state",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
  '"device.metrics.cornerRadius",\n            "Corner radius",\n            ValueKind.Decimal',
  "Device corner radius must preserve its declared fractional design-unit values",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs",
  "DictionaryControlRegistry.Create",
  "dictionary field rows must host controls through the dictionary control registry",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Validation.cs",
  "ComponentPreviewActions.ValidateContract(",
  "current Design Preview actions must be validated read-only at startup",
);
for (const strictPreviewActionOwner of [
  "public static void ValidateContract(JsonObject preview, string owner)",
  'RequiredString(action, "id"',
  'RequiredString(action, "label"',
  'RequiredString(action, "timeUnit"',
  'RequiredString(action, "completionBehavior"',
  "requires one explicit finite duration source",
]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/ComponentPreviewActions.cs",
    strictPreviewActionOwner,
    `Design Preview actions must keep their explicit declarative owner (${strictPreviewActionOwner})`,
  );
}
for (const retiredPreviewActionFallback of [
  "id = playInputId",
  'label = "Play"',
  "BooleanText.Parse(text)",
  'text.Replace(",", ".")',
  "itemAction.DeepClone() as JsonObject ?? new JsonObject()",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/ComponentPreviewActions.cs",
    retiredPreviewActionFallback,
    `Design Preview actions must not reconstruct malformed current metadata (${retiredPreviewActionFallback})`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "source.DeepClone() as JsonObject ?? new JsonObject()",
  "Runtime collection clones must preserve their guaranteed object root",
);
assertNoTerms("spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs", [
  "DictionaryPathBrowseButton",
  "ValueKind.DirectoryPath",
  "ValueKind.ImageFilePath",
]);
assertContains(
  "spikes/desktop-editor-shell/Mockups.DesktopEditorShell.csproj",
  "desktopPreviewManifest.json",
  "desktop app must embed the canonical preview manifest",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "DesktopPreviewManifest.ComponentCategory(componentClass.ComponentType)",
  "component navigation category must come from the canonical preview manifest",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "ComponentClassNavigationGroupFor",
  "component navigation must not infer category from a hard-coded component type switch",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
  "DesktopPreviewManifest.Modules",
  "module class options must come from the canonical preview manifest",
);
for (const retiredGenericModulePath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
]) {
  assertDoesNotContain(
    retiredGenericModulePath,
    "module.generic",
    "undeclared generic module fallback must not return",
  );
}
for (const retiredGenericAppPath of [
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
]) {
  assertDoesNotContain(
    retiredGenericAppPath,
    "app.generic",
    "retired generic App fallback must not return",
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.Tree.cs",
  "if (parent.Kind == ProjectTreeNodeKind.AppsRoot)",
  "repository must not create Apps through generic Add Child",
);
assertNoTerms("spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs", [
  "\"audio\"",
  "\"avatar\"",
  "\"buttonIcon\"",
  "\"textBox\"",
  "\"navigation_bar\"",
  "\"status_bar\"",
  "\"textInputBar\"",
  "\"keyboard\"",
  "\"media\"",
]);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "index < slots.Count - 1 && overrides is not null",
  "embedded inherited values must apply ancestor overrides only, so reset restores the selected child Variant",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassReferences.cs",
  "return GetComponentVariantReferenceOptionsByType(projectId, componentType);",
  "embedded Component Variant selectors must store full Component Variant references, not short Variant ids",
);
assertContains(
  "src/desktop-preview/componentPreviewDefaults.ts",
  "componentVariantConfig",
  "desktop preview resolvers must resolve embedded child Variants through the shared Variant helper",
);
assertContains(
  "src/desktop-preview/audioComponentResolver.ts",
  "componentVariantConfig(componentBaseConfigs, \"badge\", badgeSlot.variantReference)",
  "audio badge preview must resolve the selected Badge Variant",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.IconThemes.cs",
  "ResolveIconTokenAssetPath",
  "icon tokens must never resolve through the first Icon Set in a project",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/IconTokenPickerDialog.cs",
  "GetIconThemeOptions",
  "the generic icon-token picker must use the active Theme Icon Set instead of selecting a concrete set",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/SvgIconPreview.cs",
  "NativeWebView",
  "editor icon thumbnails must use lightweight vector controls instead of one web view per icon",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
  "component.label.textGap\"",
  "Label text separation must use the canonical spacing-token field",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "ValidateComponentVariantReferencesForPreview",
  "the Preview data boundary must validate full embedded Variant references before payload construction",
);
for (const embeddedVariantField of [
  "component.avatar.label.variantReference",
  "component.audio.avatar.variantReference",
]) {
  if (new RegExp(`"id"\\s*:\\s*"${embeddedVariantField.replaceAll(".", "\\.")}"`).test(editorLayoutSource)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `embedded Variant field "${embeddedVariantField}" must not be shown as a separate layout row`,
    );
  }
  assertMatches(
    "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldCatalog.cs",
    new RegExp(`\\["${embeddedVariantField.replaceAll(".", "\\.")}"\\][\\s\\S]*?ValueKind\\.OptionToken`),
    `embedded Variant field "${embeddedVariantField}" must keep the slot Variant route, not generic recordReference`,
  );
}

assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeSelectionState.cs",
  "private readonly Dictionary<string, string> _lastComponentVariantNodeIds",
  "Component Variant navigation must remember the last selected Variant per component class",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "ResolveSelectionNode",
  "component class navigation must resolve to a concrete Variant selection",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "_editorContent.Build(editorNode, node)",
  "component editor layout node and data node must stay separated so Variants edit Variant config",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "variantSourceNode.Kind != ProjectTreeNodeKind.ComponentVariant",
  "Save Variant must only be offered for a concrete selected Component Variant",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNodeSelectionState.cs",
  "VariantReferenceId.HasVariantId(child.Id, VariantEnvelopeContract.DefaultId)",
  "first component class selection must prefer the protected Default Variant",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClassVariants.cs",
  "Component variants can only be saved from an active selected variant.",
  "component variant saving must reject ambiguous parent component class configs",
);
for (const kind of ["ComponentClass", "ComponentVariant"]) {
  assertPropertyBlockContainsKind(
    "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
    "CanRenameDirectly",
    kind,
    true,
    `${kind} must expose direct rename through the standard variant action`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "CanRenameDirectly => Kind == ProjectTreeNodeKind.ComponentClass\n        || (Kind == ProjectTreeNodeKind.ComponentVariant && !IsProtected)",
  "Component Variant rename must not be coupled to delete protection",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNavigationRenderer.cs",
  "EditorIcons.Create(EditorIcons.Edit, 14)",
  "Component Variant rename must use the standard editor rename icon",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ProjectTreeNode.cs",
  "Kind == ProjectTreeNodeKind.ComponentVariant && !IsProtected",
  "protected Component Variants must not be deletable",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "ProjectTreeNodeKind.ComponentVariant => FromComponentSource(dataSource.LoadComponentVariant(node), themeMode, theme)",
  "design preview must route selected Component Variant nodes",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadDataSource.cs",
  "_database.GetComponentVariantSettings(node)",
  "the Preview data boundary must load selected Component Variant config",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentClassFieldValueService.cs",
  "ProjectTreeNodeKind.ComponentClass or ProjectTreeNodeKind.ComponentVariant",
  "component field service must support Component Variants as editable data contexts",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "CreateComponentVariantFieldValue",
  "Component Variant fields must read from Variant config",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "UpdateComponentVariantField",
  "Component Variant fields must write to Variant config",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  "ProjectTreeNodeKind.ComponentVariant, ReadString(reader, 3)",
  "theme status bar references must target concrete Component Variants",
);
assertContains(
  "spikes/desktop-editor-shell/Data/ReferenceUsageService.cs",
  "ProjectTreeNodeKind.ComponentVariant, ReadString(reader, 4)",
  "theme navigation bar references must target concrete Component Variants",
);
assertAnyContains(
  desktopPersistenceDataPaths,
  "GetComponentVariantReferenceOptionsByType(projectId, \"status_bar\"",
  "theme status bar selector must list Component Variants",
);
assertAnyContains(
  desktopPersistenceDataPaths,
  "GetComponentVariantReferenceOptionsByType(projectId, \"navigation_bar\"",
  "theme navigation bar selector must list Component Variants",
);
for (const themeVariantField of ["theme.statusBarId", "theme.navigationBarId"]) {
  assertMatches(
    "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
    new RegExp(`\\[\"${themeVariantField.replaceAll(".", "\\.")}\"\\][\\s\\S]*?ValueKind\\.ComponentVariant`),
    `${themeVariantField} must use the typed Component Variant dictionary control`,
  );
}
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/MotionVariantValue.cs",
  "legacyKey",
  "motion parser must not accept legacy transition keys",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
  "motion.Remove(\"opacity\")",
  "component config normalization must not migrate legacy motion opacity",
);
for (const legacyMediaIconBarSlot of [
  "\"topIconBarSlot\"",
  "\"centerIconBarSlot\"",
  "\"bottomIconBarSlot\"",
]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/Data/SpikeDatabase.ComponentClasses.cs",
    legacyMediaIconBarSlot,
    `media icon bars must use explicit inline/fullscreen slots, not legacy ${legacyMediaIconBarSlot}`,
  );
}

assertContains(
  "src/desktop-preview/DesktopRenderableHtmlAdapter.tsx",
  "data-renderable-id={node.id}",
  "desktop preview nodes must expose stable ids for generic incremental WebView updates",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "window.mockupsRegisterPreviewAsset",
  "animation frames must register repeated data assets once instead of transporting them in every body patch",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "_pendingUpdate = nextUpdate;",
  "animation playback must keep the latest pending frame instead of accumulating obsolete frames",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "MaxQueuedAnimationFrames",
  "animation playback must not restore a bounded backlog of obsolete frames",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "Stopwatch.GetElapsedTime(_playbackStartedTimestamp).TotalSeconds",
  "preview playback time must derive from a monotonic elapsed clock instead of counting processed UI ticks",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "PrewarmPersistentRenderer",
  "preview prewarming must not serialize interactive frames through the same renderer process",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "public static IDisposable ReserveFrameCacheCapacity",
  "expanded playback frame caches must be represented by a releasable reservation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "ReleaseFrameCacheReservation();",
  "playback must release its expanded frame-cache reservation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorCollectionCardFactory.cs",
  "ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ComponentVariant or ProjectTreeNodeKind.ModuleInstance",
  "module instances must use the same declarative runtime-input editor as module Test Values",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "payload.Kind is \"componentClass\" or \"module\" or \"moduleInstance\"",
  "module-instance actions must use the generic preview input session",
);
for (const retiredInstanceEditorTerm of [
  "moduleInstance.conversation.bubbleRevealMode",
  "moduleInstance.conversation.incomingRevealMode",
  "ConversationMessagesCollectionEditor",
]) {
  for (const file of [
    "spikes/desktop-editor-shell/EditorShell/RecordClassFieldCatalog.cs",
    "spikes/desktop-editor-shell/EditorShell/EditorCollectionCardFactory.cs",
  ]) {
    assertDoesNotContain(
      file,
      retiredInstanceEditorTerm,
      `module instances must not restore the retired Conversation-specific editor route (${retiredInstanceEditorTerm})`,
    );
  }
  if (editorLayoutSource.includes(retiredInstanceEditorTerm)) {
    addViolation(
      "data/desktop-editor-spike.sqlite",
      `module instances must not restore the retired Conversation-specific editor route (${retiredInstanceEditorTerm})`,
    );
  }
}
assertDoesNotContain(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "parseObject(payload.instanceJson).behavior",
  "module renderables must consume the canonical runtime preview payload instead of a second instance behavior channel",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  '"definesModuleDuration":true',
  "a module runtime contract must declare which action defines its finite instance duration",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  "DeclaredBaseDuration(contract)",
  "module-instance duration must be evaluated generically from the declared runtime action",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  'candidate["extendsModuleDuration"]?.GetValue<bool>() == true',
  "finite collection-item actions must be able to extend module duration through their declarative contract",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "group.Sum((instance) => instance.DurationFrames)",
  "cut-only Shot duration must remain the sum of its ordered module-instance durations",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "ProjectTreeNodeKind.Shot => FromShot",
  "Shot preview must resolve its active ordered module-instance slot",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "boundedFrame - startFrame",
  "Shot preview must translate the requested Shot frame to the active module's local frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  '["moduleInstanceId"] = moduleInstanceId',
  "production preview payloads must identify their active module instance",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "RenderProductionContextHistoryItems",
  "production preview history must use its own Shot and module-instance stack",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "public void SetWorkspace(EditorWorkspace workspace)",
  "preview ownership must follow the explicit Design or Production workspace",
);
assertContains(
  "spikes/desktop-editor-shell/MainWindow.axaml.cs",
  "_previewController.SetWorkspaceWithoutRefresh(workspace);",
  "workspace transactions must establish their final selection before refreshing Preview",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorContentController.cs",
  "_cardHost.Replace(cards);",
  "editor content must build a candidate before replacing the visible card host",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorNavigationRenderer.cs",
  "var candidate = new StackPanel();",
  "navigation content must build a candidate before replacing the visible host",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "{ Kind: ProjectTreeNodeKind.ModuleInstance } instance => _productionPreviewData.ModuleInstanceShotId(instance.Id)",
  "a selected module instance must retain its owning Shot playhead context",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  'new FieldOption("screen", "Screen")',
  "production navigation must not duplicate tree context with a Shot or Screen scope control",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "NavigationFrameRange()",
  "production slider and playback must share the tree-owned navigation range",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "return ScreenFrameRange(screen.Id);",
  "a selected Screen must present and play its own local range",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  '["localFrame"] = Math.Max(0, localTimelineFrame ?? 0)',
  "production Screen payload identity must expose its resolved local frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  '["moduleInstanceId"]?.GetValue<string>()',
  "runtime Test Values scope must distinguish production Screens by instance id",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  /TimelineButtonGroup\([\s\S]*?_shotAbsoluteStartButton,[\s\S]*?_shotPreviousSlotButton,[\s\S]*?_shotPreviousKeyframeButton,[\s\S]*?_shotPreviousFrameButton,[\s\S]*?_shotPlayButton,[\s\S]*?_shotNextFrameButton,[\s\S]*?_shotNextKeyframeButton,[\s\S]*?_shotNextSlotButton,[\s\S]*?_shotAbsoluteEndButton\)/,
  "production transport must remain symmetric around frame stepping and playback",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  /_shotPreviousKeyframeButton,[\s\S]*?_shotPreviousFrameButton,[\s\S]*?_shotPlayButton,[\s\S]*?_shotNextFrameButton,[\s\S]*?_shotNextKeyframeButton/,
  "production transport must keep frame stepping next to playback and keyframe stepping outside it",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_shotNavigationScope",
  "Production preview context must come from the selected tree node rather than a duplicate scope control",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "navigationRow = new Border",
  "production transport must retain its grouped separator layout",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_shotTimelineControls.DesiredSize.Width",
  "production transport controls must reflow as one measured unit",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "availableWidth < 880",
  "production transport must not use a fixed wrapping breakpoint",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "SynchronizeExplicitScreenSelection",
  "Production scrubbing must move the shared playhead without changing tree selection",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorIcons.cs",
  'TimelineShotStart => "M3 3H6V5H5V19H6V21H3Z',
  "timeline boundary bars must be filled geometry rather than invisible open paths",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "dataSource.ModuleInstanceLocalFrame(node.Id, timelineFrame)",
  "module-instance production preview must translate the global Shot frame to a local frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorTimelineTransport.cs",
  "CreateKeyframeStepIcon(bool next)",
  "Shot keyframe controls must use shared timeline transport chrome",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorTimelineTransport.cs",
  "CreateKeyframeGlyph(",
  "Preview and editor keyframe buttons must share one SVG glyph factory",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "var isOnKeyframe = keyframes.Contains(_shotPreviewFrame)",
  "Preview playback must expose when the playhead is parked on a keyframe",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  /_shotPlayButton\.BorderBrush\s*=\s*isOnKeyframe[\s\S]*?EditorAnimationVisuals\.ActiveTrackBrush/,
  "Preview playback must use the animation amber border at an exact keyframe",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "ModuleInstanceTimeline.ShotKeyframeFrames(_timelineDataSource, shotId)",
  "Shot navigation must aggregate keyframes from every Screen before selecting the current Screen range",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "var showScreenStep = contextNode?.Kind == ProjectTreeNodeKind.Shot",
  "previous and next Screen controls must appear only in Shot context",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "? ScreenFrameRange(contextNode.Id)",
  "Screen context keyframe navigation must stay inside the selected Screen",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "private ProjectTreeNode? ProductionPayloadNode() => ProductionContextNode();",
  "the selected Production tree node must be the sole preview payload context",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_designContextLockButton.IsVisible = _workspace != EditorWorkspace.Production",
  "Production preview must not expose a context lock that can diverge from tree selection",
);
assertMatches(
  "src/desktop-preview/bubbleComponentResolver.ts",
  /function bubbleAudioInputs[\s\S]*?showBadge:\s*false/,
  "Bubble must explicitly bind its embedded Audio badge visibility at the child boundary",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  'Timeline(definition)["extendsOwnerDuration"]?.GetValue<bool>() != false',
  "field metadata must decide whether a field advances serial owner duration",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  /"preDurationFieldIds":\["delay"\][\s\S]*?"postDurationFieldIds":\["postWriteOnHold"\]/,
  "Conversation runtime metadata must declare serial pre-delay and post-hold ownership explicitly",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  '"minimumEnabledKeyframes":2',
  "Conversation must keep base write-on when text has only mandatory KF0",
);
assertContains(
  "src/desktop-preview/conversationModuleResolver.ts",
  'timeline.usesTrackCompletion("text", targetId)',
  "Conversation resolver must suppress built-in write-on when text owns animation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "Stopwatch.GetElapsedTime(_shotPlaybackStartedTimestamp).TotalSeconds",
  "Shot playback must derive frames from a monotonic elapsed clock",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_pendingPlaybackFramesOverride = ShotPlaybackFramePayloads",
  "Shot playback must prepare its frames through the shared HTML/raster playback route",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  '"0,0,0,Auto"',
  "production preview setup must collapse editor-owned Device, Theme and Mode selectors",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "ProductionPreviewContextStrip.Render",
  "production preview setup must expose inherited context through the shared read-only strip",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  'statusText: message.statusVisible ? message.statusText : ""',
  "hidden message delivery state must not reserve status text space",
);
assertContains(
  "src/desktop-preview/bubbleComponentResolver.ts",
  'optionalBoolean(preview, "typingIndicator") || state === "system"',
  "typing-indicator and system text must be centered by the Bubble resolver",
);
assertDoesNotContain(
  "src/desktop-preview/bubbleComponentRenderable.ts",
  "avatarIntrusion",
  "external Bubble avatars must not reserve internal content padding",
);
assertContains(
  "src/desktop-preview/bubbleComponentRenderable.ts",
  "minimumContentWidth",
  "Bubble layout must allow owned labels to define its minimum content width",
);
assertContains(
  "src/desktop-preview/bubbleComponentRenderable.ts",
  "inlineBubbleStatusWidth",
  "Bubble status must reuse the final text line when its resolved frame fits",
);
assertContains(
  "src/desktop-preview/bubbleComponentResolver.ts",
  'requiredString(status, "gapToken"',
  "Bubble status row spacing must use its declared spacing token",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  '"timelineFrameJsonKey":"conversationFrame"',
  "modules with a local timeline must declare its runtime frame key",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  '"jsonKey":"conversationType"',
  "Conversation must publish its individual or group type through the runtime contract",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  /"id":"writeOn"[\s\S]*?"valueKind":"BehaviorTiming"[\s\S]*?"baseFramesPerUnit":7/,
  "new Conversation messages must contribute a finite default write-on duration",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "candidate.EnabledWhenItemJsonKey.Equals(input.JsonKey",
  "runtime collection controls must rebuild after a dependency field changes",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/InstantEditorCard.cs",
  "public EditorSubcardLayout SubcardLayout { get; }",
  "subcard organization must be an explicit generic card property",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "EditorSubcardLayout.VerticalCards",
  "runtime collection cards must declare their subcard organization explicitly",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorGroupBlock.cs",
  "subcardLayout == EditorSubcardLayout.FlatStack",
  "flat-stack cards must inherit their parent surface through the shared card factory",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "CreateSeparatedInputContent(owner, preview, ownInputs)",
  "the General runtime category must use shared separated-section content",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  '"general",\n                "General",\n                "Runtime inputs"',
  "direct runtime fields must join the shared top-level category navigator",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "CreateTestValueCollectionContent(owner, preview, collection, actions, items)",
  "runtime collections must join the same top-level category navigator",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "_entries[section.Id] = entry;",
  "vertical-card rows must own full-width separators up to the navigation divider",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayout.cs",
  '[JsonPropertyName("groupLayout")]',
  "editor cards must declare reusable child-group organization through layout metadata",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayout.cs",
  '[JsonPropertyName("presentation")]',
  "individual editor groups must be able to declare a reusable presentation block",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  "EffectiveGroupLayout(group, groupLayout)",
  "mixed editor-card organization must remain metadata-driven and generic",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  '"verticalCards" => EditorSubcardLayout.VerticalCards',
  "layout cards must route vertical-card organization through the shared subcard host",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  '"separatedSections" => EditorSubcardLayout.SeparatedSections',
  "layout cards must route separated sections through the shared subcard host",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"groupLayout"\s*:\s*"verticalCards"/,
  "component layouts must opt into vertical cards declaratively",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"groupLayout"\s*:\s*"separatedSections"/,
  "component layouts must opt into separated sections declaratively",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutsByRecordClass.get("component.status_bar") ?? "",
  /"groupLayout"\s*:\s*"separatedSections"/,
  "Status Bar must use the same declarative separated-section organization as Atoms",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutsByRecordClass.get("component.navigation_bar") ?? "",
  /"groupLayout"\s*:\s*"separatedSections"/,
  "Navigation Bar must use the same declarative separated-section organization as Atoms",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutsByRecordClass.get("component.keyboard") ?? "",
  /"groupLayout"\s*:\s*"verticalCards"/,
  "Keyboard categories must use the same declarative vertical-card organization as Atoms",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  'EditorSubcardLayout.VerticalCards',
  "runtime input groups must use the shared vertical-card organization",
);
assertSourceDoesNotContain(
  "data/desktop-editor-spike.sqlite",
  componentContractSource,
  '"iconRow::variant::default"',
  "current component input contracts must store concrete Component Variant references",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorTreeExpansionState.cs",
  "CollapseWorkspaceSectionPeers(node)",
  "workspace navigation cards must remain mutually exclusive",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorSessionUiState.cs",
  "public void SetExpanded(string key, bool value)",
  "nested card expansion must remain available within the current editor session",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "cards[0].IsExpanded = true",
  "nested cards must not reopen automatically in a new application session",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorShellStateService.cs",
  "ExpandedCards",
  "card expansion must never be written to persisted window state",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorSessionHistoryState.cs",
  "EditorViewState",
  "Preview and Variant history must never persist editor view state",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorCardHostController.cs",
  "card.IsExpanded = false;",
  "a new editor session must begin with every editor card closed",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorSessionViewStateStore.cs",
  "_statesByRecordClassId[RequiredRecordClassId(recordClassId)] = state;",
  "card expansion and scroll must remain available by exact layout class within the current session",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorViewStateController.cs",
  "EditorNodeSelectionState.EditorNodeForSelection(node).RecordClassId",
  "Variant editor state must resolve through the parent layout record class",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorViewStateController.cs",
  "node.Id",
  "editor view state must not be keyed by the selected node id",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorViewStateController.cs",
  "card.SessionStateId",
  "top-level editor card state must restore through explicit stable ids",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorViewState.cs",
  "bool[]",
  "top-level editor card expansion must not be stored by array position",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorContentController.cs",
  "presentationKey",
  "the retired Simplified/Complete presentation mode must not create session state",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  'EditorNodeSelectionState.EditorNodeForSelection(node).RecordClassId',
  "Runtime Inputs tabs must retain their session selection by exact layout class",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  '"Interpolation",\n                    ValueKind.OptionToken',
  "keyframe interpolation must use the dictionary field route",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "EditorIcons.TimelineFirstFrame",
  "animation transport must reuse the standard timeline navigation icons",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "filled: hasCurrentKeyframe",
  "animation transport must expose exact-keyframe state at the active frame",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "playbackButton.Click += (_, _) => _togglePlayback()",
  "animation play-pause must delegate to the authoritative Preview playback owner",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  'var selectionKey = $"{node.Id}:animation-properties:{scopeKey}"',
  "animation property selection must remain isolated per declared animation scope in session state",
);
assertMatches(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  /Children\s*=\s*\{[\s\S]*?CreateSeparator[\s\S]*?currentKeyframeButton,[\s\S]*?firstFrameButton,[\s\S]*?previousFrameButton,[\s\S]*?playbackButton,[\s\S]*?nextFrameButton,[\s\S]*?lastFrameButton/,
  "animation transport must keep diamond-first standard navigation order",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "DispatcherTimer",
  "animation editor must not create an independent playback clock",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "var resolvedTargets = document.Tracks",
  "animation property lists must originate from active persisted tracks only",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  ": EditorAnimationVisuals.OtherKeyframeBrush",
  "active animated properties that are not selected must remain visible in gray",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "_shotFrame() - screenStartFrame",
  "Screen animation panels must present the authoritative Shot playhead as Screen-local time",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame(",
  "owner-relative keyframes must translate through the common timeline onto the containing Screen",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "ModuleInstanceTimeline.DurationFrames(_timelineDataSource, node.Id)",
  "animation authoring panels must use their containing Screen scale",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceTimeline.cs",
  "RuntimeDurationPolicy.Explicit",
  "explicit Screen duration must be resolved by the shared module-instance timeline",
);
assertContains(
  "spikes/desktop-editor-shell/Data/SpikeDatabase.RuntimeInputContracts.cs",
  "RuntimeDurationContract.Policy(contract) == RuntimeDurationPolicy.Explicit",
  "timeline synchronization must preserve explicitly authored Screen durations",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  '"durationPolicy":"explicit"',
  "Lock Screen must declare its explicit duration policy in its own module contract",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Common/RuntimeDurationContract.cs",
  "lockScreen",
  "the generic duration contract must not know concrete modules",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "screenStartFrame + RuntimeAnimationFrameOrigin.ScreenFrameForOwnerFrame",
  "Screen-local keyframe markers must not persist or display absolute Shot offsets",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "_reloadAndSelect",
  "keyframe edits must refresh their local animation surface without rebuilding the editor",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  "firstMatchingValue",
  "entity-owned keyframes must support a generic stable first-appearance origin",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_playbackState.Changed += RefreshResolvedValue",
  "animated Runtime Values must follow the authoritative Preview playhead",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "control.IsEnabled = false",
  "an animated Runtime Value must be read-only outside its Animation editor",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryFieldControl.cs",
  "public void SetPresentedValue(string value)",
  "dictionary controls must support a resolved display value without committing it as runtime payload",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationValueResolver.cs",
  "destination.Interpolation is \"linear\" or \"easeInOut\"",
  "Runtime Values must resolve numeric keyframes with the generic interpolation contract",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  componentContractSource,
  /"componentType":"componentStack"[\s\S]*?"animationTimeline":\{"sequenceItems":false\}/,
  "current Component Stack slots must share a parallel Screen-time origin",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  /"stackStates"[\s\S]*?"animationPresentation":"collectionFooter"[\s\S]*?"animationTimeline":\{"sequenceItems":false\}/,
  "forwarded Lock Screen slots must preserve the parallel Component Stack timeline",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "ModuleInstanceAnimationValueResolver.ResolveDisplayValue(",
  "Animation keyframe controls and Runtime Values must share one resolved-value presentation path",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "timelineDuration += 10",
  "the provisional authoring horizon must grow in session-only ten-frame steps",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ModuleInstanceAnimationEditor.cs",
  "toggle.IsChecked == true ? naturalDuration : null",
  "Retime off must remove the persisted target-duration override",
);
assertContains(
  "spikes/desktop-editor-shell/Common/RuntimeAnimationFrameOrigin.cs",
  "FieldReferenceDurationFrames",
  "reference-duration lanes must resolve through the common owner timeline",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  '_animationEditor.CreateTargetContent(owner.Node, "")',
  "Screen-owned animation must live inside the General runtime category",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "_animationEditor.CreateTargetContent(owner.Node, itemId)",
  "collection-item animation must live inside its owning runtime item",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorCollectionCardFactory.cs",
  "animationEditor.Create(node)",
  "animation must not return as a detached module-level editor card",
);
assertContains(
  "src/desktop-preview/conversationModuleResolver.ts",
  'new RuntimeOwnerTimeline(preview, preview, animation, themeTokens)',
  "Conversation must resolve generic owner timing in its owning frame resolver",
);
assertContains(
  "src/desktop-preview/runtimeOwnerTimeline.ts",
  "extendsOwnerDuration !== false",
  "the generic preview owner timeline must support late fields that do not advance collection sequencing",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "CreateAnimationActivationGlyph(",
  "Runtime fields must derive the sequencing/non-sequencing activation glyph from animation metadata",
);
assertDoesNotContain(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "resolveParameterAnimation",
  "Conversation renderable must not evaluate parameter tracks",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"subtitle": "Theme color behavior"[\s\S]*?"groupLayout": "verticalCards"/,
  "Theme Colors groups must use the shared vertical-card organization",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"id": "icons"[\s\S]*?"groupLayout": "verticalCards"/,
  "Theme Icons groups must use the shared vertical-card organization",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  '"pairLayout": "sharedHeader"',
  "palette-pair groups must opt into the generic shared Light/Dark header through layout metadata",
);
for (const semanticIcon of [
  "Neutral tint",
  "App colors",
  "Content colors",
  "Action and input colors",
  "Navigation and feedback colors",
  "Border colors",
  "Icon colors",
  "Icon sizes",
  "Font families",
  "Text sizes",
  "Style and line heights",
]) {
  assertSourceContains(
    "data/desktop-editor-spike.sqlite",
    editorLayoutSource,
    `"icon": "navigation-asset:${semanticIcon}"`,
    `Theme group '${semanticIcon}' must use its dedicated reusable system icon`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryPalettePairControl.cs",
  'ColumnDefinitions = new ColumnDefinitions("*,*");',
  "compact palette pairs must preserve two equal columns at narrow widths",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryPalettePairControl.cs",
  "firstParent.Children.Remove(_firstControl);",
  "compact palette pairs must detach existing controls before reusing them in shared-header composition",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  "var controlLabels = control.UseSharedPairHeader();",
  "shared palette-pair presentation must be applied to every control in the group",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  editorLayoutSource,
  /"id": "typography"[\s\S]*?"groupLayout": "verticalCards"[\s\S]*?"id": "fontFamilies"[\s\S]*?"id": "typographySizes"[\s\S]*?"id": "typographyStyle"/,
  "Theme Typography must remain split into semantic vertical cards",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/Common/ThemeNumericTokenCatalog.cs",
  'Token("theme.typography.size",',
  "the retired singular typography size token must not remain in the numeric token catalog",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "ClearTransientValues(scopeKey);",
  "runtime contract changes must invalidate stale session-only input values generically",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "ShouldUse(",
  "subcard organization must not be inferred from count or hierarchy depth",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorLayoutCardFactory.cs",
  '"flatStack" => EditorSubcardLayout.FlatStack',
  "the documented flat-stack organization must remain available through generic layout metadata",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  "_content.Content = null;",
  "internal navigation must detach reusable editor content before selecting another card",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/EditorInternalNavigation.cs",
  '"Messages"',
  "shared internal navigation must not know a concrete runtime collection",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "conversationMessageActorIdentityVisible(conversationType, message.state)",
  "Conversation must expose per-message Actor identity only through its direction-owned composition rule",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  'return conversationType === "group" && direction === "incoming"',
  "only group incoming messages may expose per-message Actor identity",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  "let y = top + gap - scrollOffset",
  "Conversation messages must stack from Header with messageGap before applying overflow scroll",
);
assertContains(
  "src/desktop-preview/conversationModuleRenderable.ts",
  'transition: "slide"',
  "Conversation message overflow must use the shared Theme Slide motion",
);
assertContains(
  "src/desktop-preview/bubbleComponentResolver.ts",
  "actorIdentityVisible",
  "Bubble must consume its parent-owned actor identity visibility override",
);
assertSourceContains(
  "data/desktop-editor-spike.sqlite",
  moduleContractSource,
  '"source":"calculated"',
  "parent-owned timeline frame inputs must be declared calculated",
);
for (const placeholderPlural of ["input(s)", "collection(s)", "instance(s)"]) {
  assertDoesNotContain(
    "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
    placeholderPlural,
    `runtime-input UI must use grammatical counts instead of ${placeholderPlural}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/RuntimeInputsCollectionEditor.cs",
  "Payload key:",
  "runtime API diagnostics must label persisted payload keys explicitly",
);
for (const dictionaryControl of [
  "spikes/desktop-editor-shell/EditorShell/DictionaryEmbeddedComponentControl.cs",
  "spikes/desktop-editor-shell/EditorShell/DictionaryComponentVariantControl.cs",
]) {
  assertDoesNotContain(
    dictionaryControl,
    "#D6A638",
    "dictionary embedded action chrome must use the shared override visuals",
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "$\"Slot: {slot.Label}\"",
  "embedded breadcrumbs must identify the owning slot without duplicating component identity",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "new(\"Component\", component)",
  "embedded context metadata must identify the concrete component",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorHeaderController.cs",
  "new EditorContextIdentity(\"Variant\", activeVariantName)",
  "embedded context metadata must identify the concrete variant",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "mockupsPreviewImagePreloadResult",
  "WebView image preload must return a serializable request id and expose a synchronous result poll",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "asset-missing",
  "WebView patches must reject unresolved interned asset references before DOM mutation",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "mockupsMissingPreviewAssets",
  "the host must reconcile asset keys with the active WebView document before every patch",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "[\\\\s\\\"'<>)]",
  "data-URI compaction must not terminate generated SVG assets at literal parentheses",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebDesignPreviewRenderer.cs",
  "PreviewAssetRegistry.Compact(originalHtml)",
  "rendered frames must intern large assets before entering the shared frame cache",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "PreviewAssetRegistry.Keys(bodyContent)",
  "WebView patches must consume already compacted frame bodies",
);
assertDoesNotContain(
  "spikes/desktop-editor-shell/EditorShell/WebPreviewPanes.cs",
  "imageSourcesChanged",
  "resident registered assets must not force a decode-gated layer replacement on every animated image source change",
);
for (const forbiddenFontFallback of [
  "system-ui",
  "Apple Color Emoji",
  "Segoe UI Emoji",
  "Noto Color Emoji",
  "fontId === \"system\"",
]) {
  assertDoesNotContain(
    "src/desktop-preview/previewFontHelpers.ts",
    forbiddenFontFallback,
    `desktop render typography must not use host-system fallback '${forbiddenFontFallback}'`,
  );
}
assertContains(
  "src/desktop-preview/previewFontHelpers.ts",
  "Required production emoji font is unavailable",
  "missing contract emoji fonts must fail visibly instead of falling back to the host system",
);
assertContains(
  "src/desktop-preview/previewAssetResolver.ts",
  "Required production font file is missing",
  "missing production font files must fail before web rendering",
);
for (const requiredAssetIdentityTerm of ["stats.size", "stats.mtimeMs", "stats.ctimeMs"]) {
  assertContains(
    "src/desktop-preview/previewAssetResolver.ts",
    requiredAssetIdentityTerm,
    `preview video cache identity must include ${requiredAssetIdentityTerm}`,
  );
}
for (const requiredReferenceIdentityTerm of ["info.Length", "info.LastWriteTimeUtc.Ticks", "info.CreationTimeUtc.Ticks"]) {
  assertContains(
    "spikes/desktop-editor-shell/EditorShell/PreviewReferenceOverlay.cs",
    requiredReferenceIdentityTerm,
    `reference overlay cache identity must include ${requiredReferenceIdentityTerm}`,
  );
}
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryControlRegistry.cs",
  "ValueKind.TypographySystemStyle",
  "system-component typography must use its registered dictionary control route",
);
assertSourceMatches(
  "data/desktop-editor-spike.sqlite",
  currentComponentClassRows
    .filter((row) => row.component_type === "keyboard")
    .flatMap((row) => [row.config_json, row.metadata_json])
    .join("\n"),
  /"fontFamilyId":"theme\.system"/,
  "current Keyboard variants must use the Theme system-font role",
);
assertContains(
  "src/desktop-preview/textInputBarComponentResolver.ts",
  "fontFamilyId: \"theme.system\"",
  "Text Input Bar must bind its owned text surface to the Theme system-font role",
);
assertContains(
  "src/desktop-preview/statusBarComponentResolver.ts",
  "fontFamilyId: \"theme.system\"",
  "Status Bar text must use the Theme system-font role",
);
assertContains(
  "src/desktop-preview/audioComponentResolver.ts",
  "resolveLabelComponentFromRecords",
  "Audio calculated text must delegate its final visual presentation to Label",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/ComponentInputsPanel.cs",
  "ActionDurationInputValue(action, 1)",
  "declarative playback clocks must read the action duration input instead of a private fallback",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DesignPreviewPayloadFactory.cs",
  "Math.Max(0, delayMs) + durationMs",
  "motion action duration must include both declared delay and transition duration",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryMotionTimingControl.cs",
  "Unit: \"ms\"",
  "Motion Timing duration and delay subfields must declare millisecond units",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/DictionaryMotionTimingControl.cs",
  "definition.DisplayLabel",
  "compound Motion Timing labels must use shared FieldDefinition unit formatting",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_workspace == EditorWorkspace.Design",
  "preview input processing must explicitly separate Design from Production",
);
assertContains(
  "spikes/desktop-editor-shell/EditorShell/EditorPreviewController.cs",
  "_productionRuntimeResolver.Resolve",
  "Production preview must use the reference-only runtime resolver",
);
assertNoTerms(
  "spikes/desktop-editor-shell/EditorShell/ProductionPreviewRuntimeResolver.cs",
  ["ApplyTransientTestValues", "ComponentPreviewActions", "PlaybackTimeValue"],
);

function assertDesktopSystemTypographyData() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;

  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const fonts = new Map(
      (database.prepare("SELECT id, category FROM production_fonts").all() as { id: string; category: string }[])
        .map((font) => [font.id, font.category]),
    );
    const themes = database.prepare("SELECT id, tokens_json FROM themes").all() as {
      id: string;
      tokens_json: string;
    }[];
    for (const theme of themes) {
      const typography = jsonRecord(jsonRecord(jsonParse(theme.tokens_json)).typography);
      const systemFontId = typeof typography.systemFontFamilyId === "string"
        ? typography.systemFontFamilyId
        : "";
      if (!systemFontId || fonts.get(systemFontId) !== "text") {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `theme ${theme.id} systemFontFamilyId must reference a text production font`,
        );
      }
    }

    const keyboards = database
      .prepare("SELECT id, config_json, metadata_json FROM component_classes WHERE component_type = 'keyboard'")
      .all() as { id: string; config_json: string; metadata_json: string }[];
    for (const keyboard of keyboards) {
      const configFont = jsonRecord(jsonRecord(jsonParse(keyboard.config_json)).keyboard).typography;
      if (jsonRecord(configFont).fontFamilyId !== "theme.system") {
        addViolation(
          "data/desktop-editor-spike.sqlite",
          `Keyboard ${keyboard.id} config must use theme.system`,
        );
      }
      const variants = jsonArray(jsonRecord(jsonParse(keyboard.metadata_json)).variants).map(jsonRecord);
      for (const [index, variant] of variants.entries()) {
        const variantFont = jsonRecord(
          jsonRecord(jsonRecord(jsonRecord(variant.config).keyboard).typography),
        ).fontFamilyId;
        if (variantFont !== "theme.system") {
          addViolation(
            "data/desktop-editor-spike.sqlite",
            `Keyboard ${keyboard.id} variant ${index} must use theme.system`,
          );
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopDatabaseHasNoRetiredTimeFields() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const tables = database.prepare(
      "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'",
    ).all() as { name: string }[];
    for (const { name: table } of tables) {
      const columns = (database.prepare(`PRAGMA table_info("${table}")`).all() as { name: string; type: string }[])
        .filter((column) => column.type.toUpperCase().includes("TEXT"));
      if (columns.length === 0) continue;
      const quoted = columns.map((column) => `"${column.name}"`).join(", ");
      for (const row of database.prepare(`SELECT ${quoted} FROM "${table}"`).all() as Record<string, unknown>[]) {
        for (const column of columns) {
          const value = String(row[column.name] ?? "");
          for (const retired of retiredTimeFields) {
            if (value.includes(retired)) {
              addViolation("data/desktop-editor-spike.sqlite", `${table}.${column.name} contains retired time field ${retired}`);
            }
          }
        }
      }
    }
  } finally {
    database.close();
  }
}

function assertDesktopDatabaseHasNoRetiredEditorLayouts() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const retired = ["status_bar", "navigation_bar", "navigation.status_bars", "navigation.navigation_bars"];
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const placeholders = retired.map(() => "?").join(",");
    const rows = database.prepare(
      `SELECT record_class_id FROM editor_layouts WHERE record_class_id IN (${placeholders})`,
    ).all(...retired) as { record_class_id: string }[];
    for (const row of rows) {
      addViolation("data/desktop-editor-spike.sqlite", `contains retired editor layout ${row.record_class_id}`);
    }
  } finally {
    database.close();
  }
}

function assertDesktopDatabaseHasCurrentDefinitionLifecycle() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const genericApps = database.prepare(
      "SELECT id FROM apps WHERE record_class_id = 'app.generic'",
    ).all() as { id: string }[];
    for (const app of genericApps) {
      addViolation("data/desktop-editor-spike.sqlite", `contains retired generic App ${app.id}`);
    }
    const genericLayouts = database.prepare(
      "SELECT record_class_id FROM editor_layouts WHERE record_class_id IN ('app.generic', 'module.generic')",
    ).all() as { record_class_id: string }[];
    for (const layout of genericLayouts) {
      addViolation("data/desktop-editor-spike.sqlite", `contains retired generic editor layout ${layout.record_class_id}`);
    }
  } finally {
    database.close();
  }
}

function assertModuleInstanceRuntimePayloadsMatchContracts() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const instances = database.prepare(
      "SELECT mi.id, mi.content_json, m.design_preview_json FROM module_instances mi JOIN modules m ON m.id = mi.module_id",
    ).all() as { id: string; content_json: string; design_preview_json: string }[];
    for (const instance of instances) {
      const content = jsonRecord(jsonParse(instance.content_json));
      const contract = jsonRecord(jsonParse(instance.design_preview_json));
      for (const input of jsonArray(contract.inputs).map(jsonRecord)) {
        const key = typeof input.jsonKey === "string" ? input.jsonKey : "";
        const source = typeof input.source === "string" ? input.source : "runtime";
        if (!key) continue;
        if (source === "runtime" && !(key in content)) {
          addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} is missing runtime input ${key}`);
        }
        if (source !== "runtime" && key in content) {
          addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} persists parent-owned input ${key}`);
        }
      }
      for (const collection of jsonArray(contract.collections).map(jsonRecord)) {
        const key = typeof collection.sourceCollectionJsonKey === "string"
          ? collection.sourceCollectionJsonKey
          : typeof collection.jsonKey === "string" ? collection.jsonKey : "";
        if (!key || !Array.isArray(content[key])) {
          addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} is missing runtime collection ${key}`);
          continue;
        }
        const fields = jsonArray(collection.fields).map(jsonRecord);
        for (const item of jsonArray(content[key]).map(jsonRecord)) {
          if (typeof item.id !== "string" || !item.id) {
            addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} collection ${key} contains an item without stable id`);
          }
          for (const field of fields) {
            const fieldKey = typeof field.jsonKey === "string" ? field.jsonKey : "";
            const source = typeof field.source === "string" ? field.source : "runtime";
            if (fieldKey && source === "runtime" && !(fieldKey in item)) {
              addViolation("data/desktop-editor-spike.sqlite", `module instance ${instance.id} collection ${key} item is missing ${fieldKey}`);
            }
          }
        }
      }
    }

    const shots = database.prepare(
      "SELECT s.id, s.duration_frames, COALESCE(SUM(mi.duration_frames), 0) AS expected FROM shots s LEFT JOIN module_instances mi ON mi.shot_id = s.id GROUP BY s.id",
    ).all() as { id: string; duration_frames: number; expected: number }[];
    for (const shot of shots) {
      if (shot.duration_frames !== Math.max(1, shot.expected)) {
        addViolation("data/desktop-editor-spike.sqlite", `Shot ${shot.id} duration does not equal its cut-slot duration sum`);
      }
    }
  } finally {
    database.close();
  }
}

function assertConversationMessageActorOwnership() {
  const databasePath = path.join(root, "data", "desktop-editor-spike.sqlite");
  if (!existsSync(databasePath)) return;
  const database = new Database(databasePath, { readonly: true, fileMustExist: true });
  try {
    const actorProject = new Map(
      (database.prepare("SELECT id, project_id FROM actors").all() as { id: string; project_id: string }[])
        .map((actor) => [actor.id, actor.project_id]),
    );
    const modules = database.prepare(
      "SELECT id, design_preview_json FROM modules WHERE record_class_id = 'module.core.chat'",
    ).all() as { id: string; design_preview_json: string }[];
    for (const module of modules) {
      const preview = jsonRecord(jsonParse(module.design_preview_json));
      const messages = jsonArray(preview.collections).map(jsonRecord)
        .find((collection) => collection.id === "messages");
      if (!messages) {
        addViolation("data/desktop-editor-spike.sqlite", `Conversation Module ${module.id} has no messages collection contract`);
        continue;
      }
      const fields = jsonArray(messages.fields).map(jsonRecord);
      const actor = fields.find((field) => field.id === "actor");
      const direction = fields.find((field) => field.id === "direction");
      if (!actor
          || actor.enabledWhenItemJsonKey !== "direction"
          || !Array.isArray(actor.enabledWhenItemValues)
          || !actor.enabledWhenItemValues.includes("incoming")
          || !actor.enabledWhenItemValues.includes("system")
          || actor.allowEmptyWhenItemJsonKey !== "direction"
          || !Array.isArray(actor.allowEmptyWhenItemValues)
          || !actor.allowEmptyWhenItemValues.includes("system")
          || actor.allowEmpty === true) {
        addViolation("data/desktop-editor-spike.sqlite", `Conversation Module ${module.id} has incomplete Actor availability metadata`);
      }
      const transition = jsonRecord(direction?.transition);
      if (!direction
          || direction.defaultValue !== "system"
          || transition.targetInputId !== "actor"
          || transition.replacementValue !== ""
          || !jsonArray(transition.triggerValues).includes("outgoing")) {
        addViolation("data/desktop-editor-spike.sqlite", `Conversation Module ${module.id} has incomplete atomic outgoing transition metadata`);
      }
    }

    const instances = database.prepare(
      `SELECT mi.id, mi.content_json, a.project_id
       FROM module_instances mi
       JOIN modules m ON m.id = mi.module_id
       JOIN apps a ON a.id = m.app_id
       WHERE m.record_class_id = 'module.core.chat'`,
    ).all() as { id: string; content_json: string; project_id: string }[];
    for (const instance of instances) {
      const messages = jsonArray(jsonRecord(jsonParse(instance.content_json)).messages).map(jsonRecord);
      for (const [index, message] of messages.entries()) {
        const direction = typeof message.direction === "string" ? message.direction : "";
        const actorId = typeof message.actorId === "string" ? message.actorId : undefined;
        const context = `Conversation Screen ${instance.id} message ${String(message.id ?? index)}`;
        if (actorId === undefined) {
          addViolation("data/desktop-editor-spike.sqlite", `${context} has no current string actorId`);
          continue;
        }
        if (direction === "incoming") {
          if (!actorId || actorProject.get(actorId) !== instance.project_id) {
            addViolation("data/desktop-editor-spike.sqlite", `${context} requires an explicit same-Project incoming Actor`);
          }
        } else if (direction === "outgoing") {
          if (actorId) {
            addViolation("data/desktop-editor-spike.sqlite", `${context} must not persist an outgoing Actor`);
          }
        } else if (direction === "system") {
          if (actorId && actorProject.get(actorId) !== instance.project_id) {
            addViolation("data/desktop-editor-spike.sqlite", `${context} has a missing or cross-Project system Actor`);
          }
        } else {
          addViolation("data/desktop-editor-spike.sqlite", `${context} has unsupported direction ${direction}`);
        }
      }
    }
  } finally {
    database.close();
  }
}


assertDesktopSystemTypographyData();
assertSharedEditorSurfacesHaveNoConcreteEditors();
assertDesktopDatabaseHasNoRetiredTimeFields();
assertDesktopDatabaseHasNoRetiredEditorLayouts();
assertDesktopDatabaseHasCurrentDefinitionLifecycle();
assertDesktopRuntimeInputValueKindsAreCanonical();
assertModuleInstanceRuntimePayloadsMatchContracts();
assertConversationMessageActorOwnership();

if (violations.length > 0) {
  console.error("Desktop preview architecture check failed:");
  for (const violation of violations) {
    console.error(`- ${violation}`);
  }
  process.exit(1);
}

console.log("Desktop preview architecture boundaries validated.");
