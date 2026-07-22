# Conversation Message Actor Ownership Contract

Status: normative.

This document governs Actor ownership for persisted Conversation messages and
their resolved Production payload. It extends contracts 34, 41, 47, 51, 53,
57, 60 and 61 without weakening their explicit-reference or boundary rules.

## 1. Canonical rule by direction

Every current Conversation message has one exact `direction` and one current
string `actorId` field. Its meaning is direction-owned:

- `incoming`: `actorId` is required and must reference an Actor in the same
  Project as the Conversation Module Instance;
- `outgoing`: persisted `actorId` is empty; the effective Actor in Production
  is always the exact owner Actor of the containing Shot;
- `system`: `actorId` is optional; when present it must reference an Actor in
  the same Project, and when absent the resolved Actor value is empty.

Only `incoming`, `outgoing` and `system` are current directions. Missing,
unknown or wrong-kind values fail current-document validation.

Changing the Shot owner Actor immediately changes the effective Actor of every
outgoing message. It does not rewrite message documents. Incoming and optional
system references remain unchanged.

## 2. Persisted data and Production payload

The persisted Module Instance document is the authority for incoming and
system message references. It never stores a duplicated outgoing owner.

Production payload preparation applies the direction rule before generic
record-reference resolution:

```text
current Conversation Module Instance content
→ exact Conversation message Actor contract
→ outgoing actorId projected from exact Shot owner Actor
→ generic declared record-reference resolution
→ complete resolved module payload
→ component resolvers/renderables
→ generic bridge and renderer
```

The outgoing projection is payload-only. It is not a migration, repair or
write-back. A system message with no Actor resolves to an empty Actor value,
never a sample Actor. Isolated Design Preview may retain deliberate sample data
because it is not a persisted Production payload.

## 3. Owner and routing boundaries

One pure Conversation message Actor contract owns validation and Production
projection. A route-only Module runtime-document registry selects it by the
exact stable Module record class `module.core.chat`.

The registry must not infer a route from names, App identity, Variant, payload
shape, field position or collection order. It contains no validation or
projection rules of its own.

Repositories remain responsible only for complete current document reads and
prepared writes. The desktop persistence coordinator invokes the domain
contract before every Conversation content write and during read-only startup
validation. The Runtime Inputs editor declares interaction metadata but does
not reproduce the direction rule.

## 4. Editor interaction

The generic `messages` collection declares:

- Actor availability for incoming and system messages;
- an optional empty Actor choice for system messages;
- a direction transition that clears `actorId` when `outgoing` is selected;
- `system` as the safe default direction for a newly prepared blank message.

Declared multi-field transitions must be written atomically as one prepared
collection-item update. The editor must never persist an intermediate outgoing
message with a non-empty Actor.

An incoming message cannot be persisted without first selecting its explicit
Actor. No Actor may be selected from a name, type, text value, order or
position.

## 5. Visual identity

The effective Actor reference and visual identity are separate concerns:

- group incoming messages may expose their Actor identity to Bubble;
- outgoing messages never expose per-message Actor identity;
- system messages never expose per-message Actor identity, even when their
  optional Actor reference is present for semantic content.

Conversation owns this child-composition decision. Bubble continues to own the
actual Avatar/Label layout, and the bridge and renderer receive only resolved
visual state.

## 6. Migration and current-data requirements

The cutover is one explicit migration of the committed parity database:

- clear every persisted outgoing `actorId`;
- preserve valid incoming Actor ids;
- preserve blank or valid system Actor ids;
- update the current collection metadata for availability, optionality,
  transition and safe new-item defaults.

After migration there is no legacy reader, alias, fallback, normalization or
startup repair. Invalid current data fails with an actionable persistence
contract error.

## 7. Enforcement and tests

Automated coverage must prove:

- all three direction rules, including same-Project Actor validation;
- outgoing Production projection follows a changed Shot owner without a
  message rewrite;
- blank system Actor resolution never creates `sample_actor`;
- incoming Actor references remain explicit;
- direction plus Actor clearing is one atomic collection write;
- outgoing and system Bubble composition hides Actor identity;
- startup validation is byte-for-byte read-only;
- committed parity data and collection metadata satisfy this contract;
- the payload factory, bridge and renderer contain no sample Actor repair or
  inferred ownership rule.

## 8. Forbidden shortcuts

- persisting the Shot owner Actor redundantly in outgoing messages;
- inferring an incoming or system Actor from header Actor, Shot Actor, name,
  type, message text, array order or UI position;
- resolving a blank Production message Actor as sample data;
- scanning arbitrary JSON or text to discover message Actor references;
- implementing direction semantics in the generic bridge, renderer, tree or
  collection UI;
- allowing an invalid intermediate document between direction and Actor
  updates;
- changing outgoing Actor presentation by mutating a selected Bubble Variant.
