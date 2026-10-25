# Skill marketplace and remote catalog

As of October 25, 2026, the marketplace implementation targets the TableCloth `develop` line. It reads a separate XML skill catalog from TableClothCatalog and manages the skills in TableCloth AI's dedicated Codex profile. This work does not change the published Preview release.

The marketplace combines skill discovery, source and license information, installation, updates, and local skill management. Catalog entries have no listing or review states. An uncommented entry is offered for installation.

This guide explains the window, catalog contract, installation and updates, local storage, and verification boundaries in that order. [한국어](SKILL_MARKETPLACE.ko.md)

## Marketplace access from AI settings

Open **Options > AI skills > Open skill marketplace**. The marketplace provides two tabs: **Browse catalog** and **Installed skills**. Search narrows the skill lists, and the catalog also supports a category filter. Selecting a skill shows its source, author, license, source revision, and usage requirements.

The Options tab contains a short guide and the marketplace entry. In the AI window, **OpenAI account / settings > Dedicated skills > Manage installed AI skills** opens the installed-skill view directly. Closing that dialog refreshes the AI window's enabled-skill count and skill availability.

The first-message view shows three cards: finding a service, finding a public service, and finding a tax filing site. A link for more features opens the marketplace's Catalog tab to discover and install skills. New profiles start without preinstalled skills; existing installations remain manageable.

Catalog loading and local management use separate operation states. A failed catalog request leaves installed-skill management available. The [window](../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml) and [view model](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.cs) own this behavior.

## A separate versioned XML catalog

