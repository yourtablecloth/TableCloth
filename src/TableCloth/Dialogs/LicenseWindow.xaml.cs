using System.Windows;

namespace TableCloth.Dialogs;

public partial class LicenseWindow : Window
{
    public LicenseWindow()
    {
        InitializeComponent();
        
        // Set UI strings (bilingual Korean/English)
        Title = "License Agreement / 라이선스 동의";
        InstructionLabel.Content = "Please read the following license agreement carefully.\n다음 라이선스 계약을 주의 깊게 읽어주십시오.";
        AgreeButton.Content = "I Agree / 동의합니다";
        DeclineButton.Content = "I Decline / 동의하지 않음";
        LicenseContentTextBox.Text = GetLicenseContent();
    }

    public bool LicenseAccepted { get; private set; }

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseAccepted = true;
        DialogResult = true;
        Close();
    }

    private void DeclineButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseAccepted = false;
        DialogResult = false;
        Close();
    }

    private static string GetLicenseContent()
    {
        return @"식탁보 프로그램 설치 및 사용에 관한 안내
TableCloth Program Installation and Usage Guide

최종 수정일 / Last Updated: 2024년 4월 5일

중요한 내용이오니 반드시 정독하여 주십시오.
IMPORTANT: PLEASE READ CAREFULLY.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

소프트웨어 사용 시 주의 사항 / Software Usage Precautions

식탁보 소프트웨어는 다음의 조건 아래에서 사용할 수 있습니다. 설치를 계속 진행하는 경우, 이 안내문에서 설명하는 조건에 동의한 것으로 봅니다.

TableCloth software can be used under the following conditions. By continuing with the installation, you agree to the terms described in this guide.

만약 아래 조건에 동의하지 않을 경우, 언제든 설치를 취소하고 사용을 철회할 수 있습니다.

If you do not agree to the conditions below, you may cancel the installation and withdraw your use at any time.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

? 책임지지 않습니다 / NO WARRANTY:
본 오픈 소스 프로젝트의 개발자 및 모든 기여자는 소프트웨어의 품질, 성능, 또는 상업성과 같은 어떠한 명시적이거나 암시적인 보증도 제공하지 않습니다.

The developers and all contributors of this open source project do not provide any express or implied warranties, including quality, performance, or merchantability of the software.

? 위험을 감수합니다 / ASSUMPTION OF RISK:
식탁보의 사용으로 인해 발생할 수 있는 모든 위험은 사용자에게 이전됩니다. 사용 중에 발생하는 문제에 대한 책임은 전적으로 사용자가 집니다.

All risks arising from the use of TableCloth are transferred to the user. The user is solely responsible for any issues that occur during use.

? 손해 배상을 하지 않습니다 / NO LIABILITY FOR DAMAGES:
본 오픈 소스 프로젝트의 개발자 및 모든 기여자는 소프트웨어 사용으로 인해 발생하는 직접적, 간접적, 부수적, 특별한, 징벌적 또는 결과적 손해에 대해 책임을 지지 않으며 손해를 배상하지 않습니다.

The developers and all contributors of this open source project are not liable for and will not compensate any direct, indirect, incidental, special, punitive, or consequential damages arising from the use of the software.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

소프트웨어 사용 권한 / Software Usage Rights

이 소프트웨어 (식탁보)는 오픈 소스 프로젝트 활동을 통하여 개발된 소프트웨어이며, 누구나 무료로 사용할 수 있습니다.

This software (TableCloth) is developed through open source project activities and is free for anyone to use.

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

소스 코드 사용 권한 / Source Code Usage Rights

이 소프트웨어 (식탁보)의 소스 코드의 일부 또는 전체를 활용하기 위해서는 다음 중 하나의 라이선스를 준수해야 합니다.

To use part or all of the source code of this software (TableCloth), you must comply with one of the following licenses:

- 1.13.0 버전 이후 / Version 1.13.0 and later: AGPL 3.0 라이선스 ? 또는 ? 상업 라이선스
  AGPL 3.0 License ? or ? Commercial License

- 1.13.0 버전 이전 / Before version 1.13.0: MIT 라이선스 / MIT License

위의 라이선스 중 적합한 라이선스를 택하여 사용하실 수 있습니다.
You may choose the appropriate license from the above.

코드 사용 권한에 관련된 상세한 라이선스 정보는 다음 웹 사이트를 확인하여 주십시오:
For detailed license information, please refer to:

https://github.com/yourtablecloth/TableCloth";
    }
}
