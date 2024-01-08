using System.Collections.Generic;

namespace TableCloth.Models.Catalog
{
    internal static class DesignTimeCatalog
    {
        public static CatalogDocument DesignTimeCatalogDocument = new CatalogDocument()
        {
            Services = new List<CatalogInternetService>(new CatalogInternetService[] {
              new CatalogInternetService() {
                Id = "WooriBank", DisplayName = "[디자인] W은행", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.wooribank.com/",
                  CompatibilityNotes = @"이 웹 사이트는 해당 기관의 보안 정책에 따라 AhnLab Safe Transaction이 Windows Sandbox의 필수 구성 요소인 RDP 세션을 강제 종료하도록 구성되어있습니다. https://yourtablecloth.app/troubleshoot.html 페이지를 참고하여 AST가 원격 연결을 허용하도록 사이트 이용 전에 먼저 변경한 후 접속하는 것을 권장합니다.", Packages = new List < CatalogPackageInformation > (new CatalogPackageInformation[] {
                    new CatalogPackageInformation() {
                      Name = "Veraport", Url = "https://www.wooribank.com/download/veraportG3/veraport-g3-x64.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "AnySign", Url = "https://www.wooribank.com/download/AnySign_Installer/AnySign_Installer.exe", Arguments = "/S",
                    }, new CatalogPackageInformation() {
                      Name = "AhnLabSafeTx", Url = "https://safetx.ahnlab.com/master/win/default/all/astx_setup.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "NOS", Url = "https://www.wooribank.com/download/NOS/nos_setup.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "IPInside", Url = "https://www.wooribank.com/download/IPinside/I3GSvcManager_3.0.0.7.exe", Arguments = "/nodlg",
                    }
                  })
              }, new CatalogInternetService() {
                Id = "KookminBank", DisplayName = "[디자인] K은행", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.kbstar.com/",
                  CompatibilityNotes = @"이 웹 사이트는 해당 기관의 보안 정책에 따라 AhnLab Safe Transaction이 Windows Sandbox의 필수 구성 요소인 RDP 세션을 강제 종료하도록 구성되어있습니다. https://yourtablecloth.app/troubleshoot.html 페이지를 참고하여 AST가 원격 연결을 허용하도록 사이트 이용 전에 먼저 변경한 후 접속하는 것을 권장합니다.", Packages = new List < CatalogPackageInformation > (new CatalogPackageInformation[] {
                    new CatalogPackageInformation() {
                      Name = "AhnLabSafeTx", Url = "https://safetx.ahnlab.com/master/win/default/all/astx_setup.exe", Arguments = "",
                    }, new CatalogPackageInformation() {
                      Name = "WizInDelfino", Url = "https://download.kbstar.com/security/wizvera/delfino/g3/delfino-g3.exe", Arguments = "/silent",
                    }
                  })
              }, new CatalogInternetService() {
                Id = "KEBHanaBank", DisplayName = "[디자인] H은행", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.kebhana.com/",
                  CompatibilityNotes = @"", Packages = new List < CatalogPackageInformation > (new CatalogPackageInformation[] {
                    new CatalogPackageInformation() {
                      Name = "Veraport", Url = "https://www.kebhana.com/wizvera/veraport/down/veraport-g3-x64-sha2.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "TouchEnKey32", Url = "https://www.kebhana.com/TouchEn/nxKey/module/TouchEn_nxKey_Installer_32bit.exe", Arguments = "/silence",
                    }, new CatalogPackageInformation() {
                      Name = "Delfino", Url = "https://www.kebhana.com/wizvera/delfino/down/g3/delfino-g3.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "SCWSSP", Url = "https://www.kebhana.com/softcamp/WebSecurityStandard/SCWSSPSetup.exe", Arguments = "",
                    }, new CatalogPackageInformation() {
                      Name = "IPInside", Url = "https://www.kebhana.com/interezen/agent/np_v6/I3GSvcManager.exe", Arguments = "/nodlg",
                    }
                  })
              }, new CatalogInternetService() {
                Id = "ShinhanBank", DisplayName = "[디자인] S은행", Category = CatalogInternetServiceCategory.Banking, Url = "https://www.shinhan.com/",
                  CompatibilityNotes = @"이 웹 사이트는 해당 기관의 보안 정책에 따라 AhnLab Safe Transaction이 Windows Sandbox의 필수 구성 요소인 RDP 세션을 강제 종료하도록 구성되어있습니다. https://yourtablecloth.app/troubleshoot.html 페이지를 참고하여 AST가 원격 연결을 허용하도록 사이트 이용 전에 먼저 변경한 후 접속하는 것을 권장합니다.", Packages = new List < CatalogPackageInformation > (new CatalogPackageInformation[] {
                    new CatalogPackageInformation() {
                      Name = "ASTX", Url = "https://bank.shinhan.com/sw/astx/astxdn.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "INISAFECrossWeb", Url = "https://bank.shinhan.com/sw/initech/extension/down/INIS_EX_SHA2.exe?ver=1.0.1.961", Arguments = "/S",
                    }, new CatalogPackageInformation() {
                      Name = "TouchEnKey32", Url = "https://bank.shinhan.com/sw/raon/TouchEn/nxKey/module/TouchEn_nxKey_32bit.exe?ver=1.0.0.50", Arguments = "/silence",
                    }, new CatalogPackageInformation() {
                      Name = "Printmade3", Url = "https://bank.shinhan.com/sw/printmade/download_files/Windows/Printmade3_setup.exe", Arguments = "",
                    }
                  })
              }, new CatalogInternetService() {
                Id = "NHInternetBank", DisplayName = "[디자인] N은행", Category = CatalogInternetServiceCategory.Banking, Url = "https://banking.nonghyup.com/",
                  CompatibilityNotes = @"이 웹 사이트는 해당 기관의 보안 정책에 따라 AhnLab Safe Transaction이 Windows Sandbox의 필수 구성 요소인 RDP 세션을 강제 종료하도록 구성되어있습니다. https://yourtablecloth.app/troubleshoot.html 페이지를 참고하여 AST가 원격 연결을 허용하도록 사이트 이용 전에 먼저 변경한 후 접속하는 것을 권장합니다.", Packages = new List < CatalogPackageInformation > (new CatalogPackageInformation[] {
                    new CatalogPackageInformation() {
                      Name = "Veraport", Url = "https://veraport.nonghyup.com/download/20230210/veraport-g3-x64.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "AhnLabSafeTx", Url = "https://safetx.ahnlab.com/master/win/default/all/astx_setup.exe", Arguments = "/silent",
                    }, new CatalogPackageInformation() {
                      Name = "INISAFECrossWebEx", Url = "https://veraport.nonghyup.com/download/20230210/INIS_EX_SHA2.exe?ver=1.0.1.1021", Arguments = "/S",
                    }, new CatalogPackageInformation() {
                      Name = "TouchEnKey64", Url = "https://img.nonghyup.com/install/so/raon/TouchEnNxKey/TouchEn_nxKey_Installer_64bit_new.exe", Arguments = "/silence",
                    }, new CatalogPackageInformation() {
                      Name = "TouchEnKey32", Url = "https://veraport.nonghyup.com/download/20230210/TouchEn_nxKey_Installer_32bit_new.exe", Arguments = "/silence",
                    }
                  })
              },
            }),
            Companions = new List<CatalogCompanion>(new CatalogCompanion[] { })
        };
    }
}
