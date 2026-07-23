# System Bar Item Authoring Contract

Status: normative.

This contract governs Status Bar and Navigation Bar item authoring in the
desktop editor. It extends the current-JSON and Variant rules in contracts 35
and 36 without introducing a shared preview resolver for the two components.

## 1. Objective

System-bar items use the same complete metadata-driven editor route as other
Component Variant values:

```text
editor layout metadata
→ ComponentClassFieldCatalog
→ FieldDefinition / ValueKind.StructuredCollection
→ DictionaryStructuredCollectionControl
→ active complete Component Variant config
→ component-owned resolver
→ component-owned renderable
```

There is no Status Bar or Navigation Bar collection editor parallel to this
route. `MainWindow`, `EditorCollectionCardFactory` and shared dictionary
surfaces do not know either component type.

## 2. Variant ownership

`items` belongs to the concrete Status Bar or Navigation Bar Variant config.
The Default Variant remains the parity source for
`component_classes.config_json`; every non-default Variant owns its own
complete `items` snapshot.

The Items card is declared in the record-class layout, so it is available for
every active Variant. An edit never targets a Component class by display name,
the currently visible row position or an inferred default. It commits through
the selected Variant owner and preserves every unrelated config property.

## 3. Stable item identity and fixed structure

Every item has a required non-empty stable `id`, unique within its collection.
Dictionary field ids and session presentation keys derive from that id, never
from the array index.

The current item sets are development-owned fixed structures. The user may
edit the declared values, zones and order values but cannot add, duplicate,
delete or drag-reorder these rows in the editor. Array order is storage order;
visual placement is the component-owned result of explicit `zone` and `order`.

Fixed structure is declared by collection metadata
(`CanEditStructure: false`), not inferred from the component type, card label,
item count or position.

## 4. Dictionary fields

Status Bar exposes dictionary fields according to item kind:

- text value for `text`;
- icon token for `iconToken`;
- bounded integer signal value for `generatedSignal`;
- bounded integer battery value and explicit charging boolean for
  `generatedBattery`;
- explicit left/right/off zone and integer order for every item.

Navigation Bar exposes explicit left/center/right/off zone and integer order.
Its generated button kind and both components' item labels are current
contract data used for presentation, not editable scalar controls.

Conditional field visibility reads the explicit item `kind`. It does not
infer behavior from an item id, label, array index or visual position.

## 5. Strict current documents

Status Bar and Navigation Bar retain separate component contracts. Each
current class config and every Variant snapshot must contain its exact current
schema version, complete component-level fields and an `items` array.

Every item must be an object with:

- a unique non-empty `id`;
- a non-empty `label`;
- a supported explicit `kind`;
- a supported explicit `zone`;
- an explicit integer `order`;
- the kind-owned value/token/charging members required by its component.

Readers, repositories, editors and resolvers reject incomplete or unsupported
documents. They do not synthesize default items, repair fields, coerce an
unknown kind/zone or extend the array during a write.

## 6. Complete-property preservation

Editing one item field modifies that property on the current in-memory item
object and commits the complete collection through the generic field path.
Properties not exposed in the current editor, including component-owned item
metadata, remain unchanged. A commit must not reconstruct an item from a
partial UI DTO or discard unknown-but-current owner properties.

## 7. Preview boundary

Status Bar and Navigation Bar continue to have separate contracts, resolvers
and renderable modules. Their resolvers validate the prepared component config
and emit complete resolved item values. Renderables use required ids, kinds,
tokens and values directly; they do not fall back to labels, indices, default
kinds or zero values.

This phase does not create a shared `systemBar` contract, resolver or
renderable. Common editor collection mechanics do not imply shared component
semantics across the Preview boundary.

## 8. Migration and proof

The explicit one-time migration appends the Items card to only
`component.status_bar` and `component.navigation_bar` editor layouts. Existing
component configs already satisfy the strict item contracts and are not
rewritten.

Tests and architecture enforcement prove:

- malformed component or Variant item documents fail read-only;
- invalid explicit writes fail without changing the database;
- non-default Variants can persist their own item values;
- hidden owner properties survive an item edit;
- both layouts declare their structured collection field;
- the two retired bespoke editors and their index-based facade APIs cannot
  return;
- Preview component modules contain no item identity/value fallback.

## 9. Forbidden shortcuts

- selecting or updating an item by array index after the UI already has its id;
- capturing an item DTO and later overwriting newer sibling-field edits;
- exposing Items only on the Component class or Default Variant;
- reconstructing item JSON from just the visible fields;
- adding raw scalar controls inside an item row;
- enabling structural actions because the generic collection supports them;
- inferring kind, zone, order or identity from names or positions;
- sharing a Status/Navigation resolver or renderable to mirror shared editor
  mechanics;
- adding default item builders, normalizers or compatibility fallbacks to a
  normal read or write path.
