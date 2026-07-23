# Action Duration Field Identity Contract

Status: normative.

This contract closes the `durationInputId` decision deferred by contract 74.
It applies to Design Preview actions, Module finite actions, collection item
actions, forwarded actions and recursively embedded Component actions.

## 1. `durationInputId` is a stable field id

`durationInputId` stores the exact non-empty `id` of one declared runtime
`FieldDefinition` owned by the action boundary. It never stores the field's
`jsonKey`.

The consumer must:

1. locate exactly one owner field whose `id` equals `durationInputId`;
2. require that field's explicit non-empty `jsonKey`;
3. read the current duration value through that `jsonKey`; and
4. require a positive finite JSON number when the finite action is active.

Missing fields, a JSON-key spelling used as `durationInputId`, missing or
malformed `jsonKey`, numeric strings, zero and negative values fail explicitly.
Readers must not accept both identities, search by name, infer a field from its
position or fall back to the raw `durationInputId` as a storage key.

## 2. Owner boundaries

For a root action, the owner definitions are the root runtime `inputs`. For a
collection item action, they are that exact collection's `fields`. An embedded
action first resolves its duration field in the embedded owner's contract; the
derived storage key may then be carried with the prepared transient action
definition so the host does not need to rediscover the child contract.

The resolved runtime value remains owned by the current action target:
top-level values stay on their root runtime object and collection values stay
on their stable item. This contract changes identity lookup, not runtime value
ownership.

## 3. Forwarding

Forwarding maps the child's stable duration field id to the forwarded parent's
stable field id. It does not replace `durationInputId` with the parent's
`jsonKey`.

This preserves one explicit identity across Component and Module boundaries.
The forwarded field definition remains responsible for its own storage key.
There is no short-id, JSON-key or name-based fallback when forwarding fails.

## 4. Action time remains a storage key

`timeJsonKey` remains the explicit storage key of the action clock. An action
clock can be calculated session state and therefore need not have a
`FieldDefinition`. It is not renamed to `timeInputId` by this contract.

`durationInputId` is different because its value is an authored runtime field
with dictionary metadata, forwarding identity and animation ownership.

## 5. One-shot data migration

The committed Conversation Module contract was migrated once from
`durationInputId: "playDurationFrames"` to
`durationInputId: "playDuration"`.

The runtime value continues to be stored as `playDurationFrames`, because that
is the explicit `jsonKey` of field `playDuration`. No runtime alias, dual reader
or migration routine remains.

## 6. Enforcement

Architecture enforcement must keep:

- exact field-id lookup in both web and desktop owner timelines;
- field-id-to-field-id mapping in Runtime Input forwarding;
- Design action lookup through the owner definition's `jsonKey`;
- committed Module action references that resolve to exact declared field ids;
- focused tests that reject a `durationInputId` written with the storage key.

Any future action duration field must enter through the same declared
dictionary/runtime contract. Component-specific action duration catalogs or
host-owned compatibility rules are forbidden.
