using System.Reflection;
using TableCloth.Components.Implementations;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;

namespace TableCloth.Test
{
    /// <summary>
    /// 샌드박스 LogonCommand 로 실행되는 StartupScript.cmd 생성 결과에 대한 회귀 방지 테스트.
    /// 이 스크립트가 조용히 깨지면 Spork 가 아예 뜨지 않고 사용자에게 아무 메시지도 남지 않으므로
    /// (이슈 #304), 실측으로 확인된 함정들을 여기서 고정한다.
    /// </summary>
    [TestClass]
    public sealed class SandboxStartupScriptTests
    {
        /// <summary>
        /// <c>GenerateSandboxStartupScript</c> 는 private 인스턴스 메서드지만 주입된 의존성을
        /// 전혀 사용하지 않으므로, 생성자 인수는 null 로 두고 리플렉션으로 호출한다.
        /// </summary>
        private static string GenerateScript(TableClothConfiguration configuration)
        {
            var builder = new SandboxBuilder(null!, null!, null!, null!);
            var method = typeof(SandboxBuilder).GetMethod(
                "GenerateSandboxStartupScript",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "GenerateSandboxStartupScript 를 찾지 못했습니다. 이름이 바뀌었는지 확인하세요.");
            return (string)method.Invoke(builder, new object[] { configuration })!;
        }

        private static TableClothConfiguration DefaultConfiguration() => new();

        /// <summary>
        /// 스크립트는 UTF-8(Encoding.Default)로 기록되지만 cmd 는 이를 OEM 코드페이지로 읽는다.
        /// 비 ASCII 문자가 섞이면 바이트 정렬이 깨져 스크립트가 통째로 무동작이 될 수 있다
        /// (이슈 #304 진단 중 진단 하니스에서 실측한 현상).
        /// </summary>
        [TestMethod]
        public void StartupScript_ShouldBePureAscii()
        {
            var script = GenerateScript(DefaultConfiguration());

            var offending = script.Where(c => c > 0x7F).Distinct().ToArray();

            Assert.IsEmpty(offending,
                $"StartupScript 에 비 ASCII 문자가 있습니다: {string.Join(", ", offending.Select(c => $"U+{(int)c:X4}"))}");
        }

        /// <summary>
        /// citool.exe 는 작업을 마친 뒤 표준 입력을 기다리는 환경이 있다. stdin 을 nul 로 묶지 않으면
        /// batch 가 그 자리에서 멈추고 바로 다음 줄의 Spork 실행에 도달하지 못한다(이슈 #304).
        /// </summary>
        [TestMethod]
        public void StartupScript_CitoolRefresh_ShouldRedirectStdinFromNul()
        {
            var script = GenerateScript(DefaultConfiguration());

            StringAssert.Contains(script, "--refresh <nul",
                "citool --refresh 가 stdin 리다이렉트 없이 호출되면 프롬프트에서 무한 대기할 수 있습니다.");
        }

        /// <summary>
        /// <c>echo rc=%errorlevel%&gt;&gt;file</c> 처럼 숫자 바로 뒤에 리다이렉션이 붙으면 cmd 가
        /// <c>0&gt;&gt;</c> 를 fd 0 리다이렉션으로 파싱해 로그가 조용히 깨진다. 그래서 브레드크럼은
        /// 리다이렉션을 명령 앞에 둔다.
        /// </summary>
        [TestMethod]
        public void StartupScript_ShouldNotHaveDigitAdjacentRedirection()
        {
            var script = GenerateScript(DefaultConfiguration());

            foreach (var token in new[] { "%errorlevel%>>", "%errorlevel%>", "%TCSPORKRC%>>" })
            {
                Assert.IsFalse(script.Contains(token, StringComparison.Ordinal),
                    $"'{token}' 형태는 cmd 가 fd 리다이렉션으로 오인합니다. 리다이렉션을 명령 앞으로 옮기세요.");
            }
        }

        /// <summary>
        /// 부팅 브레드크럼이 없으면 Spork 가 뜨기 전에 실패했을 때 아무 단서도 남지 않는다.
        /// 기록 위치는 세션 종료 후에도 남는 Data 마운트가 우선이어야 한다.
        /// </summary>
        [TestMethod]
        public void StartupScript_ShouldWriteBootBreadcrumbsToPersistentMount()
        {
            var script = GenerateScript(DefaultConfiguration());

            StringAssert.Contains(script, "set TCBOOTLOG=");
            StringAssert.Contains(script, @"Desktop\Data\tablecloth-boot.log",
                "브레드크럼은 세션 종료 후 회수 가능한 Data 마운트에 남아야 합니다.");

            foreach (var stage in new[] { "[00]", "[01]", "[02]", "[03]", "[04]", "[05]", "[06]" })
            {
                StringAssert.Contains(script, stage, $"브레드크럼 단계 {stage} 가 없습니다.");
            }
        }

