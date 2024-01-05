using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Moq;
using Moq.AutoMock;
using System.Windows.Controls;
using System.Windows.Input;
using TableCloth.Resources;
using TableCloth.SpecFlow.Support;

namespace TableCloth.SpecFlow.StepDefinitions;

[Binding]
public partial class AppMessageBoxStepDefinitions { }

// 시나리오 a.
partial class AppMessageBoxStepDefinitions
{
    private string _aTitle = string.Empty;
    private string _aMessage = string.Empty;
    private MessageBoxButton _aButton;
    private AutoMocker? _acMocker;
    private AutoMocker? _adMocker;
    private AutoMocker? _aeMocker;

    [Given(@"a\.a\. 다음과 같이 메시지 박스에 나타낼 문자열을 준비한다\.")]
    public void GivenA_A_다음과같이메시지박스에나타낼문자열을준비한다_(Table table)
    {
        _aTitle = table.Rows[0][0];
        _aMessage = table.Rows[0][1];
        _aButton = MessageBoxButton.OK;
    }

    [Given(@"a\.b\. 기능 동작에 필요한 내부 컴포넌트들이 Mockup으로 설정되어 있다\.")]
    public void GivenA_B_기능동작에필요한내부컴포넌트들이Mockup으로설정되어있다_()
    {
        _acMocker = new AutoMocker(MockBehavior.Loose);
        _acMocker.Use(ScenarioDependencies.SharedApplication);

        _adMocker = new AutoMocker(MockBehavior.Loose);
        _adMocker.Use(ScenarioDependencies.SharedApplication);

        _aeMocker = new AutoMocker(MockBehavior.Loose);
        _aeMocker.Use(ScenarioDependencies.SharedApplication);
    }

    [Given(@"a\.c\. 나중에 시스템 메시지 박스의 아이콘이 정보를 나타내는 아이콘으로 표시되었는지 확인한다\.")]
    public void GivenA_C_나중에시스템메시지박스의아이콘이정보를나타내는아이콘으로표시되었는지확인한다_()
    {
        _acMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), IsAny<MessageBoxButton>(),
            MessageBoxImage.Information, IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [Given(@"a\.d\. 나중에 창 제목과 본문이 설정한대로 나타났는지 확인한다\.")]
    public void GivenA_D_나중에창제목과본문이설정한대로나타났는지확인한다_()
    {
        _adMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), _aMessage, _aTitle, IsAny<MessageBoxButton>(),
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [Given(@"a\.e\. 나중에 버튼은 확인 버튼만 나타났는지 확인한다\.")]
    public void GivenA_E_나중에버튼은확인버튼만나타났는지확인한다_()
    {
        _aeMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), _aButton,
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [When(@"a\.f\. 정보 표시를 위한 메시지 박스를 띄우는 메서드를 호출하면")]
    public void WhenA_F_정보표시를위한메시지박스를띄우는메서드를호출하면()
    {
        _acMocker!.CreateInstance<AppMessageBox>().DisplayInfo(_aMessage, _aButton);
        _adMocker!.CreateInstance<AppMessageBox>().DisplayInfo(_aMessage, _aButton);
        _aeMocker!.CreateInstance<AppMessageBox>().DisplayInfo(_aMessage, _aButton);
    }

    [Then(@"a\.g\. 의도한 대로 작동했는지 확인한다\.")]
    public void ThenA_G_의도한대로작동했는지확인한다_()
    {
        _acMocker!.VerifyAll();
        _adMocker!.VerifyAll();
        _aeMocker!.VerifyAll();
    }
}

partial class AppMessageBoxStepDefinitions
{
    private string _bWarnTitle = string.Empty;
    private string _bWarnMessage = string.Empty;
    private MessageBoxButton _bWarnButton;

    private string _bCritTitle = string.Empty;
    private string _bCritMessage = string.Empty;
    private MessageBoxButton _bCritButton;

    private AutoMocker? _bMocker;

    [Given(@"b\.a\. 다음과 같이 메시지 박스에 나타낼 문자열을 준비한다\.")]
    public void GivenB_A_다음과같이메시지박스에나타낼문자열을준비한다_(Table table)
    {
        _bWarnTitle = table.Rows[0][0];
        _bWarnMessage = table.Rows[0][1];
        _bWarnButton = MessageBoxButton.OK;

        _bCritTitle = table.Rows[1][0];
        _bCritMessage = table.Rows[1][1];
        _bCritButton = MessageBoxButton.OK;
    }

