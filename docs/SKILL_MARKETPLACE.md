# Skill marketplace and contained script execution

As of October 27, 2026, this guide describes the TableCloth development implementation. The marketplace reads TableClothCatalog's XML catalog and manages skills in TableCloth AI's dedicated Codex profile. Preview 8 connected only the calculator script to AppContainer; its calculator check did not establish support for other installed scripts.

The development implementation discovers packaged Python and Node.js entry points across enabled installed skills and exposes them through an application-owned MCP broker. Instruction-only skills and the existing certificate and Windows Sandbox host bridges retain their own paths. Additional prompt references come from anonymously accessible public GitHub files at the recorded commit; this context does not forward local document bodies. Installing or enabling a skill does not establish that the model used it in a response.

This guide covers marketplace access, the catalog contract, installation, isolation, runtime diagnostics, verification and catalog maintenance. [한국어](SKILL_MARKETPLACE.ko.md)

## Marketplace access and operation progress

Open **Options > AI skills > Open skill marketplace**. The window provides **Browse catalog**, **Installed skills** and **Runtimes** tabs. Search narrows both skill lists; catalog browsing also supports a category filter. Selecting a skill shows its source, author, license, source revision and requirements. See the [window](../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml) and [view model](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.cs).

The Options page contains a short guide and the marketplace entry. In the AI window, **OpenAI account / settings > Dedicated skills > Manage installed AI skills** opens the installed-skill tab directly. Closing the dialog refreshes the enabled-skill count, which describes available instructions rather than completed script executions.

The first-message view offers three cards for finding a service, a public service and a tax filing site. Its additional-features link opens the catalog tab. New profiles have no preinstalled skills; existing installations remain manageable.

Catalog loading, installed-skill operations and runtime checks maintain separate progress states. The activity panel identifies installation, updates and removal while they run. A failed catalog request leaves installed-skill management available; loading does not display an empty-result message prematurely.

## A versioned catalog with distinct skill paths