        /// <summary>
        /// LogonCommand 는 <c>.cmd</c> 경로를 직접 지정하지 않는다. <c>.cmd</c> 는 PE 이미지가 아니라
        /// 실행에 셸/파일 연결이 개입하는데, 그 경로가 깨지면 아무 오류 없이 그냥 실행되지 않는다
        /// (이슈 #304). System32 의 cmd.exe 를 절대 경로로 명시해 의존을 없앤다.
        /// </summary>
        [TestMethod]
        public void LogonCommand_ShouldInvokeScriptThroughAbsoluteCmdExePath()
        {
            // AssetsDirectoryPath 가 실재해야 BootstrapSandboxConfiguration 이 LogonCommand 를 채운다.
            var configuration = new TableClothConfiguration
            {
                AssetsDirectoryPath = Path.GetTempPath(),
            };

            var method = typeof(SandboxBuilder).GetMethod(
                "BootstrapSandboxConfiguration",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "BootstrapSandboxConfiguration 을 찾지 못했습니다.");

            var sandboxConfiguration = (SandboxConfiguration)method.Invoke(null, new object[] { configuration })!;

            Assert.HasCount(1, sandboxConfiguration.LogonCommand);

            var command = sandboxConfiguration.LogonCommand[0];
            StringAssert.StartsWith(command, @"C:\Windows\System32\cmd.exe /c ",
                "LogonCommand 는 게스트 System32 의 cmd.exe 를 절대 경로로 먼저 지정해야 합니다.");
            StringAssert.Contains(command, @"""C:\Users\WDAGUtilityAccount\Desktop\App\StartupScript.cmd""",
                "스크립트 경로는 따옴표로 감싸 전달해야 합니다.");
        }

        /// <summary>
        /// SAC 해제와 정책 적용은 반드시 Spork 실행 *전에* 끝나야 하고, Spork 실행은 스크립트의
        /// 마지막 동작이어야 한다. 순서가 뒤집히면 이슈 #256 회귀가 된다.
        /// </summary>
        [TestMethod]
        public void StartupScript_ShouldLaunchSporkAfterPolicyStages()
        {
            var script = GenerateScript(DefaultConfiguration());

            var sacIndex = script.IndexOf("VerifiedAndReputablePolicyState", StringComparison.Ordinal);
            var citoolIndex = script.IndexOf("citool.exe", StringComparison.Ordinal);
            var sporkIndex = script.IndexOf(" spork ", StringComparison.Ordinal);

            Assert.IsTrue(sacIndex >= 0 && citoolIndex >= 0 && sporkIndex >= 0,
                "SAC 정책 / citool / spork 실행 중 하나가 스크립트에 없습니다.");
            Assert.IsLessThan(citoolIndex, sacIndex, "SAC 레지스트리 설정이 citool refresh 보다 뒤에 있습니다.");
            Assert.IsLessThan(sporkIndex, citoolIndex, "citool refresh 가 Spork 실행보다 뒤에 있습니다.");
        }

        /// <summary>
        /// <c>tablecloth:https://…</c> 딥링크로 진입하면 대상 URL 이 Spork 로 전달되어야 한다.
        /// 값이 없으면 스위치 자체가 나오지 않아야 한다(카탈로그 대표 URL 을 여는 기존 동작 유지).
        /// </summary>
        [TestMethod]
        public void StartupScript_ShouldPassTargetUrlToSpork()
        {
            var withoutTarget = GenerateScript(DefaultConfiguration());
            Assert.IsFalse(withoutTarget.Contains("--target-url", StringComparison.Ordinal),
                "대상 URL 이 없는데 스위치가 붙었습니다.");

            var configuration = DefaultConfiguration();
            configuration.TargetUrl = "https://spib.wooribank.com/pib/Dream?withyou=CTCER0149&fromSite=pib";

            var script = GenerateScript(configuration);

            StringAssert.Contains(script, "--target-url \"https://spib.wooribank.com/pib/Dream?withyou=CTCER0149&fromSite=pib\"",
                "대상 URL 이 Spork 인자로 전달되지 않았습니다.");
        }

        /// <summary>
        /// cmd 는 큰따옴표 안에서도 <c>%</c> 를 확장한다. 퍼센트 인코딩된 URL 을 그대로 두면
        /// <c>%2</c>(인자 2) 참조로 해석돼 주소가 조용히 망가지므로 <c>%%</c> 로 써야 한다.
        /// </summary>
        [TestMethod]
        public void StartupScript_TargetUrl_ShouldEscapePercentSigns()
        {
            var configuration = DefaultConfiguration();
            configuration.TargetUrl = "https://www.wooribank.com/search?q=%EC%9A%B0%EB%A6%AC";

            var script = GenerateScript(configuration);

            StringAssert.Contains(script, "q=%%EC%%9A%%B0%%EB%%A6%%AC",
                "퍼센트 인코딩된 URL 의 % 가 이스케이프되지 않았습니다.");
            Assert.IsFalse(script.Contains("q=%EC", StringComparison.Ordinal),
                "이스케이프되지 않은 % 가 남아 있습니다.");
        }

        /// <summary>
        /// 스크립트 전체가 ASCII 여야 한다는 제약(<see cref="StartupScript_ShouldBePureAscii"/>)은
        /// 대상 URL 에도 적용된다. 원문에 한글이 있으면 퍼센트 인코딩해서 실어야 한다.
        /// </summary>
        [TestMethod]
        public void StartupScript_TargetUrl_WithNonAsciiCharacters_StaysAscii()
        {
            var configuration = DefaultConfiguration();
            configuration.TargetUrl = "https://www.wooribank.com/search?q=우리";

            var script = GenerateScript(configuration);

            var offending = script.Where(c => c > 0x7F).Distinct().ToArray();

            Assert.IsEmpty(offending,
                $"대상 URL 의 비 ASCII 문자가 그대로 스크립트에 들어갔습니다: {string.Join(", ", offending.Select(c => $"U+{(int)c:X4}"))}");
            StringAssert.Contains(script, "q=%%EC%%9A%%B0%%EB%%A6%%AC",
                "비 ASCII 문자가 UTF-8 퍼센트 인코딩으로 바뀌지 않았습니다.");
        }
    }
}
