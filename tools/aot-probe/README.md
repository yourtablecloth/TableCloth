# AOT 호환성 프로브 (이슈 #296)

WPF → Avalonia + Native AOT 전환([docs/AVALONIA_AOT_MIGRATION.md](../../docs/AVALONIA_AOT_MIGRATION.md))에 앞서,
릴리스에 핵심적인 비-UI 의존성들이 **Native AOT에서 실제로 동작하는지** 컴파일·실행으로 확인하는 최소 프로브다.
솔루션(`TableCloth.slnx`)에 포함되지 않는 독립 프로젝트이므로 본 빌드에 영향을 주지 않는다.

## 대상 의존성

`Directory.Packages.props`의 v1.20.6 핀 버전 기준:

- `System.Management` 10.0.1 (WMI — `AppStartup`/`Win32DiskDrive`에서 사용)
- `Velopack` 0.0.1298 (업데이트 — `AppUpdateManager`)
- `Sentry` 6.0.0 (오류 보고)
- `System.Text.Json` (소스젠 경로 vs 리플렉션 경로 대비)

## 실행

Native AOT 링크에는 MSVC C++ 툴체인이 필요하다. `vswhere.exe`가 PATH에 있어야 한다(Developer Command Prompt
사용 권장).

```powershell
# Developer PowerShell for VS, 또는:
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH
dotnet publish -c Release -r win-x64 -p:PublishAot=true
& .\bin\Release\net10.0-windows\win-x64\publish\aot-probe.exe
```

## 실측 결과 (2026-07-24, .NET 10.0.302 / ILCompiler 10.0.10)

산출 네이티브 exe = **9.7 MB** (콘솔, UI 없음).

| 의존성 | ILC 분석 | Native AOT 런타임 | 판정 |
| ------ | -------- | ----------------- | ---- |
| System.Management (WMI) | `IL3052: COM interop is not supported with full AOT`, `IL2067`, `IL2077`. ILC "will always throw" | `TypeInitializationException` | ❌ 하드 blocker → 대체 필수 |
| System.Text.Json (리플렉션) | `IL2026`, `IL3050` | `InvalidOperationException: Reflection-based serialization has been disabled` | ⚠️ 소스젠 전환 필수 |
| System.Text.Json (소스젠) | 경고 없음 | ✅ 정상 | ✅ |
| Velopack 0.0.1298 | 경고 없음 | 정상 (런타임 예외는 `VelopackApp.Build()` 미호출 탓 — 앱은 이미 호출) | ✅ AOT 호환 |
| Sentry 6.0.0 | 경고 없음 | ✅ `SentrySdk.Init completed` | ✅ AOT 호환 |
| CPUID 대체(WMI 하이퍼바이저) | 경고 없음 | ✅ `hypervisor-present bit (ECX[31]) = True` | ✅ WMI 대체 확정 |

`X86Base.CpuId(1, 0)`의 ECX 비트 31은 `Win32_ComputerSystem.HyperVisorPresent`와 동일한 신호이며 순수 인트린식이라
AOT-safe다(IL 경고 0). `AppStartup.cs`의 유일한 실사용 WMI 쿼리를 이것으로 대체한다. 디스크 지문 WMI 클래스
(`Win32DiskDrive`/`Win32DiskPartition`)는 참조 0건의 죽은 코드라 삭제한다.

**결론:** 하드 blocker는 `System.Management` 하나뿐이며, 실사용은 하이퍼바이저 감지 1곳뿐이라 CPUID로 대체 확정.
JSON은 기계적 소스젠 이관, Velopack/Sentry는 무변경.
자세한 대응은 [docs/AVALONIA_AOT_MIGRATION.md §6](../../docs/AVALONIA_AOT_MIGRATION.md#6-stage-2--native-aot-적용) 참고.
