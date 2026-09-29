# Release history

The active release text and media remain part of the normal project tree and therefore participate in Git history.

Publishing text is stored as the canonical Markdown files described in [Publishing content](publishing-content.md). The authoritative version is still read from the primary runtime addon's TOC; publishing filenames do not duplicate the version.

## Git projects

For a Git-backed project, a public release is represented by:

- the source version read from the TOC
- the exact Git commit
- a Git tag, conventionally `v<version>`
- the generated package and its SHA-256 hash
- optional CurseForge publication metadata

The Git tag is the historical snapshot. The Studio does not duplicate every historical release into `Release/Versions/`.

Because descriptions, release notes and media are committed project files, their state for an old release can be read directly from the tagged commit.

A release should normally require a clean working tree and a version tag that does not already exist.

## Projects without Git

Git remains optional.

For a project without Git, the Studio creates an immutable release snapshot so the exact released project state can still be reconstructed.

The snapshot mechanism is an internal release-history feature and does not change the fixed active project layout.

## SQLite

SQLite stores release history and external state, for example:

- version
- release source kind: Git tag or Studio snapshot
- Git tag/commit when applicable
- package hash
- creation time
- CurseForge file id and publication state

SQLite does not replace the canonical project files.

## CurseForge

CurseForge publication uses the already-created release record and package.

A successful publication can attach the returned CurseForge file identity to the release history.
