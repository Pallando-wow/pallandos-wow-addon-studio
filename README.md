# Pallando's WoW Addon Studio

Pallando's WoW Addon Studio is a desktop development environment exclusively for World of Warcraft addons targeting WoW Forever.

## Status

Early development. The repository is currently private.

## Supported scope

- WoW Forever
- Git, optionally per project
- CurseForge, optionally per project
- fixed Studio project structure
- addon and library projects
- reusable WoW-addon components

The long-term goal includes WoW-specific Lua, XML and TOC editing, localization tooling, QA, deployment, packaging and release management. The Studio is not intended to become a general-purpose IDE.

## Technology

- C# / .NET 10
- Avalonia UI
- MVVM
- xUnit
- Git / GitHub

## Solution structure

- `AddonStudio.App` – desktop UI and composition root
- `AddonStudio.Application` – application use cases and orchestration
- `AddonStudio.Core` – domain models and contracts
- `AddonStudio.Data` – local persistence
- `AddonStudio.Git` – Git integration
- `AddonStudio.Wow` – WoW Forever, TOC parsing and addon discovery
- `AddonStudio.Media` – logos and screenshots
- `AddonStudio.Packaging` – release validation and packaging
- `AddonStudio.Platforms` – CurseForge integration
- `AddonStudio.Tests` – automated tests

## License

Pallando's WoW Addon Studio is licensed under the GNU General Public License v3.0 or later (GPL-3.0-or-later).
