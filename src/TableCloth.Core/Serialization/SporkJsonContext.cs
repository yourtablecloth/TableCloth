using System.Text.Json.Serialization;
using TableCloth.Models.Answers;
using TableCloth.Models.UserData;

namespace TableCloth.Serialization
{
    /// <summary>
    /// Spork 계약 타입(SporkAnswers/SporkUserData/InstallRecord)의 System.Text.Json 소스 생성 컨텍스트.
    /// Native AOT/트리밍에서 리플렉션 없이 (역)직렬화하기 위해 사용한다. 세 타입 모두 TableCloth.Core에
    /// 정의된 공유 계약이므로 컨텍스트도 Core에 두어 호스트(TableCloth.App)·에이전트(Spork.App)·
    /// 부팅(Spork.Sandbox) 세 프로젝트가 함께 사용한다.
    ///
    /// 기존 호출부 옵션(쓰기 시 들여쓰기, 읽기 시 trailing comma 허용)을 컨텍스트 옵션으로 재현하므로,
    /// 각 호출부는 <c>SporkJsonContext.Default.&lt;Type&gt;</c>를 넘기기만 하면 동일 동작을 얻는다.
    /// 프로퍼티 이름 정책은 기본(PascalCase, 대소문자 구분)을 유지해 기존 JSON 파일과 호환된다.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true)]
    [JsonSerializable(typeof(SporkAnswers))]
    [JsonSerializable(typeof(SporkUserData))]
    [JsonSerializable(typeof(InstallRecord))]
    public sealed partial class SporkJsonContext : JsonSerializerContext
    {
    }
}
