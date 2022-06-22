using System;
using System.Collections.ObjectModel;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 카탈로그 패키지 정보를 보관하는 컬렉션 형식입니다. <see cref="CatalogPackageInformation.Name"/> 속성을 키 속성으로 사용합니다.
    /// </summary>
    [Serializable]
    public sealed class PackageCollection : KeyedCollection<string, CatalogPackageInformation>
    {
        /// <summary>
        /// <see cref="CatalogPackageInformation"/> 형식에서 기준이 되는 키 속성의 값을 읽습니다.
        /// </summary>
        /// <param name="item">기준이 되는 키 속성을 조회할 개체의 참조</param>
        /// <returns>기준이 되는 키 속성의 값을 반환합니다.</returns>
        protected override string GetKeyForItem(CatalogPackageInformation item)
            => item.Name;
    }
}
