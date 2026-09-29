# Product scope

Pallando's WoW Addon Studio is a specialized development environment exclusively for World of Warcraft addons targeting WoW Forever.

It is not intended to become a general-purpose IDE.

## Supported external systems

The supported external scope is intentionally small enough that it can be tested end-to-end:

- WoW Forever as the runtime, deployment and test target
- Git for version-control integration
- CurseForge for publishing integration

Git and CurseForge are supported capabilities, but an individual project does not have to use either one.

Additional WoW clients and additional publishing providers are outside the supported scope unless this decision is changed explicitly in a future version.

## Managed object types

The Studio manages two primary project types:

- addon
- library

An addon project may contain one or multiple runtime addon folders below the fixed `AddOns/` directory.

The Studio also maintains a reusable component catalog. Components are not projects by themselves unless their source is a Studio-managed library project.

Examples of components include:

- libraries
- UI frameworks
- icon and asset packs

A project only references the components it actually uses.

## Creation and import

The Studio starts from real-world WoW addon source, not from the assumption that `project.json` already exists.

Addon:

- Create new addon
- Import existing addon

Library:

- Create new library
- Add existing library
- Add external library

Existing addons and owned/editable libraries are copied from source locations outside the global project root into a new Studio project and normalized to the fixed project structure. Their original source is not modified.

The global project root is reserved for managed Studio projects. Folders there without `project.json` remain unmanaged and may be identified as requiring import.

External libraries are registered as reusable components rather than converted into editable Studio projects.

An existing valid Studio project is opened directly.

## Product areas

The long-term product scope includes:

- project and library creation
- import and normalization of existing addons/libraries
- external library registration
- reusable component catalog and component installation
- WoW Forever runtime addon discovery and TOC parsing
- WoW Forever installation configuration
- deployment and source-to-client comparison
- Git integration for addon projects
- documentation and media management
- Lua syntax checking and WoW-aware static analysis
- WoW Lua editor
- WoW XML editor
- TOC editor
- localization tooling
- WoW Forever API browser
- quality and release-readiness checks
- packaging and ZIP inspection
- versioned release history through Git tags or Studio snapshots
- CurseForge publishing integration

Every feature must have a clear purpose in the development, testing, management or publishing of WoW Forever addons.

## Component rule

No reusable component is mandatory.

SharedUI, LibSemanticData, Ace-style libraries or asset packs are examples of optional components that can be selected per project.

Component updates are applied project by project.

## Editor rule

The future editor is intentionally WoW-specific. It should understand Lua in the WoW Forever environment, TOC load order, XML references, localization, WoW APIs and addon project boundaries rather than attempting to reproduce a general editor such as VS Code.
