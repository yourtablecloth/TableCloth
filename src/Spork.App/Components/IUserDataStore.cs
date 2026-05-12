using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.UserData;

namespace Spork.Components
{
    /// <summary>
    /// Data 디렉터리(샌드박스에서는 <c>C:\Users\WDAGUtilityAccount\Desktop\Data</c>)에
    /// 영속화되는 사용자 데이터(즐겨찾기, 사용 기록 등)에 대한 읽기/쓰기 추상화입니다.
    /// </summary>
    public interface IUserDataStore
    {
        /// <summary>
        /// 사용자 데이터 파일의 절대 경로입니다.
        /// </summary>
        string UserDataFilePath { get; }

        /// <summary>
        /// 사용자 데이터를 로드합니다. 파일이 없거나 손상된 경우 기본값을 반환합니다.
        /// </summary>
        Task<SporkUserData> LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 사용자 데이터를 저장합니다. 디렉터리가 없으면 생성합니다.
        /// </summary>
        Task SaveAsync(SporkUserData userData, CancellationToken cancellationToken = default);
    }
}
