# WoW TOC metadata

The Studio treats the runtime addon's TOC as the authoritative source for WoW runtime metadata such as Version and Interface.

## Reader foundation

The TOC reader parses a document into ordered lines while preserving the original raw text.

Each line is classified as:

- blank
- metadata
- comment
- runtime file

Metadata uses the normal WoW TOC form:

```text
## Key: Value
```

The parsed document exposes common fields such as Title, Notes, Version, Interface, dependencies and SavedVariables while also keeping all metadata keys available through a case-insensitive lookup.

Comma-separated metadata values are exposed as trimmed lists.

If a metadata key occurs more than once, the last value is the effective value. The original ordered lines remain available so a future TOC editor can still display or rewrite the document without losing source context.

Malformed lines beginning with `##` are preserved as comments rather than silently interpreted as metadata.

## Runtime files

Non-empty, non-comment and non-metadata lines are treated as runtime file entries. Their trimmed path is exposed through the document model while the original source line remains preserved.

## Future use

This reader is the foundation for:

- project overview metadata
- authoritative version lookup
- TOC editor
- Interface validation
- dependency inspection
- SavedVariables inspection
- diagnostics and QA checks


## Writer foundation

The TOC writer updates metadata without rebuilding the complete file from scratch.

It:

- preserves unrelated metadata, comments, blank lines and runtime file entries
- keeps the existing metadata key spelling when updating a value
- removes duplicate definitions of a key when that key is explicitly updated
- removes all definitions when metadata is explicitly deleted
- inserts new metadata into the existing metadata block before runtime files
- preserves the file's existing LF or CRLF line ending style
- preserves whether the file ended with a trailing newline
- writes UTF-8 without a BOM

Metadata keys cannot contain a colon or line break. Metadata values cannot contain line breaks.

## TOC QA diagnostics

The TOC QA analyzer currently reports:

| Code | Severity | Meaning |
| --- | --- | --- |
| TOC001 | Error | Required `Interface` metadata is missing |
| TOC002 | Error | `Interface` is not one or more positive numeric values |
| TOC003 | Warning | `Title` metadata is missing |
| TOC004 | Warning | `Version` metadata is missing |
| TOC005 | Warning | A metadata key is defined more than once |
| TOC006 | Warning | A runtime file is listed more than once |
| TOC007 | Error | A runtime file path is rooted or escapes the addon directory |
| TOC008 | Error | A referenced runtime file does not exist |

Runtime file checks are performed relative to the runtime addon's directory. Both slash styles are accepted as TOC path separators for validation, while rooted and parent-escaping paths are rejected.

The diagnostics are deliberately independent from a specific WoW Forever Interface build number. Matching the project's configured target build will be a separate validation rule once target-version metadata is wired into the Studio.