The application reads `https://catalog.yourtablecloth.app/SkillCatalog.xml`. Its source, schema and contributor guide live in [TableClothCatalog's catalog directory](https://github.com/yourtablecloth/TableClothCatalog/tree/main/docs). The Pages workflow publishes changes from `main`. `SkillCatalog Version="1"` records stable IDs, immutable GitHub source commits, license evidence, runtime and network requirements, and usage notes. Entries have no review or listing state; commenting out a `Skill` block withdraws it. See the [catalog model](../src/TableCloth.ManagedAi.Core/SkillCatalog.cs).

The checked-out catalog contains six active entries with different integration paths:

| Skill | Product path | Dependencies and boundary |
| --- | --- | --- |
| `bok-ecos-stats` | Packaged Python helper through MCP and AppContainer | Public/sample ECOS requests through a bounded host HTTPS proxy |
| `kosis-stats` | Packaged Python helper through MCP and AppContainer | Upstream public proxy reached through a bounded host HTTPS proxy |
| `tablecloth-calculators` | Packaged Python script through MCP and AppContainer | User inputs and Python standard library; network disabled |
| `official-report-skillset` | Instructions and installed references | Drafting from supplied material; no script required |
| `tablecloth-certificate-expiry` | Existing application-owned certificate report bridge | Consent and expiry counts; no Python or Node.js worker |
| `tablecloth-windows-sandbox` | Existing application-owned Windows Sandbox bridge | Compatible Windows Sandbox; no Python or Node.js worker |

This table describes routing and requirements rather than test outcomes or guaranteed upstream availability. Other candidates remain commented out. API-key settings and DPAPI credential protection remain tracked in [issue #321](https://github.com/yourtablecloth/TableCloth/issues/321). The [eligibility policy](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SKILL_ELIGIBILITY.md) and [original PoC report](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/APP_CONTAINER_POC.md) describe catalog decisions and the earlier evaluation scope separately.

## Local installation, updates and enabled state

An uninstalled entry can be installed without review approval. The client re-fetches the entry by ID and validates its fixed source commit, matching license evidence and contents. It uses GitHub REST APIs without a Git checkout, bounds requests and files, rejects links and submodules, stages downloads before installation and preserves the license as `LICENSE.upstream`. XML parsing rejects external entities and DTDs. See the [installer](../src/TableCloth.ManagedAi.OpenAi/Skills/GithubSkillInstaller.cs).

Marketplace installations record the catalog ID, repository, source path and commit. A different commit from the same source offers an update in both views. Updating validates the replacement and retains its enabled setting; failed or cancelled replacement preserves the previous installation. A local import with the same ID is not overwritten by a catalog update.

The installed tab supports folder import, updates, enable and disable actions, removal with confirmation, refresh and opening folders. The enabled label controls whether the next chat receives the skill's instructions; it does not report execution. Invalid `SKILL.md` entries remain visible for inspection, disabling or removal and cannot be enabled. See the [skill manager](../src/TableCloth.ManagedAi.OpenAi/Skills/OpenAiCodexSkillManager.cs).

Skills reside under `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`. Chats use separate `%LOCALAPPDATA%\TableCloth\ManagedAi\runs\<GUID>` directories. These different locations are expected: the application discovers installed entry points and registers them independently of the chat working directory. The global Codex profile remains outside marketplace management.

## MCP routing and the AppContainer boundary

The [production bridge](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs) discovers packaged `.py`, `.js`, `.mjs` and `.cjs` entry points across enabled, valid installed skills. An invocation-specific manifest records runtime and package identities. The application-owned [broker](../src/TableCloth.ManagedAi.AppContainer/Broker/SkillMcpBroker.cs) accepts up to 128 registered entry points and invokes Python or Node.js directly with an argument array and optional stdin, without a command shell. The [invocation builder](../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs) registers this server and describes the installed script route in place of an upstream `npx` instruction loader.

Each worker runs in a native Windows AppContainer with a separate scratch working directory. The package and verified runtime files receive read-only access; their hash-checked handles remain locked during execution. The worker does not inherit host credentials, API-key environment variables or arbitrary host paths. The [runner and registry](../src/TableCloth.ManagedAi.AppContainer) enforce integrity, path, process and resource limits. Ordinary AppContainer can still access resources already granted to `ALL APPLICATION PACKAGES`; it does not provide a virtual-machine boundary.

Workers always run with `AllowNetwork=false` and receive no `internetClient` capability. BOK and KOSIS use a bounded host HTTPS proxy that checks the allowed destinations for those public-service requests. A catalog declaration of `Network="required"` does not grant arbitrary network access, add another destination or supply credentials. Local imports and other scripts do not gain internet access merely by mentioning it in `SKILL.md`.

The [dedicated profile](../src/TableCloth.ManagedAi.Windows/WindowsManagedAiProfile.cs) keeps `shell_tool=false` and `unified_exec=false`; Codex retains a `read-only` sandbox and `never` approval policy. Packaged scripts use the application-owned MCP path. TableCloth does not install arbitrary dependencies with `pip`, `npm` or `npx`, configure unrelated upstream MCP servers or supply unsupported native dependencies.

For catalog-installed skills, the [public-document context](../src/TableCloth.ManagedAi.OpenAi/Execution/InstalledSkillPromptContext.cs) fetches `SKILL.md`, `instruction.md` and at most four linked `references/*.md` files from `raw.githubusercontent.com` at the recorded commit. Requests use no credentials and do not follow redirects. The in-memory session cache uses the public source URL. Documents are limited to 32 KiB each and 128 KiB of JSON context in total; unavailable or oversized files are marked as omitted, and truncated context is identified. Local imports contribute IDs only to this context; no installed document body is read or forwarded by this path. Public instructions replace an `npx` loader without running it.

Certificate-expiry and Windows Sandbox requests use [existing host bridges](../src/TableCloth.App/ManagedAi/ManagedAiWindow.cs). The certificate bridge obtains consent and sends expiry counts only. The Sandbox bridge supports `list`, `start`, `stop`, `connect` and `ip`, with confirmation before stopping a session. These host actions do not run inside the skill AppContainer. `Network="none"` describes the skill's data requirements; Codex chat itself remains online.

## Dedicated runtimes and long-path diagnostics

The **Runtimes** tab shows installed Python and Node.js versions and provides **Install or repair runtimes** and **Check installation and environment**. These dedicated runtimes remain separate from the native Codex CLI. Their installation does not change system interpreters, the system PATH or Windows policy. See the [runtime manager](../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs) and [runtime view model](../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.Runtimes.cs).

Long-path checks read and write files at paths longer than 260 characters inside AppContainer for each interpreter. The UI reports **Passed**, **Limited** or **Unverified** separately for Python and Node.js. Failed or unverified results display a possible-feature-limitation notice; a failed refresh clears any earlier passing result. Additional libraries can impose their own limits even when the interpreter check passes.

Runtime installation and script discovery establish prerequisites. Incompatible code, missing bundled dependencies, service outages, authentication requirements and input validation can still prevent execution. Runtime versions and enabled-skill counts do not establish that a script ran.

## Verification evidence and publication scope

The [product skill smoke tool](../tools/managed-skill-smoke/README.md) reproduces production DI, all 22 calculator operations, actual Codex MCP events and separately identified synthetic host-report cases.

Automated tests cover catalog parsing, download validation, revisions, rollback, removal and view-model actions. Headless Avalonia checks cover languages, themes, window sizes, progress and runtime warnings. Fixtures check their contracts and do not establish live service availability or actual model tool selection. See the [backend tests](../src/TableCloth.ManagedAi.Test) and [marketplace UI tests](../src/TableCloth.Test/SkillMarketplaceTests.cs).

A script check records the raw MCP call and result, selected installed entry point, runtime and package hashes, and exit status. An [independent Win32 observer](../tools/managed-skill-smoke/ProcessEvidenceObserver.cs) also measures descendant process ancestry, actual image paths and hashes, creation times and kernel `TokenIsAppContainer` values during execution. The verifier correlates these measurements with the MCP records. A model's skill attribution, a saved summary flag or the broker's `wasAppContainer` field alone does not establish contained execution. Direct broker checks and real Codex conversations exercise different layers.

The tool's read-only `--verify <evidence-directory> --expected-product-sha256 <trusted-product-sha256>` mode reconstructs its assessment from the saved raw records without starting the product or requesting a model response. Obtain the expected hash from an independently trusted product payload, not the bundle itself. Script bundles without independent OS observations fail verification, including older bundles that contain successful summaries. This check establishes consistency against the supplied identity and measurements; it does not cryptographically attest files supplied by a malicious producer. The [harness instructions](../tools/managed-skill-smoke/README.md#read-only-verification-with-an-independently-trusted-product-hash) describe the command and retained evidence.

The six catalog entries also need different evidence: instructions and supplied material for the report skill, host results for the two local bridges, and contained script results for BOK, KOSIS and the calculator. The harness's report-writing response and synthetic certificate/Sandbox reports do not prove actual host actions. Its [Python concurrency and PowerShell service-result fixtures](../tools/managed-skill-smoke/README.md#native-product-host-and-independent-broker-fixtures) check transport and response-validation contracts separately from live upstream availability; the legacy concurrency control deliberately expects a contained failure.

The [original PoC results](../tools/appcontainer-skill-poc/RESULTS.md) retain their measured environment and qualification limits. They do not prove that a later packaged application registered every script or that Windows 10 22H2 and ARM64 devices behave identically. Development changes, catalog publication and application releases follow separate paths. This guide alone does not declare a build released or a test passed.

## Catalog changes and retained installations

Maintainers offer a skill by keeping its `Skill` block active. Changing the pinned commit and matching license path offers that revision as an update. Commenting out the block withdraws its listing while installed copies remain under local control. See the [catalog contributor guide](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/SkillCatalog.README.md).

An installed skill remains manageable after withdrawal or a catalog outage. Its files retain their recorded dependencies and execution limits. Retaining an installation does not supply new credentials, packages or upstream MCP connections; those requirements remain subject to the application's supported integration paths.
