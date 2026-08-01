namespace TableCloth.Models.Configuration
{
    /// <summary>
    /// <see cref="ReleaseChannelReconciler.Reconcile"/>의 판정 결과.
    /// </summary>
    public readonly struct ReleaseChannelReconciliation
    {
        internal ReleaseChannelReconciliation(
            ReleaseChannel channel, ReleaseChannel lastKnownInstalledChannel,
            bool requiresSave, bool channelChanged = false)
        {
            Channel = channel;
            LastKnownInstalledChannel = lastKnownInstalledChannel;
            RequiresSave = requiresSave;
            ChannelChanged = channelChanged;
        }

        /// <summary>실제로 사용해야 할 릴리스 링.</summary>
        public ReleaseChannel Channel { get; }

        /// <summary>설정에 기록해 둘 "마지막으로 관측한 설치본의 링".</summary>
        public ReleaseChannel LastKnownInstalledChannel { get; }

        /// <summary>설정 파일을 다시 저장해야 하는지 여부.</summary>
        public bool RequiresSave { get; }

        /// <summary>사용자가 고른 링이 이번 대조로 바뀌었는지 여부(= 자동 옵트아웃이 일어났는지).</summary>
        public bool ChannelChanged { get; }
    }

    /// <summary>
    /// 설정에 저장된 릴리스 링을 실제 설치본의 링과 대조하는 규칙.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 설정 파일은 <c>%LOCALAPPDATA%\TableCloth.Data</c>에 있어 <b>재설치를 살아남는다.</b> 그래서 미리 보기를
    /// 쓰던 사용자가 안정 버전을 수동으로 재설치해도 설정은 여전히 미리 보기를 가리키고, 다음 업데이트
    /// 확인에서 다시 미리 보기로 끌려 올라간다. 이 규칙이 그 상태를 끊는다.
    /// </para>
    /// <para>
    /// 판정 기준은 "설치본의 링이 <b>지난번 관측과 달라졌는가</b>"다. 단순히 "설치본을 따른다"로 하면
    /// 미리 보기로 토글한 뒤 업데이트를 받기 전에 재시작했을 때 토글이 곧바로 되돌아가 버린다.
    /// </para>
    /// </remarks>
    public static class ReleaseChannelReconciler
    {
        /// <summary>
        /// 사용자가 고른 링과 설치본의 링을 대조해 실제 사용할 링을 정한다.
        /// </summary>
        /// <param name="selectedChannel">설정에 저장된, 사용자가 고른 링.</param>
        /// <param name="lastKnownInstalledChannel">
        /// 마지막으로 관측한 설치본의 링. <see langword="null"/>이면 관측 기록이 없다는 뜻이며,
        /// 이 경우 사용자의 선택을 존중하고 기준선만 남긴다(기능 도입 전에 설치된 사용자).
        /// </param>
        /// <param name="installedChannel">지금 실행 중인 설치본의 링.</param>
        public static ReleaseChannelReconciliation Reconcile(
            ReleaseChannel selectedChannel,
            ReleaseChannel? lastKnownInstalledChannel,
            ReleaseChannel installedChannel)
        {
            // 설치 상태가 그대로다 — 사용자의 선택을 그대로 쓴다.
            // (미리 보기로 토글한 뒤 아직 업데이트를 안 받은 상태가 여기 해당한다.)
            if (lastKnownInstalledChannel == installedChannel)
                return new ReleaseChannelReconciliation(selectedChannel, installedChannel, requiresSave: false);

            // 관측 기록이 없다 — 기준선만 남기고 선택은 건드리지 않는다.
            if (!lastKnownInstalledChannel.HasValue)
                return new ReleaseChannelReconciliation(selectedChannel, installedChannel, requiresSave: true);

            // 앱 밖에서 링이 바뀌었다 — 설치본을 따라간다(= 수동 다운그레이드 시 자동 옵트아웃).
            return new ReleaseChannelReconciliation(
                installedChannel, installedChannel,
                requiresSave: true, channelChanged: selectedChannel != installedChannel);
        }
    }
}
