# Icon Theme Persistence and Asset Contract

Status: normative.

This document governs the Icon Theme slice of the staged desktop repository
extraction. It extends contracts 24, 33, 35, 36 and 39 without changing schema,
current parity data, SVG assets or editor presentation.

## 1. Persistence ownership

Current Icon Theme rows follow one route:

```text
icon_themes current rows
→ IconThemeRepository
→ SpikeDatabase compatibility facade
→ asset workflow, field service or Preview payload caller
```

`IconThemeRepository` is the only ordinary owner of `icon_themes` SQL and row
materialization. It owns exact lookup, coordinated tree reads, explicit set
discovery upsert, duplicate-row creation, mapping writes, identity/asset-root
writes and deletion.

Every row keeps its stable id, explicit Project, display name, asset root,
required object `mapping_json` and required object `metadata_json`. Repository
reads and writes reject malformed or wrong-root JSON. They never repair a
current document or replace it with a plausible empty value.

## 2. Assets stay outside persistence

The repository does not inspect or modify the filesystem. The existing Icon
Theme asset workflow remains responsible for:

- discovering explicit set directories during Refresh;
- reading and validating manifests;
- calculating safe Project-relative asset paths;
- copying, moving and deleting set directories;
- reading, replacing and generating SVG files;
- invoking the external icon-generation script;
- building token mappings and metadata from explicit asset input.

The facade coordinates those operations and submits complete current documents
to the repository. The repository does not construct tree nodes, editor
controls, Preview token values or provider-specific asset definitions.

## 3. Strict token file references

Every persisted token used for SVG access must contain an explicit `file`
value. It must be a local filename with the `.svg` extension. Reading a token
with a blank, missing, nested or non-SVG file reference fails before any file
access or database mutation.

The retired behavior that supplied `${token}.svg` and wrote it back while
reading was a compatibility repair. It is forbidden. Adding or changing a file
reference belongs only to the explicit Refresh, manual replacement or token
generation write workflow.

Runtime icon-generation data also requires the current explicit
`metadata_json.iconSet` object. An ordinary read must not reconstruct provider,
style or weight from the Icon Theme name or row position.

## 4. Lifecycle and coordination

Refresh, duplicate, rename and delete remain explicit user actions. Asset work
is completed or rolled back by the facade around the repository write:

- duplicate copies the selected asset directory, then inserts a new row with a
  generated stable id and the selected row's complete mapping;
- rename moves the selected directory, updates its manifest and then writes the
  exact new name, asset root and metadata;
- delete passes the typed Usage guard, removes the exact safe asset directory
  and deletes the exact row;
- Refresh discovers current directories and explicitly writes their current
  mappings and metadata.

No lifecycle action may select a referenced Icon Theme from a label, provider,
filename, directory order or tree position. Theme references remain exact ids.

## 5. Validation

Automated enforcement verifies:

- the repository contract and implementation are explicit;
- the facade constructs and delegates to it;
- Icon Theme facade, search and tree files contain no `icon_themes` SQL;
- the repository imports no filesystem, SVG, process, Preview or tree concern;
- facade and repository reads agree on a disposable database copy;
- invalid mapping or metadata documents fail before a write;
- reading a token without `file` fails byte-for-byte read-only;
- the committed database and asset tree remain unchanged by the extraction.

## 6. Forbidden shortcuts

- querying or writing `icon_themes` directly from the tree or editor shell;
- reading, copying or deleting files in `IconThemeRepository`;
- repairing `mapping_json` during a read;
- supplying a token filename from its token id;
- reconstructing current `iconSet` metadata from a display name at runtime;
- selecting a Theme reference through provider, style, name or ordering;
- accepting blank, malformed or wrong-root current JSON;
- using startup, Preview resolution or a picker read as Refresh or migration;
- changing parity rows or SVG assets as an incidental repository extraction.
