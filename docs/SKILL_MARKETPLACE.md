# Skill marketplace and remote catalog

As of October 25, 2026, the marketplace implementation targets the TableCloth `develop` line. It reads a separate XML skill catalog from TableClothCatalog and manages the skills in TableCloth AI's dedicated Codex profile. This work does not change the published Preview release.

The marketplace combines candidate discovery, source and license information, installation review, and local skill management. The initial catalog lists twelve candidates. Their installation reviews remain pending.

This guide explains the window, catalog contract, installation policy, local storage, and verification boundaries in that order. [한국어](SKILL_MARKETPLACE.ko.md)

## Marketplace access from AI settings

Open **Options > AI skills > Open skill marketplace**. The marketplace provides separate catalog and installed-skill views. Search narrows the lists, and the catalog also supports a category filter. Selecting a candidate shows its source, author, license, review state, and adoption notes.

Catalog loading and local management use separate operation states. A failed catalog request leaves installed-skill management available. The [window](../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml) and [view model](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.cs) own this behavior.

## A separate versioned XML catalog

The application reads `https://catalog.yourtablecloth.app/SkillCatalog.xml`. The source lives in [TableClothCatalog's catalog directory](https://github.com/yourtablecloth/TableClothCatalog/tree/main/docs), alongside `SkillCatalog.xsd` and its contributor guide. The catalog's existing Pages workflow publishes the files after a change reaches `main`.

`SkillCatalog Version="1"` contains `Skill` entries with stable IDs. Each entry identifies a GitHub repository, a path, and an immutable source commit. Descriptions, authors, SPDX identifiers, license evidence, runtime and network requirements, install mode, review states, and adoption notes describe each candidate. Catalog listing does not grant installation approval. See the [shared model](../src/TableCloth.ManagedAi.Core/SkillCatalog.cs).

## Review gates before remote installation

Remote installation accepts only listed upstream skills with a verified license review, an approved installation review, and a fixed source commit. The license source also points to a file in that repository at the same commit. Pending or rejected reviews, unlisted entries, and MCP adapter candidates cannot start a download. Installation rechecks the current catalog by skill ID.

The client fetches selected files through GitHub REST APIs without a Git checkout. It validates paths and repository entries, bounds requests and downloaded data, rejects links and submodules, and stages files before calling the existing skill importer. It preserves the pinned license file as `LICENSE.upstream`. XML parsing disables external entities and DTDs. The [OpenAI backend](../src/TableCloth.ManagedAi.OpenAi/Skills) implements these checks.

The initial candidates retain pending reviews. Some need API keys, OAuth credentials, an MCP adapter, or a Node.js CLI. The catalog notes record those dependencies. The marketplace does not configure those services or extend the dedicated runtime's execution permissions.

## Local management within the dedicated profile

The installed view supports folder import, enable and disable actions, removal with confirmation, refresh, and opening skill folders. Enabled-state changes apply to subsequent AI operations. Removing a skill removes its dedicated folder.

A malformed `SKILL.md` remains visible with an invalid-manifest state. It can be disabled, inspected, or removed while other skills remain manageable. Enabling that entry is blocked. AI execution still rejects invalid managed manifests until they are repaired or removed.

Skills remain under `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`. TableCloth's existing importer and operation lock continue to control mutations. The marketplace manages this profile; the user's global Codex profile and skills remain outside its scope. See [the skill manager](../src/TableCloth.ManagedAi.OpenAi/Skills/OpenAiCodexSkillManager.cs) and [managed paths](../src/TableCloth.ManagedAi.Core/ManagedAiPaths.cs).

## Verification and publication boundaries

Automated checks cover catalog parsing, review gates, download validation, and view-model actions. Headless Avalonia checks exercise the actual window. These checks use synthetic skills and HTTP responses. They do not establish that the candidate services work with user credentials or that an MCP adapter exists. See [backend tests](../src/TableCloth.ManagedAi.Test/SkillMarketplaceTests.cs) and [UI tests](../src/TableCloth.Test/SkillMarketplaceTests.cs).

The catalog and application have separate release paths. The catalog becomes available after TableClothCatalog publishes it. Users receive the window through a subsequent TableCloth build. An application build can manage installed skills while catalog publication is still pending.

## Candidate review and installation approval

The catalog records candidates and their adoption conditions. A maintainer can review a pinned revision and approve a compatible upstream skill without changing the application's candidate list.

Local management is available independently of candidate approval. Service credentials, sensitive document access, and MCP adaptation remain specific to each candidate. Approval can follow that candidate's documented conditions and runtime compatibility checks.
