# Skill marketplace and remote catalog

As of October 25, 2026, the marketplace implementation targets the TableCloth `develop` line. It reads a separate XML skill catalog from TableClothCatalog and manages the skills in TableCloth AI's dedicated Codex profile. This work does not change the published Preview release.

The marketplace combines skill discovery, source and license information, installation, updates, local skill management, and dedicated Node.js and Python runtimes. Catalog entries have no listing or review states. An uncommented entry is offered for installation.

This guide explains the window, catalog contract, installation and updates, local storage, and verification boundaries in that order. [한국어](SKILL_MARKETPLACE.ko.md)

## Marketplace access from AI settings

Open **Options > AI skills > Open skill marketplace**. The marketplace provides **Catalog**, **Installed skills**, and **Runtimes** tabs. Search narrows the skill lists, and the catalog also supports a category filter. Selecting a skill shows its source, author, license, source revision, and usage requirements.

The Options tab contains a short guide and the marketplace entry. In the AI window, **OpenAI account / settings > Dedicated skills > Manage installed AI skills** opens the installed-skill view directly. Closing that dialog refreshes the AI window's enabled-skill count and skill availability.

The first-message view shows three cards: finding a service, finding a public service, and finding a tax filing site. A link for more features opens the marketplace's Catalog tab to discover and install skills. New profiles start without preinstalled skills; existing installations remain manageable.

Catalog loading and local management use separate operation states. A failed catalog request leaves installed-skill management available. The [window](../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml) and [view model](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.cs) own this behavior.

## A separate versioned XML catalog

