import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";

const root = process.cwd();
const read = (relativePath: string) =>
  readFileSync(path.join(root, relativePath), "utf8");

test("record creation has one catalog, form and persistence route", () => {
  const port = read(
    "src/Mockups.Application.PersistencePorts/ApplicationDataPorts.cs",
  );
  const workflow = read(
    "src/Mockups.Desktop/EditorShell/EditorAddChildWorkflow.cs",
  );
  const persistence = read(
    "src/Mockups.Persistence.Sqlite/SqliteEditorChildStore.cs",
  );
  const navigation = read(
    "src/Mockups.Desktop/EditorShell/EditorNavigationMetadata.cs",
  );

  assert.match(port, /PrepareRecordCreation/);
  assert.match(port, /CreateRecord/);
  assert.doesNotMatch(port, /ProjectTreeNode AddChild\(/);
  assert.doesNotMatch(port, /ProjectTreeNode AddShot\(/);
  assert.doesNotMatch(port, /ProjectTreeNode AddTheme\(/);
  assert.doesNotMatch(workflow, /parent\.Kind\s*==/);
  assert.match(workflow, /EditorAddOperationCatalog\.TryGet/);
  assert.match(persistence, /_creationPreparers/);
  assert.match(persistence, /_creationCommitters/);
  assert.doesNotMatch(persistence, /internal ProjectTreeNode AddChild/);
  assert.doesNotMatch(persistence, /internal ProjectTreeNode AddShot/);
  assert.doesNotMatch(persistence, /internal ProjectTreeNode AddTheme/);
  assert.match(navigation, /EditorAddOperationCatalog\.Require/);
  assert.equal(
    existsSync(path.join(
      root,
      "src/Mockups.Desktop/EditorShell/ShotCreationDialog.cs",
    )),
    false,
  );
});

test("Actor creation cannot persist empty required references", () => {
  const actorRepository = read(
    "src/Mockups.Persistence.Sqlite.Resources/ActorRepository.cs",
  );
  const referenceIntegrity = read(
    "src/Mockups.Persistence.Sqlite.Core/ProjectReferenceIntegrity.cs",
  );

  assert.doesNotMatch(
    actorRepository,
    /VALUES \(\$id, \$projectId, \$displayName, \$shortName, '', ''/,
  );
  assert.match(actorRepository, /required: true/);
  assert.match(actorRepository, /RequiredPalettePair/);
  assert.match(referenceIntegrity, /Actor '\{actor\.Id\}' default Device[\s\S]*required: true/);
  assert.match(referenceIntegrity, /Actor '\{actor\.Id\}' default Theme[\s\S]*required: true/);
});

test("Episode and Icon Theme aggregate writes are atomic", () => {
  const episodes = read(
    "src/Mockups.Persistence.Sqlite.Production/ProjectEpisodeRepository.cs",
  );
  const iconThemes = read(
    "src/Mockups.Persistence.Sqlite.Resources/SqliteResourceOwner.IconThemes.cs",
  );
  const iconRepository = read(
    "src/Mockups.Persistence.Sqlite.Resources/IconThemeRepository.cs",
  );

  assert.match(episodes, /INSERT INTO episodes \(id, project_id, name, slug/);
  assert.match(episodes, /using var transaction = connection\.BeginTransaction\(\)/);
  assert.match(episodes, /_moduleInstanceRepository\.Insert/);
  assert.match(iconThemes, /using var transaction = connection\.BeginTransaction\(\)/);
  assert.match(iconThemes, /transaction\.Commit\(\)/);
  assert.doesNotMatch(
    iconRepository,
    /VALUES \(\$id, \$projectId, \$name, \$assetRoot, '\{\}'/,
  );
});
