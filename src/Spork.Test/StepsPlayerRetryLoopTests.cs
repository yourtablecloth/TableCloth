using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spork.Steps;
using Spork.Steps.Implementations;
using Spork.ViewModels;
using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Test
{
    /// <summary>
    /// 재시도 루프(LoadStepContentWithRetryAsync) 자체의 동작을 검증한다. 특히 "영구성(비일시적) 오류는
    /// 실패 후 재시도하지 않고 1회 시도로 끝나는지", "일시적 오류는 최대 2회까지 재시도(총 3회)하는지",
    /// "중간에 성공하면 멈추는지"를 실제 호출 횟수로 확인한다.
    /// (생성자 의존성이 필요 없는 메서드라 GetUninitializedObject로 인스턴스를 만들어 리플렉션으로 호출한다.
    ///  실제 1s/3s 지연은 테스트 속도를 위해 리플렉션으로 0으로 바꿔 두고 종료 시 복원한다.)
    /// </summary>
    [TestClass]
    public class StepsPlayerRetryLoopTests
    {
        private static readonly FieldInfo DownloadRetryDelaysField = typeof(StepsPlayer)
            .GetField("DownloadRetryDelays", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo LoadStepContentWithRetryAsyncMethod = typeof(StepsPlayer)
            .GetMethod("LoadStepContentWithRetryAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private TimeSpan[] _originalDelays = Array.Empty<TimeSpan>();

        [TestInitialize]
        public void ZeroOutRetryDelays()
        {
            // readonly는 필드 참조만 보호하므로 배열 원소는 수정 가능하다. 지연을 0으로 만들어 테스트를 빠르게 한다.
            var delays = (TimeSpan[])DownloadRetryDelaysField.GetValue(null)!;
            _originalDelays = (TimeSpan[])delays.Clone();
            for (var i = 0; i < delays.Length; i++)
                delays[i] = TimeSpan.Zero;
        }

        [TestCleanup]
        public void RestoreRetryDelays()
        {
            var delays = (TimeSpan[])DownloadRetryDelaysField.GetValue(null)!;
            for (var i = 0; i < delays.Length; i++)
                delays[i] = _originalDelays[i];
        }

        // 호출 횟수를 세고, 시도 번호(0-based)에 따라 원하는 결과(faulted/완료)를 돌려주는 fake Step.
        private sealed class CountingStep : IStep
        {
            private readonly Func<int, Task> _behavior;
            public int InvocationCount { get; private set; }

            public CountingStep(Func<int, Task> behavior) => _behavior = behavior;

            public Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
                => _behavior(InvocationCount++);

            public Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            public Task PlayStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public bool ShouldSimulateWhenDryRun => false;
        }

        private static async Task InvokeRetryLoopAsync(StepItemViewModel item)
        {
            var player = (StepsPlayer)RuntimeHelpers.GetUninitializedObject(typeof(StepsPlayer));
            var task = (Task)LoadStepContentWithRetryAsyncMethod.Invoke(player, [item, CancellationToken.None])!;
            await task;
        }

        private static StepItemViewModel CreateItem(CountingStep step)
            => new StepItemViewModel { Step = step, Argument = new InstallItemViewModel() };

        private static Task Fail(HttpStatusCode statusCode)
            => Task.FromException(new HttpRequestException("simulated", null, statusCode));

        // ── 영구성 상태 코드: 실패 후 재시도하지 않고 1회 시도로 끝난다 ──
        [TestMethod]
        public async Task PermanentStatusCode_501_DoesNotRetry_SingleAttempt()
        {
            var step = new CountingStep(_ => Fail(HttpStatusCode.NotImplemented)); // 501
            var item = CreateItem(step);

            Exception? caught = null;
            try { await InvokeRetryLoopAsync(item); }
            catch (Exception ex) { caught = ex; }

            Assert.IsNotNull(caught, "영구성 오류는 최종적으로 예외로 전파되어야 한다.");
            Assert.IsTrue(caught is HttpRequestException);
            Assert.AreEqual(1, step.InvocationCount, "영구성 상태 코드는 재시도하지 않아야 한다(1회).");
        }

        // ── 비일시적 예외 타입: 재시도하지 않고 1회 시도로 끝난다 ──
        [TestMethod]
        public async Task NonTransientException_DoesNotRetry_SingleAttempt()
        {
            var step = new CountingStep(_ => Task.FromException(new InvalidOperationException("permanent")));
            var item = CreateItem(step);

            Exception? caught = null;
            try { await InvokeRetryLoopAsync(item); }
            catch (Exception ex) { caught = ex; }

            Assert.IsNotNull(caught);
            Assert.IsTrue(caught is InvalidOperationException);
            Assert.AreEqual(1, step.InvocationCount, "비일시적 예외는 재시도하지 않아야 한다(1회).");
        }

        // ── 일시적 상태 코드가 계속 실패: 총 3회(초기 + 재시도 2회) 시도 후 예외 ──
        [TestMethod]
        public async Task TransientStatusCode_AlwaysFails_RetriesUpToThreeAttempts()
        {
            var step = new CountingStep(_ => Fail(HttpStatusCode.ServiceUnavailable)); // 503
            var item = CreateItem(step);

            Exception? caught = null;
            try { await InvokeRetryLoopAsync(item); }
            catch (Exception ex) { caught = ex; }

            Assert.IsNotNull(caught, "재시도 소진 후에는 마지막 예외가 전파되어야 한다.");
            Assert.IsTrue(caught is HttpRequestException);
            Assert.AreEqual(3, step.InvocationCount, "일시적 오류는 초기 1회 + 재시도 2회 = 총 3회 시도해야 한다.");
        }

        // ── 일시적 실패 후 중간에 성공: 성공하면 더 이상 시도하지 않는다 ──
        [TestMethod]
        public async Task TransientThenSuccess_StopsRetrying_OnFirstSuccess()
        {
            var step = new CountingStep(attempt => attempt == 0
                ? Fail(HttpStatusCode.ServiceUnavailable) // 첫 시도 실패(일시적)
                : Task.CompletedTask);                    // 두 번째 시도 성공
            var item = CreateItem(step);

            await InvokeRetryLoopAsync(item); // 예외 없이 반환되어야 한다.

            Assert.AreEqual(2, step.InvocationCount, "첫 재시도에서 성공하면 총 2회 시도로 끝나야 한다.");
        }
    }
}