The application reads `https://catalog.yourtablecloth.app/SkillCatalog.xml`. The source lives in [TableClothCatalog's catalog directory](https://github.com/yourtablecloth/TableClothCatalog/tree/main/docs), alongside `SkillCatalog.xsd` and its contributor guide. The catalog's existing Pages workflow publishes the files after a change reaches `main`.

`SkillCatalog Version="1"` contains `Skill` entries with stable IDs. Each entry identifies a GitHub repository, a path, and an immutable source commit in `Source.Ref`. Descriptions, authors, SPDX identifiers, license evidence, runtime and network requirements, install mode, and usage notes describe each skill. Commenting out or removing a `Skill` block withdraws it from the remote list. Installed copies remain manageable. See the [shared model](../src/TableCloth.ManagedAi.Core/SkillCatalog.cs).

The active catalog lists three skills: `official-report-skillset`, [tablecloth-certificate-expiry](https://github.com/yourtablecloth/tablecloth-certificate-expiry), and [tablecloth-windows-sandbox](https://github.com/yourtablecloth/tablecloth-windows-sandbox). The report skill supplies drafting instructions and references. The other two interpret local reports supplied by TableCloth's host bridge after installation and activation without API keys. New profiles do not install these skills automatically.

`bok-ecos-stats`, `kosis-stats`, and `tablecloth-calculators` remain commented out because they require script execution. `korean-jangbu-for` and `korean-law-mcp` remain commented out because they require an MCP connection and further setup. Other previously deferred candidates also remain in comments. These entries preserve source information for later work; they are not offered for installation in the active catalog. The [skill eligibility policy](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SKILL_ELIGIBILITY.md) explains the current boundary. API-key settings and DPAPI credential management remain deferred to the next version in [issue #321](https://github.com/yourtablecloth/TableCloth/issues/321).

## Installation and changes to the catalog revision

Selecting an uninstalled skill enables installation without review approval. Installation rechecks the current catalog by skill ID. The source must identify an installable upstream skill at a fixed commit, and its license source must point to a file in the same repository at that commit. Technical validation still rejects invalid metadata or contents. Legacy review attributes from the earlier catalog are accepted for transition and do not grant or deny installation.

The client fetches selected files through GitHub REST APIs without a Git checkout. It validates paths and repository entries, bounds requests and downloaded data, rejects links and submodules, and stages files before committing an installation. It preserves the pinned license file as `LICENSE.upstream`. XML parsing disables external entities and DTDs. The [OpenAI backend](../src/TableCloth.ManagedAi.OpenAi/Skills) implements these checks.

Marketplace installations retain their catalog ID, repository, path, and source commit. When the same source has a different `Source.Ref`, both views offer an update to that catalog revision. Commit hashes identify revisions but do not establish chronological order. Updating stages and validates the replacement before changing the installed copy. Failed or cancelled validation preserves the existing files, recorded revision, and enabled setting. A local import with the same ID is not replaced by a marketplace update.

Some candidates need API keys, OAuth credentials, an MCP connection, additional packages, or script execution. Catalog notes describe those dependencies. Installing skill files does not supply credentials, configure an MCP server, or enable command execution. Candidates whose required execution path is unavailable stay commented out under the [skill eligibility policy](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SKILL_ELIGIBILITY.md).

## Local control of installed skills

The installed view supports folder import, updates to the catalog revision, enable and disable actions, removal with confirmation, refresh, and opening skill folders. It displays the installed source revision for marketplace installations. Enabled-state changes apply to subsequent AI operations. Removing a skill removes its dedicated folder and source metadata. Removal is available even when the catalog no longer contains the skill or cannot be reached.

A malformed `SKILL.md` remains visible with an invalid-manifest state. It can be disabled, inspected, or removed while other skills remain manageable. Enabling that entry is blocked. AI execution still rejects invalid managed manifests until they are repaired or removed.

Skills remain under `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`. TableCloth's existing importer and operation lock continue to control mutations. The marketplace manages this profile; the user's global Codex profile and skills remain outside its scope. See [the skill manager](../src/TableCloth.ManagedAi.OpenAi/Skills/OpenAiCodexSkillManager.cs) and [managed paths](../src/TableCloth.ManagedAi.Core/ManagedAiPaths.cs).

## Native Codex and host-bridge execution boundaries

AI setup installs the native Codex CLI used for chat and search. It does not install Node.js or Python. The marketplace has no language-runtime installation, version, or diagnostic controls. The [Codex installer](../src/TableCloth.ManagedAi.OpenAi/Distribution/OpenAiCodexPackageInstaller.cs) remains separate from the skill-file installer.

The [managed profile](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiProfile.cs) keeps `shell_tool=false` and `unified_exec=false`. The [invocation builder](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs) uses a `read-only` sandbox and `never` approval policy. The [chat instructions](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs) prohibit command execution. Skill installation does not change these settings. Read-only execution does not establish complete read isolation or network confinement.

The certificate-expiry and Windows Sandbox integrations use application-owned host bridges. The [AI window](../src/TableCloth.App/ManagedAi/ManagedAiWindow.cs) recognizes a supported user request and runs the bridge before sending its report to the model. The certificate bridge asks for consent and sends expiry counts only. The Sandbox bridge supports `list`, `start`, `stop`, `connect`, and `ip`; stopping a session requires confirmation. The model explains the supplied report instead of invoking a shell. Neither integration requires Node.js or Python. `Network="none"` describes the skill's own data-access requirements; Codex chat still uses an online connection.

## Verification and publication boundaries

Automated checks cover catalog parsing without review gates, download validation, revision tracking, replacement rollback, removal, and view-model actions. Backend fixtures use synthetic responses to exercise failure and recovery paths. Headless Avalonia checks exercise the two marketplace tabs across languages, themes, and window sizes. See [skill tests](../src/TableCloth.ManagedAi.Test/SkillMarketplaceTests.cs) and [UI tests](../src/TableCloth.Test/SkillMarketplaceTests.cs).

Fixture results, downloads from real repositories, and native Codex skill discovery establish different properties. Fixtures check the implemented contracts. Real downloads and discovery can confirm installed files, metadata, and enabled state. Neither establishes that the model invoked a script or that an external service, API credential, or MCP connection works. Actual service and model-tool execution remain unverified in this guide. The host bridges also depend on the local CLI and Windows capabilities; a skill listing alone does not establish successful host execution.

The catalog and application have separate release paths. The catalog becomes available after TableClothCatalog publishes it. Users receive the window through a subsequent TableCloth build. An application build can manage installed skills while catalog publication is still pending.

## Catalog maintenance and installed revisions

Catalog maintainers publish an installable source by keeping its `Skill` block active. Changing the pinned source commit and matching license source offers that revision as an update. Commenting out the block withdraws the remote listing. See the [catalog contributor guide](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SkillCatalog.README.md).

Installed files remain under local control after withdrawal. Users can update when a different catalog revision is available or remove the skill independently of the catalog. Keeping an installed copy does not supply missing execution permissions, credentials, or MCP configuration.