The application reads `https://catalog.yourtablecloth.app/SkillCatalog.xml`. The source lives in [TableClothCatalog's catalog directory](https://github.com/yourtablecloth/TableClothCatalog/tree/main/docs), alongside `SkillCatalog.xsd` and its contributor guide. The catalog's existing Pages workflow publishes the files after a change reaches `main`.

`SkillCatalog Version="1"` contains `Skill` entries with stable IDs. Each entry identifies a GitHub repository, a path, and an immutable source commit in `Source.Ref`. Descriptions, authors, SPDX identifiers, license evidence, runtime and network requirements, install mode, and usage notes describe each skill. Commenting out or removing a `Skill` block withdraws it from the remote list. Installed copies remain manageable. See the [shared model](../src/TableCloth.ManagedAi.Core/SkillCatalog.cs).

The catalog lists seven skills: `official-report-skillset`, `bok-ecos-stats`, `kosis-stats`, `korean-law-mcp`, `korean-jangbu-for`, and the optional [tablecloth-certificate-expiry](https://github.com/yourtablecloth/tablecloth-certificate-expiry) and [tablecloth-windows-sandbox](https://github.com/yourtablecloth/tablecloth-windows-sandbox). Other candidates remain in XML comments. The two TableCloth skills are distributed in separate repositories, require no API keys, and interpret local reports supplied by TableCloth's host bridge after installation and activation. BOK sample-key requests return at most 10 rows. Keyless KOSIS and law queries depend on external shared servers, while bookkeeping requires separate MCP and child-skill configuration. API-key settings and DPAPI credential management are tracked for the next version in [issue #321](https://github.com/yourtablecloth/TableCloth/issues/321).

## Installation and changes to the catalog revision

Selecting an uninstalled skill enables installation without review approval. Installation rechecks the current catalog by skill ID. The source must identify an installable upstream skill at a fixed commit, and its license source must point to a file in the same repository at that commit. Technical validation still rejects invalid metadata or contents. Legacy review attributes from the earlier catalog are accepted for transition and do not grant or deny installation.

The client fetches selected files through GitHub REST APIs without a Git checkout. It validates paths and repository entries, bounds requests and downloaded data, rejects links and submodules, and stages files before committing an installation. It preserves the pinned license file as `LICENSE.upstream`. XML parsing disables external entities and DTDs. The [OpenAI backend](../src/TableCloth.ManagedAi.OpenAi/Skills) implements these checks.

Marketplace installations retain their catalog ID, repository, path, and source commit. When the same source has a different `Source.Ref`, both views offer an update to that catalog revision. Commit hashes identify revisions but do not establish chronological order. Updating stages and validates the replacement before changing the installed copy. Failed or cancelled validation preserves the existing files, recorded revision, and enabled setting. A local import with the same ID is not replaced by a marketplace update.

Some skills need API keys, OAuth credentials, an MCP connection, or additional packages. Catalog notes describe those dependencies. Installing skill files does not supply credentials or enable command execution. The Runtimes tab manages the dedicated Node.js and Python installations described below. Sources without an installable `SKILL.md` stay commented out until an adapter is available.

## Local skills and dedicated runtimes

The installed view supports folder import, updates to the catalog revision, enable and disable actions, removal with confirmation, refresh, and opening skill folders. It displays the installed source revision for marketplace installations. Enabled-state changes apply to subsequent AI operations. Removing a skill removes its dedicated folder and source metadata. Removal is available even when the catalog no longer contains the skill or cannot be reached.

A malformed `SKILL.md` remains visible with an invalid-manifest state. It can be disabled, inspected, or removed while other skills remain manageable. Enabling that entry is blocked. AI execution still rejects invalid managed manifests until they are repaired or removed.

Skills remain under `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`. TableCloth's existing importer and operation lock continue to control mutations. The marketplace manages this profile; the user's global Codex profile and skills remain outside its scope. See [the skill manager](../src/TableCloth.ManagedAi.OpenAi/Skills/OpenAiCodexSkillManager.cs) and [managed paths](../src/TableCloth.ManagedAi.Core/ManagedAiPaths.cs).

The **Runtimes** tab displays the installed Node.js and Python versions and provides **Install or update runtimes** and **Check installation and environment**. Codex installation also prepares this runtime pair. The application pins Node.js 24.22.1 with npm and npx 11.19.1, and Python 3.14.9, for Windows x64 and ARM64. It downloads official ZIP packages from [Node.js](https://nodejs.org/en/download/archive/v24.22.1) and [Python](https://www.python.org/downloads/release/python-3149/) using the versions and SHA-256 digests in the [runtime manifest](../src/TableCloth.ManagedAi.OpenAi/Distribution/SkillRuntimeManifest.cs).

Runtime files reside under `%LOCALAPPDATA%\TableCloth\ManagedAi\runtimes\skill-tools\releases\<installation-id>`, with separate `node` and `python` directories. The installer checks archive safety, digests, executable versions, and installation receipts before atomically selecting the complete pair. A failed or cancelled replacement preserves the previous active pair. Private npm caches, configuration, temporary files, and the child-process home reside under `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\skill-tools`. Only child processes receive the dedicated executable paths; TableCloth does not modify the global `PATH`, registry, or existing Node.js and Python installations.

Python uses the [embeddable distribution](https://docs.python.org/3.14/using/windows.html#the-embeddable-package) with its default `python314._pth` isolation. This installation supports standard-library scripts and does not provide pip, ensurepip, venv, or third-party Python package management. The installer supplies a byte-identical `python3.exe` alias for scripts that use that command name. Additional dependencies remain outside this installation's scope.

## Long-path checks without changing Windows policy

**Check installation and environment** reads the Windows long-path policy without changing it and checks .NET file I/O, Node.js, and Python separately. After a short-path baseline succeeds, each available runtime creates, reads, renames, rereads, and deletes a temporary file at paths longer than 330 characters under the application's dedicated diagnostics folder. The window distinguishes a passed check, a path-length restriction, an uninstalled runtime, and an unknown result. Access failures and other inconclusive results do not imply a path-length restriction. Limited or unknown results provide guidance without blocking installation, catalog access, or local skill management. See the [diagnostics contract](../src/TableCloth.ManagedAi.Core/SkillRuntimeDiagnostics.cs).

Windows policy changes remain under administrator or organization control. TableCloth does not modify the registry or policy. Modern .NET handles long paths implicitly, while Python's ordinary paths longer than 260 characters depend on Windows long-path support. A successful .NET extraction or runtime version check therefore does not establish equivalent support in child processes. Even a passed runtime I/O check cannot guarantee that every external tool or package accepts long paths. See the [Windows policy requirements](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation), [.NET path handling](https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats#skip-normalization), and [Python long-path support](https://docs.python.org/3.14/using/windows.html#removing-the-max-path-limitation).

## Verification and publication boundaries

Automated checks cover catalog parsing without review gates, download validation, revision tracking, replacement rollback, removal, and view-model actions. Runtime tests use synthetic ZIP packages and process responses to cover integrity, archive limits, isolation, and atomic pair replacement. Headless Avalonia checks exercise the actual window. These checks do not establish that the services work with user credentials or that an MCP connection is configured. See [skill tests](../src/TableCloth.ManagedAi.Test/SkillMarketplaceTests.cs), [runtime tests](../src/TableCloth.ManagedAi.Test/SkillRuntimeTests.cs), and [UI tests](../src/TableCloth.Test/SkillMarketplaceTests.cs).

The [CI probe](../tools/verify-skill-runtime-long-paths.ps1) checks both enabled and disabled Windows long-path policies in fresh processes on disposable GitHub-hosted Windows runners and restores the original policy in a `finally` block. It rejects local and self-hosted execution. The application's diagnostic service only reads the policy. [Diagnostic tests](../src/TableCloth.ManagedAi.Test/SkillRuntimeDiagnosticsTests.cs) also cover unknown results, permission failures, cancellation, and cleanup.

For k-dart, installing Node.js and Python does not establish a working DART integration. This change leaves the chat's command-execution policy and API-key handling unchanged. The [managed profile](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiProfile.cs), [invocation builder](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs), and [chat instructions](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiChatPromptFactory.cs) still constrain execution. Enabling the required commands and providing `API_K_DART` through a controlled credential path require separate implementation and verification.

The catalog and application have separate release paths. The catalog becomes available after TableClothCatalog publishes it. Users receive the window through a subsequent TableCloth build. An application build can manage installed skills while catalog publication is still pending.

## Catalog maintenance and installed revisions

Catalog maintainers publish an installable source by keeping its `Skill` block active. Changing the pinned source commit and matching license source offers that revision as an update. Commenting out the block withdraws the remote listing. See the [catalog contributor guide](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SkillCatalog.README.md).

Installed files remain under local control after withdrawal. Users can update when a different catalog revision is available or remove the skill independently of the catalog. Service credentials and runtime prerequisites remain specific to each skill.
