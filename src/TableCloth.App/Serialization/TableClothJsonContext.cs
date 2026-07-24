using System.Text.Json.Serialization;
using TableCloth.Models.Configuration;

namespace TableCloth.Serialization;

/// <summary>
/// System.Text.Json 소스 생성 컨텍스트 — Native AOT/트리밍에서 리플렉션 없이 (역)직렬화하기 위해 사용한다.
/// 기존 호출부의 옵션(쓰기 시 들여쓰기, 읽기 시 trailing comma 허용)을 컨텍스트 옵션으로 그대로 재현하므로,
/// 각 호출부는 <c>TableClothJsonContext.Default.PreferenceSettings</c>를 넘기기만 하면 동일 동작을 얻는다.
///
/// App 라이브러리에 두되, 진입점(TableCloth) 프로젝트는 <c>InternalsVisibleTo</c>로 internal 컨텍스트에 접근한다.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(PreferenceSettings))]
internal sealed partial class TableClothJsonContext : JsonSerializerContext
{
}
