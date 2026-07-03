using System;
using System.IO;
using System.Text.Json;
using TableCloth.Models.Answers;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// <see cref="ISessionPolicyProvider"/>의 무료판 구현. 호스트가 staging 폴더에 떨궈둔
    /// <c>SporkAnswers.json</c>에서 유휴 자동 종료 설정을 1회 읽어 정책으로 노출한다(이슈 #197).
    /// 파일이 없거나(단독 실행 등) 값이 비활성이면 <see cref="IdleAutoLogoutPolicy.Disabled"/>를 반환하므로
    /// 정상 사용자·단독 실행에는 영향이 없다.
    /// </summary>
    public sealed class SporkAnswersSessionPolicyProvider : ISessionPolicyProvider
    {
        private IdleAutoLogoutPolicy _cached;

        public IdleAutoLogoutPolicy GetIdleAutoLogoutPolicy()
            => _cached ??= LoadIdlePolicy();

        private static IdleAutoLogoutPolicy LoadIdlePolicy()
        {
            try
            {
                // Spork.exe(또는 통합된 TableCloth.exe spork)와 동일 디렉터리.
                var answerFilePath = Path.Combine(AppContext.BaseDirectory, "SporkAnswers.json");
                if (!File.Exists(answerFilePath))
                    return IdleAutoLogoutPolicy.Disabled;

                using var stream = File.OpenRead(answerFilePath);
                var answers = JsonSerializer.Deserialize<SporkAnswers>(stream);

                if (answers == null || !answers.EnableIdleAutoLogout || answers.IdleAutoLogoutMinutes <= 0)
                    return IdleAutoLogoutPolicy.Disabled;

                return new IdleAutoLogoutPolicy(true, answers.IdleAutoLogoutMinutes);
            }
            catch
            {
                // 정책을 못 읽으면 안전하게 비활성으로 둔다(기능을 켜지 않는 쪽이 안전한 기본값).
                return IdleAutoLogoutPolicy.Disabled;
            }
        }
    }
}
