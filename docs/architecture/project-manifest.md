# Project manifest

`project.json` is the portable, machine-readable project definition used by Pallando's WoW Addon Studio.

The fixed project layout is defined in [Project structure](project-structure.md).

The ownership rules for project information are defined in [Sources of truth](source-of-truth.md).

Reusable content is described in [Components](components.md), and release history in [Release history](release-history.md).

## Fixed project layout

Every managed project uses the fixed `AddOns/` runtime root. The runtime root is not configurable.

The two project types are:

- `addon`
- `library`

An addon project may contain one or multiple runtime addon folders.

## project.json

The manifest stores durable project intent that cannot be derived reliably elsewhere.

It contains:

- project id, name and type
- primary runtime addon
- runtime addon membership
- optional component usages and their placement
- optional release package-name override
- optional CurseForge project binding

It does not duplicate TOC metadata or detected Git state.

## Components

`components` contains only reusable components used by this project.

A component usage references a component catalog id and defines one or more target locations inside declared runtime addons.

A project without reusable components simply omits the section or uses an empty list.

Git and CurseForge are not components.

## Runtime metadata

TOC files remain authoritative for runtime metadata such as:

- version
- interface values
- dependencies
- SavedVariables
- title
- notes
- author
- ordered runtime files

## Git

Git is detected automatically. No Git setting is required in `project.json`.

When Git is present, tagged commits are the preferred source for immutable release history.

## CurseForge

CurseForge is the only supported publishing provider.

A project only contains a `curseForge` section when it is bound to a CurseForge project.

Credentials are never stored in `project.json`.

## Schema evolution

The manifest is versioned through `schemaVersion`. Breaking changes require a new schema version and an explicit migration path.
