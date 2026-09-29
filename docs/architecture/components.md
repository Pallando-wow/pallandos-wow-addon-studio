# Components

Components are reusable WoW-addon content that can be added to projects.

Examples:

- libraries such as Ace3 or LibStub
- Studio-managed libraries such as LibSemanticData
- UI frameworks such as SharedUI
- icon or asset packs

Git and CurseForge are not components.

## Component catalog

The Studio maintains a local component catalog. A component definition contains:

- stable id
- display name
- kind
- source

Supported component kinds include:

- library
- UI
- asset/icon pack

Supported source types are:

- Studio library project
- Git repository
- local directory

A Git-backed component may additionally track a tag, branch or commit reference.

## External libraries

An external library is a component whose source is maintained outside the Studio project collection.

For example, Ace3 or LibStub can be registered through **Add external library** with a Git repository or local-directory source. They are then selectable by projects without being converted into editable Studio library projects.

A library that the user owns or wants to develop inside the Studio should instead be created or imported as a project of type `library`.

## Project usage

A project references a component by id and defines one or more targets.

Each target identifies:

- the runtime addon that receives the component
- the relative destination path inside that runtime addon

Example:

```json
{
  "id": "shared-ui",
  "targets": [
    {
      "runtimeAddon": "ForeverBag",
      "path": "SharedUI"
    }
  ]
}
```

This keeps the project structure fixed while still allowing different component layouts inside runtime addons.

## Updates

Component updates are project-scoped.

The Studio must not perform uncontrolled global updates across all projects. It may show available updates globally, but applying an update is an explicit action for one project at a time.

## Reproducibility

The Studio records the resolved component source revision and content hash when needed for deployment, release or history.

The component catalog describes where a component comes from. A project manifest describes where that component is used.
