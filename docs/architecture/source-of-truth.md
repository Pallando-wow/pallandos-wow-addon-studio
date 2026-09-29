# Sources of truth

Pallando's WoW Addon Studio must not create competing copies of project information.

The canonical project is the fixed project directory on disk. The Studio combines project files, detected state, TOC metadata and local history.

## project.json

`project.json` contains durable project intent:

- stable project id
- display name
- project type: addon or library
- primary runtime addon
- runtime addon membership below `AddOns/`
- optional reusable component usages and target paths
- optional release package-name override
- optional non-secret CurseForge project binding

It does not contain the current addon version, TOC metadata, Git status, test results, hashes or installed versions.

## Fixed project tree

The Studio understands these fixed areas:

- `project.json`
- `AddOns/`
- `Documentation/`
- `Release/`
- `Media/`

The project layout itself is not configurable.

`Documentation/`, `Release/` and `Media/` may be absent when unused.

## Automatically detected state

The Studio detects information already represented by files or Git, including:

- project path
- runtime folder existence
- source files and hashes
- documentation
- release text
- logos and screenshots
- Git repository presence
- branch, HEAD commit, status, tags and remotes
- component files and resolved component revisions where detectable

Git is optional and detected automatically.

## TOC files

TOC files are authoritative for WoW runtime metadata.

The Studio reads metadata per TOC file, including:

- Title
- Notes
- Author
- Version
- Interface
- Dependencies / RequiredDeps
- OptionalDeps
- SavedVariables
- SavedVariablesPerCharacter
- load metadata
- X-* metadata
- ordered runtime file entries

Conflicts between TOCs are reported rather than silently collapsed into invented values.

## Component catalog

The Studio maintains reusable component definitions separately from project manifests.

A component definition describes a reusable WoW-addon resource such as a library, UI framework or asset pack and records its source.

Projects reference component ids and define where the component is placed inside their runtime addons.

Resolved component revision/hash information may be stored in Studio state and release history for reproducibility.

## SQLite

SQLite stores Studio state, cache and history, including:

- last scan
- cached hashes
- source-to-WoW-Forever comparison
- local WoW Forever installation configuration
- installed versions
- deployment history and verification
- test history
- quality-check history
- release records
- component catalog/local resolution state
- CurseForge synchronization state
- search index
- UI preferences

Deleting SQLite may lose local cache/history/settings, but it must not destroy the canonical addon project.

## Release history

For Git projects, release history is anchored by an exact commit and Git tag.

For projects without Git, the Studio creates an immutable snapshot.

Descriptions, release notes and media for Git-backed historical versions are read from the tagged project state rather than duplicated into permanent `Release/Versions/` folders.

## Decision rule

When adding information:

1. If WoW defines it in a TOC, read the TOC.
2. If files or Git determine it reliably, detect it.
3. If it is durable project intent, store it in `project.json`.
4. If it defines a reusable component source, store it in the component catalog.
5. If it is local state, cache or history, store it in SQLite.
6. If it belongs to CurseForge, retain only the non-secret binding locally and read remote state from CurseForge.
