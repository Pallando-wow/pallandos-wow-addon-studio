# Creation and import

Pallando's WoW Addon Studio distinguishes between Studio-managed projects and source material that is being brought into the Studio.

A `project.json` is required only after a project has been created or imported into the Studio. Existing WoW addons and libraries are not expected to contain Studio metadata.

## Addon

The Addon action offers:

- Create new addon
- Import existing addon

### Create new addon

The Studio creates the fixed project structure and an initial `project.json`.

At minimum:

```text
<ProjectRoot>/
├─ project.json
└─ AddOns/
   └─ <AddonName>/
      └─ <AddonName>.toc
```

The Studio may also create the reserved optional directories `Documentation/`, `Release/` and `Media/`.

### Import existing addon

The user selects an existing WoW addon or a directory containing multiple related addon folders.

Typical source material may contain only WoW runtime files:

```text
SomeAddon/
├─ SomeAddon.toc
├─ Core.lua
├─ Options.lua
└─ Frames.xml
```

The source does not need a `project.json`.

Raw/legacy addon sources live outside the configured global project root. The global project root is reserved for Studio-managed projects.

The first import stage is deliberately limited to runtime addon content.

The Studio inspects the source, identifies the runtime addon folders, then copies only those addon folders into a new Studio project below the global project root and normalizes them below `AddOns/`.

Documentation, CurseForge/project-publishing files, screenshots and other project-side material are not imported automatically. Their structure, filenames, language and version context are not reliable enough to infer safely. They can be migrated later through dedicated imports or manual editing.

The source directory is never modified by this operation.

Example result:

```text
SomeAddon/
├─ project.json
├─ AddOns/
│  └─ SomeAddon/
│     ├─ SomeAddon.toc
│     ├─ Core.lua
│     ├─ Options.lua
│     └─ Frames.xml
├─ Documentation/
├─ Release/
└─ Media/
```

For a multi-addon source, every selected runtime addon is copied below the single fixed `AddOns/` root. One runtime addon is selected as primary.

## Library

The Library action offers:

- Create new library
- Add existing library
- Add external library

### Create new library

The Studio creates a new editable library project using the same fixed project layout as addons.

### Add existing library

This is for a library the user wants to manage and develop as a Studio project.

The selected source is inspected, copied and normalized into a new project of type `library`. The original source remains unchanged.

After import, the library project can also be registered as a reusable component source for other projects.

### Add external library

This is for third-party or externally maintained libraries such as Ace3 or LibStub that the user wants to consume but not manage as an editable Studio project.

An external library is added to the component catalog rather than converted into a Studio project.

Its source can be:

- a Git repository
- a local directory

The component catalog keeps the library identity and source information. Projects can then select that library and define where it is copied into the project's runtime addon.

Updating an external library updates the selected project's component copy explicitly; it does not turn the external library into a Studio-managed source project.

## Global project root

The configured global project root contains Studio-managed projects only.

A direct child without `project.json` is treated as an unmanaged folder. If it contains a recognizable addon layout, the Studio may label it as `Import required`, but it is not opened as a managed project.

For safe copy-import behavior, legacy/raw sources should be kept outside the global project root and imported into it.

## Existing Studio project

If the selected directory already contains a valid `project.json` and fixed Studio layout, the Studio opens/adds that project directly.

It is not imported or normalized again.

## Safety rule

Import and normalization are copy operations.

The original addon or library source remains untouched so that importing cannot damage the user's existing working copy or downloaded addon.
