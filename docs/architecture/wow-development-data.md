# WoW development data

Pallando's WoW Addon Studio uses an external, public, machine-readable data source for WoW facts that are useful during addon development.

The Studio must not depend directly on undocumented Blizzard endpoints, scraped web pages or one specific community mirror. The external data repository is an abstraction boundary.

## Public repository

The planned public repository is:

`Pallando-wow/wow-development-data`

The Studio consumes a stable `catalog.json`. Upstream collection and verification can change without changing the Studio contract.

## Catalog contract

The first schema contains two concepts:

- client builds
- dataset descriptors

A client build contains a stable client id, display name, client version, build number, TOC Interface number and provenance.

A dataset descriptor points to a separate data file. This lets the repository grow without turning one catalog into a very large game database.

Example:

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-09-29T04:00:00Z",
  "clients": [
    {
      "id": "wow_forever",
      "name": "WoW Forever",
      "version": "1.60.1",
      "build": 70009,
      "interface": 16001,
      "source": {
        "type": "upstream",
        "provider": "Gethe/wow-ui-source",
        "reference": "forever",
        "observedAt": "2026-09-24T22:03:25Z"
      }
    }
  ],
  "datasets": []
}
```

## Dataset strategy

Large development datasets are stored separately and referenced from the catalog.

Possible dataset kinds include:

- APIs and events
- quests
- spells
- items
- NPCs
- maps and zones
- factions
- achievements
- professions
- pet families, abilities and trainer relationships

The goal is not to reproduce Wowhead. The repository stores development-relevant facts, identifiers, relationships and observations.

Large copyrighted source text and game assets are deliberately out of scope.

## Collector architecture

A future `PallandoDataCollector` WoW addon can observe data exposed through the normal addon API and write it to SavedVariables.

The intended pipeline is:

```text
WoW
 ↓
PallandoDataCollector
 ↓
SavedVariables
 ↓
Pallando's WoW Addon Studio
 ↓
validation / normalization
 ↓
wow-development-data
```

The collector must not collect player-identifying or social data such as character names, Battle.net identity, guild names, friend lists, whispers or chat logs.

## Provenance

Published data should retain provenance whenever practical.

Sources can include:

- upstream version mirrors
- collector observations
- manually verified corrections

The repository should record when a fact was observed and which client/build it belongs to.

## TOC integration

The current client build supplies the expected TOC `Interface` value.

TOC QA reports `TOC009` when a valid Interface value does not include the expected current Interface for the selected WoW client.

The Studio should offer an explicit fix later. It must not silently rewrite the TOC.
