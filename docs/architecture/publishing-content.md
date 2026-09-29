# Publishing content

Publishing text is canonical project content and is stored as three Markdown files below the fixed `Release/` directory.

The Studio does not infer publishing text from arbitrary legacy documentation during the initial addon import.

## Canonical files

```text
Release/
├─ SUMMARY.md
├─ DESCRIPTION.md
└─ CHANGELOG.md
```

The files are created on demand when content is first saved. Empty publishing files are not required for a valid project.

## Responsibilities

- `SUMMARY.md` contains one short English project summary.
- `DESCRIPTION.md` contains the full project description. English comes first; additional language sections may follow in the same document.
- `CHANGELOG.md` contains the changelog for the file or release currently being prepared. English comes first; additional language sections may follow in the same document.

The files contain content only. They do not carry the authoritative addon version.

## Multilingual content

Summary remains English-only because the publishing target exposes one short summary field and a multilingual one-line summary would become difficult to read.

Description and Changelog each map to one publishing field. Multiple languages therefore live in one Markdown document instead of separate locale files.

A typical document can use language headings:

```markdown
## English

English content.

---

## Deutsch

Deutscher Inhalt.
```

The Studio does not automatically translate, reorder or synchronize language sections.

## Version source

The current addon version is read from the primary runtime addon's TOC.

`CHANGELOG.md` represents the file or release currently being prepared. Its filename does not contain a version number.

For Git projects, historical publishing content is recovered from the release tag. For non-Git projects, it is recovered from the immutable Studio release snapshot.

This avoids maintaining a second versioned directory tree below `Release/`.

## Legacy content

Legacy projects may contain Word documents, text files, Markdown files, CurseForge helper files or mixed documentation with unrelated or outdated version information.

The Studio must not silently map that material to publishing fields.

Later migration tools may let the user explicitly import text into one canonical target, for example:

```text
Source: CurseForge/ReleaseNotes_EN.txt
Target: CHANGELOG.md
Release being prepared: 1.1.0
```

The user remains responsible for confirming that the source content belongs to the selected target and release.

## Markdown editor

Summary, Description and Changelog use the same Markdown document infrastructure as normal Markdown documentation.

The editor supports:

- source editing
- rendered preview
- split source/preview mode
- synchronized split scrolling
- Markdown formatting toolbar
- unsaved-change tracking
- explicit save and revert

Publishing views provide purpose-specific navigation while still reading and writing the canonical Markdown files.

## Studio publishing workspace

The Studio exposes Publishing as a project-specific workspace separate from the CurseForge integration.

The workspace:

- shows Summary, Description and Changelog independently
- shows whether each canonical file already exists
- explains the language policy for each target
- opens each target in the shared Markdown editor
- keeps a missing target virtual until the user explicitly saves it
- creates the canonical file below `Release/` on first save

CurseForge remains a platform integration. Publishing content remains usable even when a project is not connected to CurseForge.
