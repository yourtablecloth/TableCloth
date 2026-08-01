using TableCloth.Models.Configuration;

namespace TableCloth.Test
{
    /// <summary>
    /// 릴리스 링 대조 규칙(<see cref="ReleaseChannelReconciler"/>) 검증.
    /// </summary>
    /// <remarks>
    /// 설정 파일이 재설치를 살아남기 때문에, 미리 보기에서 안정으로 수동 재설치한 사용자가 다음 업데이트
    /// 확인에서 다시 미리 보기로 끌려 올라가는 함정이 있었다. 이 규칙이 그것을 끊되, 반대로 "미리 보기로
    /// 토글한 뒤 업데이트 전에 재시작" 이 토글을 되돌려버리는 일도 없어야 한다. 두 요구가 충돌하므로
    /// 각 상태 전이를 여기서 고정한다.
    /// </remarks>
    [TestClass]
    public sealed class ReleaseChannelReconcilerTests
    {
        [TestMethod]
        public void ToggledToPreview_BeforeUpdatingYet_KeepsTheChoice()
        {
            // 사용자가 미리 보기로 토글만 하고 아직 업데이트를 받지 않은 상태에서 앱을 재시작한 경우.
            // 설치본은 여전히 안정이지만, 관측값도 안정이라 "밖에서 바뀐 것"이 아니다.
            var result = ReleaseChannelReconciler.Reconcile(
                selectedChannel: ReleaseChannel.Preview,
                lastKnownInstalledChannel: ReleaseChannel.Retail,
                installedChannel: ReleaseChannel.Retail);

            Assert.AreEqual(ReleaseChannel.Preview, result.Channel, "토글한 선택이 되돌아가면 안 됩니다.");
            Assert.IsFalse(result.RequiresSave);
            Assert.IsFalse(result.ChannelChanged);
        }

        [TestMethod]
        public void PreviewUpdateApplied_RecordsNewBaselineWithoutChangingChoice()
        {
            // 미리 보기 업데이트가 적용되어 설치본이 미리 보기가 된 상태.
            var result = ReleaseChannelReconciler.Reconcile(
                selectedChannel: ReleaseChannel.Preview,
                lastKnownInstalledChannel: ReleaseChannel.Retail,
                installedChannel: ReleaseChannel.Preview);

            Assert.AreEqual(ReleaseChannel.Preview, result.Channel);
            Assert.AreEqual(ReleaseChannel.Preview, result.LastKnownInstalledChannel);
            Assert.IsTrue(result.RequiresSave, "관측값이 갱신되어야 다음 대조가 정확해집니다.");
            Assert.IsFalse(result.ChannelChanged, "선택은 그대로 미리 보기입니다.");
        }

        [TestMethod]
        public void ManualDowngradeToStable_OptsOutOfPreview()
        {
            // 이 테스트가 이 규칙의 존재 이유다. 미리 보기를 쓰던 사용자가 안정 설치본을 직접 설치한 상태.
            var result = ReleaseChannelReconciler.Reconcile(
                selectedChannel: ReleaseChannel.Preview,
                lastKnownInstalledChannel: ReleaseChannel.Preview,
                installedChannel: ReleaseChannel.Retail);

            Assert.AreEqual(ReleaseChannel.Retail, result.Channel, "수동 다운그레이드는 미리 보기 옵트아웃이어야 합니다.");
            Assert.AreEqual(ReleaseChannel.Retail, result.LastKnownInstalledChannel);
            Assert.IsTrue(result.RequiresSave);
            Assert.IsTrue(result.ChannelChanged);
        }

        [TestMethod]
        public void ManualUpgradeToPreviewInstaller_OptsIn()
        {
            // 반대 방향도 같은 규칙으로 처리된다: 미리 보기 설치본을 직접 설치했으면 미리 보기 링으로 본다.
            var result = ReleaseChannelReconciler.Reconcile(
                selectedChannel: ReleaseChannel.Retail,
                lastKnownInstalledChannel: ReleaseChannel.Retail,
                installedChannel: ReleaseChannel.Preview);

            Assert.AreEqual(ReleaseChannel.Preview, result.Channel);
            Assert.IsTrue(result.ChannelChanged);
        }

        [TestMethod]
        public void FirstObservation_SeedsBaselineButRespectsTheChoice()
        {
            // 이 기능 도입 전에 설치된 사용자: 관측 기록이 없다. 선택을 건드리면 안 된다.
            var previewUser = ReleaseChannelReconciler.Reconcile(
                selectedChannel: ReleaseChannel.Preview,
                lastKnownInstalledChannel: null,
                installedChannel: ReleaseChannel.Preview);

            Assert.AreEqual(ReleaseChannel.Preview, previewUser.Channel);
            Assert.AreEqual(ReleaseChannel.Preview, previewUser.LastKnownInstalledChannel);
            Assert.IsTrue(previewUser.RequiresSave, "기준선은 남겨야 다음부터 대조가 됩니다.");
            Assert.IsFalse(previewUser.ChannelChanged);

            // 이미 함정에 빠져 있던 사용자(설정=미리 보기, 설치본=안정)도 첫 관측에서는 선택을 존중한다.
            // 이 경우의 구제는 문서의 수동 절차나 옵션 창 전환이 담당한다.
            var trappedUser = ReleaseChannelReconciler.Reconcile(
                selectedChannel: ReleaseChannel.Preview,
                lastKnownInstalledChannel: null,
                installedChannel: ReleaseChannel.Retail);

            Assert.AreEqual(ReleaseChannel.Preview, trappedUser.Channel);
            Assert.AreEqual(ReleaseChannel.Retail, trappedUser.LastKnownInstalledChannel);
            Assert.IsTrue(trappedUser.RequiresSave);
        }

        [TestMethod]
        public void SteadyState_RequiresNoWrite()
        {
            // 평상시(안정 사용자)에는 설정 파일을 건드리지 않아야 한다.
            var result = ReleaseChannelReconciler.Reconcile(
                selectedChannel: ReleaseChannel.Retail,
                lastKnownInstalledChannel: ReleaseChannel.Retail,
                installedChannel: ReleaseChannel.Retail);

            Assert.AreEqual(ReleaseChannel.Retail, result.Channel);
            Assert.IsFalse(result.RequiresSave);
        }
    }
}
