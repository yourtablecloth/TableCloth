using System;
using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Models.Configuration
{
    /// <summary>
    /// 샌드박스 및 내부로 전달할 설정값을 저장하는 모델 클래스입니다.
    /// </summary>
    [Serializable]
    public sealed class TableClothConfiguration
    {
        /// <summary>
        /// 선택한 X.509 인증서 정보입니다.
        /// </summary>
        public X509CertPair CertPair { get; set; } = null;

        /// <summary>
        /// 마이크 입력을 공유할 것인지 여부입니다.
        /// </summary>
        public bool EnableMicrophone { get; set; }

        /// <summary>
        /// 웹캠 입력을 공유할 것인지 여부입니다.
        /// </summary>
        public bool EnableWebCam { get; set; }

        /// <summary>
        /// 프린터 출력을 공유할 것인지 여부입니다.
        /// </summary>
        public bool EnablePrinters { get; set; }

        /// <summary>
        /// 모두의 프린터 설치를 샌드박스 시작 후 자동 실행할 지 여부입니다.
        /// </summary>
        public bool InstallEveryonesPrinter { get; set; }

        /// <summary>
        /// Adobe Reader 설치를 샌드박스 시작 후 자동 실행할 지 여부입니다.
        /// </summary>
        public bool InstallAdobeReader { get; set; }

        /// <summary>
        /// 한컴오피스 뷰어 설치를 샌드박스 시작 후 자동 실행할 지 여부입니다.
        /// </summary>
        public bool InstallHancomOfficeViewer { get; set; }

        /// <summary>
        /// RaiDrive 설치를 샌드박스 시작 후 자동 실행할 지 여부입니다.
        /// </summary>
        public bool InstallRaiDrive { get; set; }

        /// <summary>
        /// 같이 설치하는 공통 소프트웨어에 관한 정보입니다.
        /// </summary>
        public ICollection<CatalogCompanion> Companions { get; set; } = new List<CatalogCompanion>();

        /// <summary>
        /// 식탁보를 이용하여 샌드박스 내에서 설치해야 할 각 서비스 별 필요 소프트웨어에 관한 정보입니다.
        /// </summary>
        public ICollection<CatalogInternetService> Services { get; set; } = new List<CatalogInternetService>();

        /// <summary>
        /// 샌드박스 명세 파일 (WSB) 및 데이터 파일을 저장할 디렉터리 경로입니다.
        /// </summary>
        public string AssetsDirectoryPath { get; set; } = null;

        /// <summary>
        /// 샌드박스에 매핑할 사용자 지정 폴더 목록입니다.
        /// </summary>
        public ICollection<MappedFolderSetting> MappedFolders { get; set; } = new List<MappedFolderSetting>();
    }
}
