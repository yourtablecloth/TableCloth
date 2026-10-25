# Fixed skill execution broker

This original, BCL-only evaluation implementation exposes reviewed local scripts through a bounded stdio MCP server. It does not change TableCloth's production profile, shell features, global executable paths, or authentication. The native `AppContainerProcessRunner` supplies the isolation boundary; MCP supplies only the communication protocol.

## Trusted registration file

The caller supplies an absolute manifest path inside an existing, non-root `ownedRoot` directory. Executable and script paths may be relative to that directory or absolute paths inside it. The broker rejects paths outside the directory, UNC paths, alternate data streams, reparse ancestors, unknown manifest fields, duplicate JSON keys, and invalid hashes. The manifest is limited to 128 KiB and 16 skills. Each registered interpreter and script has a SHA-256 digest; the broker checks those files during loading and again before execution. It holds read-only file handles until the worker finishes to deny changes to those files on Windows. The caller remains responsible for the trusted manifest and the remaining runtime dependency files.

The following shape uses placeholder digests that the preparer replaces with actual SHA-256 values:

```json
{
  "ownedRoot": "D:\\Projects\\TableCloth\\Artifacts\\appcontainer-skill-poc",
  "pythonExecutable": "runtime/python/python.exe",
  "pythonSha256": "<64 hexadecimal characters>",
  "nodeExecutable": "runtime/node/node.exe",
  "nodeSha256": "<64 hexadecimal characters>",
  "skills": [
    {
      "skillId": "tablecloth-calculators",
      "kind": "python-json-stdin",
      "scriptPath": "skills/tablecloth-calculators/scripts/calculate.py",
      "sha256": "<64 hexadecimal characters>",
      "allowedOperations": ["percent.part", "discount.single", "loan", "date.add", "time.convert", "bmi"],
      "requiresNetwork": false
    }
  ]
}
```

`pythonExecutable` and `pythonSha256` are required. Node fields may both be omitted or null when no Node fixture is registered. `allowedOperations`, `fixedArguments`, and `requiresNetwork` are optional; network defaults to denied. Python calculator registrations default to the original calculator's 22 known operations when the list is omitted. The broker additionally rejects missing or unknown calculator fields before invoking the worker. The original calculator remains responsible for arithmetic validation and result rounding.

## Fixed execution kinds

| Kind | Tool input | Fixed worker invocation |
| --- | --- | --- |
| `python-json-stdin` | Calculator JSON object with an allowed `operation` | Registered Python, `-I -S -B -X utf8`, registered script, JSON on stdin |
| `python-cli-fixed` | Empty `{}` only | Registered Python, `-I -S -B -X utf8`, registered script, manifest `fixedArguments` |
| `node-json-stdin` | Finite JSON numbers `a` and `b` only | Registered Node, `--preserve-symlinks-main`, registered addition fixture, JSON on stdin |

The model cannot select an executable, script path, expression, interpreter flags, working directory, or CLI arguments. Fixed CLI arguments belong to the trusted registration file. A reviewed KOSIS dry-run can register `search --query 인구 --result-count 1 --json --dry-run`. An actual BOK query registers `requiresNetwork=true` and remains blocked here. No network flag supplied in tool input can override that decision.

Python's isolated mode ignores `PYTHONUTF8` from the environment. The broker therefore fixes `-X utf8` on the interpreter command line for every Python kind so Korean stdin and output use the same UTF-8 encoding as the transport.

The fixed Node option avoids main-file realpath traversal of volume ancestors outside the granted directories. It does not relax registration path checks: reparse paths remain rejected and the registered executable and script hashes remain mandatory. The caller cannot add, remove, or override the option through tool input.

Every request uses a new private work directory under `ownedRoot/broker-work`. The native runner receives read-only directories for the interpreter and script, one read/write work directory, `AllowNetwork=false`, a 15-second timeout, a 256 MiB memory limit, a 64 KiB combined output limit, and a one-process limit. After the awaited worker finishes or cancellation cleanup completes, the broker removes only its owned work directory and refuses reparse-based cleanup. A failed cleanup returns an error instead of a successful result.

Internet-capability experiments performed directly by the evaluation harness are separate from this broker. Neither a successful dry-run nor an unrestricted internet-capability experiment establishes a domain allowlist, a safe credential integration, or production eligibility.

## MCP transport and results

`SkillMcpBroker.RunAsync(manifestPath, token)` uses only JSON-RPC on stdout. Fixed, content-free failure messages go to stderr. It supports `initialize`, `notifications/initialized`, `ping`, `tools/list`, and `tools/call`, following the [MCP stdio transport](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports), [lifecycle](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle), and [tool result](https://modelcontextprotocol.io/specification/2025-06-18/server/tools) contracts. Notifications never execute a tool and never receive a response. Initialization negotiates supported protocol versions through `2025-06-18`.

Each tool has its fixed skill ID as its name. `tools/call.params.arguments` contains the operation input directly. For example, the calculator call uses the following request after initialization:

```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"tablecloth-calculators","arguments":{"operation":"discount.single","price":100,"rate_percent":20}}}
```

A tool result contains a JSON text block with `status`, `exitCode`, `stdout`, `stderr`, `wasAppContainer`, `timedOut`, `outputLimited`, and `failure`. Only a successful exit with AppContainer evidence and no timeout or output limit produces `status="completed"`; calculator results also require a valid JSON object with `ok=true`. Network policy and invalid-input results set `isError=true` without starting a worker. Process failures and bounded worker output remain distinguishable from JSON-RPC protocol errors.

Input lines are limited to 128 KiB, operation JSON to 64 KiB, response messages to 1 MiB, nesting to 32 levels, and a session to 1,024 input messages. Oversized lines and invalid UTF-8 produce a protocol error and terminate the transport without reading an unbounded remainder. Duplicate keys are rejected. Output text is JSON-escaped within the response, so a worker cannot inject a protocol message through stdout.

## Offline checks and native evidence

`BrokerSelfChecks.RunAsync(ownedRoot, token)` returns named fixture checks with `Passed` and a content-free `Failure`. It uses ordinary fixture files and a fake executor, never a native worker. Checks cover registration paths and hashes, argument and code injection, duplicate and oversized inputs, network denial, output bounds, required AppContainer evidence, JSON-RPC lifecycle and notifications, invalid UTF-8, cancellation, and work-directory cleanup.

`SkillExecutionRegistry.LoadAsync(manifestPath, executor, token)` injects the offline executor. `SkillMcpBroker.RunAsync(registry, input, output, token)` injects transport streams. These seams are not exposed through the manifest or MCP protocol. Their passing results do not establish actual AppContainer execution, operating-system denial, a model's tool selection, or external service access. The evaluation harness performs those checks separately with the real native runner.
