using System;
using System.Collections.Generic;

namespace TableCloth.Models.UserData
{
    /// <summary>
    /// 샌드박스의 Data 디렉터리에 영속화되는 사용자 데이터입니다.
    /// 즐겨찾기, 사이트별 최근 사용 시각 등 세션 간에 유지되어야 하는 정보를 담습니다.
    /// 호스트와 Spork가 동일 스키마로 직렬화/역직렬화하므로 본 모델은 TableCloth.Core에 위치합니다.
    /// </summary>
    public sealed class SporkUserData
    {
        /// <summary>
        /// Data 디렉터리 안에 저장되는 파일 이름입니다. 호스트의 마이그레이션 코드와
        /// Spork의 <c>IUserDataStore</c>가 동일한 경로 규약을 공유하기 위해 사용합니다.
        /// </summary>
        public const string FileName = "user-data.json";

        /// <summary>
        /// 현재 user-data.json 스키마 버전입니다. 향후 마이그레이션 분기용.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// 즐겨찾기로 등록된 사이트 ID 목록.
        /// </summary>
        public List<string> Favorites { get; set; } = new List<string>();

        /// <summary>
        /// 즐겨찾기만 보기 토글의 마지막 상태.
        /// </summary>
        public bool ShowFavoritesOnly { get; set; } = false;

        /// <summary>
        /// 사이트별 최근 사용 시각(UTC). 카탈로그 정렬/하이라이트에 사용.
        /// </summary>
        public Dictionary<string, DateTime> LastUsedAt { get; set; } = new Dictionary<string, DateTime>();
    }
}
