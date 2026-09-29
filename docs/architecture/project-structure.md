# Project structure

Pallando's WoW Addon Studio uses one fixed directory convention for all managed projects.

## Standard layout

```text
<ProjectRoot>/
├─ project.json
├─ AddOns/
│  └─ <RuntimeAddon>/
├─ Documentation/
├─ Release/
└─ Media/
   ├─ Logo/
   └─ Screenshots/
```

The names and locations of these areas are fixed. The Studio does not support project-specific aliases such as `Runtime/`, `Docs/`, `CurseForge/` or arbitrary media directories.

This structure is required for projects after they are created or imported into the Studio. Raw source material selected for import is not expected to follow it.

Creation and import behavior is defined in [Creation and import](creation-and-import.md).

## Required project elements

Every managed project requires:

- `project.json`
- `AddOns/`
- at least one runtime addon folder declared by `project.json`

Both supported project types, addon and library, use the same fixed runtime root.

A library is therefore stored like this:

```text
LibSemanticData/
├─ project.json
├─ AddOns/
│  └─ LibSemanticData-1.0/
├─ Documentation/
├─ Release/
└─ Media/
```

There is no special runtime-root override for libraries.

## Reserved optional areas

`Documentation/`, `Release/` and `Media/` have fixed names and fixed purposes.

They may be absent when a project does not use that area yet. New projects created or normalized by the Studio may create these directories proactively, but a fresh Git checkout must not be considered invalid merely because an otherwise empty optional directory is absent.

If the area is used, it must use the standard path.

### Documentation

Known documentation filenames include:

- `PROJECT.md`
- `TEST_STATUS.md`
- `ARCHITECTURE.md`
- `CHANGELOG.md`
- `NOTES.md`

Additional project-specific documentation is allowed below `Documentation/`.

### Release

Publishing text uses separate canonical Markdown files with WoW locale codes:

- `SUMMARY.<locale>.md`
- `DESCRIPTION.<locale>.md`
- `RELEASE_NOTES.<locale>.md`

`enUS` is the default publishing locale. Files are created on demand when content is first saved.

The detailed model is defined in [Publishing content](publishing-content.md).

### Media

Media uses:

```text
Media/
├─ Logo/
│  └─ logo.png
└─ Screenshots/
   └─ NN-name[-locale].png
```

Screenshot numbering defines display order.

## Multi-addon projects

An addon project may contain one or multiple runtime addon folders directly below the same `AddOns/` root.

The manifest declares which folder is the primary runtime addon and which runtime addon folders belong to the project. Broker Panels is an example of a multi-addon project; it does not require a separate project type.

## Global project root

The global project root is the Studio workspace for normalized, managed projects.

Each direct child that represents a project must contain a valid `project.json`. Folders without `project.json` are unmanaged source/folders and are not opened as Studio projects.

Legacy or third-party source should normally remain outside the global project root until it is imported.

## Import rule

Existing addons and libraries may have arbitrary source layouts and commonly contain only TOC, Lua, XML and asset files.

When added to the Studio, their current state is copied into a newly created project and normalized into the fixed Studio structure. The source directory remains unchanged.

An already valid Studio project is opened directly and is not normalized again.

## Development source and WoW target

After creation or import, the normalized Studio project tree is the development source.

The configured WoW Forever `Interface/AddOns` directory is only a deployment and test target. Development must not happen directly in the WoW target directory.