    [Given(@"b\.b\. 기능 동작에 필요한 내부 컴포넌트들이 Mockup으로 설정되어 있다\.")]
    public void GivenB_B_기능동작에필요한내부컴포넌트들이Mockup으로설정되어있다_()
    {
        _bMocker = new AutoMocker();
    }

    [Then(@"b\.c\. 나중에 시스템 메시지 박스의 아이콘이 오류를 나타내는 아이콘으로 표시되었는지 확인한다\.")]
    public void ThenB_C_나중에시스템메시지박스의아이콘이오류를나타내는아이콘으로표시되었는지확인한다_()
    {
    }

    [Then(@"b\.d\. 나중에 심각도 수준 지정에 따라 아이콘의 모양이 다르게 나타났는지 확인한다\.")]
    public void ThenB_D_나중에심각도수준지정에따라아이콘의모양이다르게나타났는지확인한다_()
    {
        throw new PendingStepException();
    }

    [Then(@"b\.e\. 나중에 창 제목과 본문이 설정한대로 나타났는지 확인한다\.")]
    public void ThenB_E_나중에창제목과본문이설정한대로나타났는지확인한다_()
    {
        throw new PendingStepException();
    }

    [Then(@"b\.f\. 나중에 버튼은 확인 버튼만 나타났는지 확인한다\.")]
    public void ThenB_F_나중에버튼은확인버튼만나타났는지확인한다_()
    {
        throw new PendingStepException();
    }

    [When(@"b\.g\. 오류 표시를 위한 메시지 박스를 띄우는 메서드를 호출하면")]
    public void WhenB_G_오류표시를위한메시지박스를띄우는메서드를호출하면()
    {
        throw new PendingStepException();
    }

    [Then(@"b\.h\. 의도한 대로 작동했는지 확인한다\.")]
    public void ThenB_H_의도한대로작동했는지확인한다_()
    {
        throw new PendingStepException();
    }
}

partial class AppMessageBoxStepDefinitions
{
    [Given(@"c\.a\. 다음과 같이 메시지 박스에 나타낼 문자열을 사용하여 예외 개체를 준비한다\.")]
    public void GivenC_A_다음과같이메시지박스에나타낼문자열을사용하여예외개체를준비한다_(Table table)
    {
        throw new PendingStepException();
    }

    [Given(@"c\.b\. 기능 동작에 필요한 내부 컴포넌트들이 Mockup으로 설정되어 있다\.")]
    public void GivenC_B_기능동작에필요한내부컴포넌트들이Mockup으로설정되어있다_()
    {
        throw new PendingStepException();
    }

    [Then(@"c\.c\. 나중에 시스템 메시지 박스의 아이콘이 오류를 나타내는 아이콘으로 표시되었는지 확인한다\.")]
    public void ThenC_C_나중에시스템메시지박스의아이콘이오류를나타내는아이콘으로표시되었는지확인한다_()
    {
        throw new PendingStepException();
    }

    [Then(@"c\.d\. 나중에 심각도 수준 지정에 따라 아이콘의 모양이 다르게 나타났는지 확인한다\.")]
    public void ThenC_D_나중에심각도수준지정에따라아이콘의모양이다르게나타났는지확인한다_()
    {
        throw new PendingStepException();
    }

    [Then(@"c\.e\. 나중에 창 제목과 본문이 설정한대로 나타났는지 확인한다\.")]
    public void ThenC_E_나중에창제목과본문이설정한대로나타났는지확인한다_()
    {
        throw new PendingStepException();
    }

    [Then(@"c\.f\. 나중에 버튼은 확인 버튼만 나타났는지 확인한다\.")]
    public void ThenC_F_나중에버튼은확인버튼만나타났는지확인한다_()
    {
        throw new PendingStepException();
    }

    [When(@"c\.g\. 예외 정보 표시를 위한 메시지 박스를 띄우는 메서드를 호출하면")]
    public void WhenC_G_예외정보표시를위한메시지박스를띄우는메서드를호출하면()
    {
        throw new PendingStepException();
    }

    [Then(@"c\.h\. 의도한 대로 작동했는지 확인한다\.")]
    public void ThenC_H_의도한대로작동했는지확인한다_()
    {
        throw new PendingStepException();
    }
}
