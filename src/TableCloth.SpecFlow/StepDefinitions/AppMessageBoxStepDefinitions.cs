using Moq;
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

    [Given(@"a\.a\. 다음과 같이 메시지 박스에 나타낼 문자열을 준비한다\.")]
    public void GivenA_A_다음과같이메시지박스에나타낼문자열을준비한다_(Table table)
    {
        _aTitle = table.Rows[0][0];
        _aMessage = table.Rows[0][1];
        _aButton = MessageBoxButton.OK;
    }

    private AutoMocker? _acMocker;
    private AutoMocker? _adMocker;
    private AutoMocker? _aeMocker;

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

    [Given(@"b\.a\. 다음과 같이 메시지 박스에 나타낼 문자열을 준비한다\.")]
    public void GivenB_A_다음과같이메시지박스에나타낼문자열을준비한다_(Table table)
    {
        _bWarnTitle = table.Rows[0][1];
        _bWarnMessage = table.Rows[0][2];
        _bWarnButton = MessageBoxButton.OK;

        _bCritTitle = table.Rows[1][1];
        _bCritMessage = table.Rows[1][2];
        _bCritButton = MessageBoxButton.OK;
    }

    private AutoMocker? _bcWarnMocker;
    private AutoMocker? _bdWarnMocker;
    private AutoMocker? _beWarnMocker;
    private AutoMocker? _bcCritMocker;
    private AutoMocker? _bdCritMocker;
    private AutoMocker? _beCritMocker;

    [Given(@"b\.b\. 기능 동작에 필요한 내부 컴포넌트들이 Mockup으로 설정되어 있다\.")]
    public void GivenB_B_기능동작에필요한내부컴포넌트들이Mockup으로설정되어있다_()
    {
        _bcWarnMocker = new AutoMocker(MockBehavior.Loose);
        _bcWarnMocker.Use(ScenarioDependencies.SharedApplication);

        _bdWarnMocker = new AutoMocker(MockBehavior.Loose);
        _bdWarnMocker.Use(ScenarioDependencies.SharedApplication);

        _beWarnMocker = new AutoMocker(MockBehavior.Loose);
        _beWarnMocker.Use(ScenarioDependencies.SharedApplication);

        _bcCritMocker = new AutoMocker(MockBehavior.Loose);
        _bcCritMocker.Use(ScenarioDependencies.SharedApplication);

        _bdCritMocker = new AutoMocker(MockBehavior.Loose);
        _bdCritMocker.Use(ScenarioDependencies.SharedApplication);

        _beCritMocker = new AutoMocker(MockBehavior.Loose);
        _beCritMocker.Use(ScenarioDependencies.SharedApplication);
    }

    [Given(@"b\.c\. 나중에 심각도 수준 지정에 따라 아이콘의 모양이 다르게 나타났는지 확인한다\.")]
    public void 조건B_C_나중에심각도수준지정에따라아이콘의모양이다르게나타났는지확인한다_()
    {
        _bcWarnMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), IsAny<MessageBoxButton>(),
            MessageBoxImage.Warning, IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );

        _bcCritMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), IsAny<MessageBoxButton>(),
            MessageBoxImage.Stop, IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [Given(@"b\.d\. 나중에 창 제목과 본문이 설정한대로 나타났는지 확인한다\.")]
    public void 조건B_D_나중에창제목과본문이설정한대로나타났는지확인한다_()
    {
        _bdWarnMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), _bWarnMessage, _bWarnTitle, IsAny<MessageBoxButton>(),
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );

        _bdCritMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), _bCritMessage, _bCritTitle, IsAny<MessageBoxButton>(),
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [Given(@"b\.e\. 나중에 버튼은 확인 버튼만 나타났는지 확인한다\.")]
    public void 조건B_E_나중에버튼은확인버튼만나타났는지확인한다_()
    {
        _beWarnMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), _bWarnButton,
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );

        _beCritMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), _bCritButton,
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [When(@"b\.f\. 오류 표시를 위한 메시지 박스를 띄우는 메서드를 호출하면")]
    public void WhenB_F_오류표시를위한메시지박스를띄우는메서드를호출하면()
    {
        _bcWarnMocker!.CreateInstance<AppMessageBox>().DisplayError(_bWarnMessage, false);
        _bdWarnMocker!.CreateInstance<AppMessageBox>().DisplayError(_bWarnMessage, false);
        _beWarnMocker!.CreateInstance<AppMessageBox>().DisplayError(_bWarnMessage, false);

        _bcCritMocker!.CreateInstance<AppMessageBox>().DisplayError(_bCritMessage, true);
        _bdCritMocker!.CreateInstance<AppMessageBox>().DisplayError(_bCritMessage, true);
        _beCritMocker!.CreateInstance<AppMessageBox>().DisplayError(_bCritMessage, true);
    }

    [Then(@"b\.g\. 의도한 대로 작동했는지 확인한다\.")]
    public void ThenB_G_의도한대로작동했는지확인한다_()
    {
        _bcWarnMocker!.VerifyAll();
        _bdWarnMocker!.VerifyAll();
        _beWarnMocker!.VerifyAll();

        _bcCritMocker!.VerifyAll();
        _bdCritMocker!.VerifyAll();
        _beCritMocker!.VerifyAll();
    }
}

partial class AppMessageBoxStepDefinitions
{
    private string _cWarnTitle = string.Empty;
    private Exception? _cWarnError;
    private MessageBoxButton _cWarnButton;

    private string _cCritTitle = string.Empty;
    private Exception? _cCritError;
    private MessageBoxButton _cCritButton;

    [Given(@"c\.a\. 다음과 같이 메시지 박스에 나타낼 문자열을 사용하여 예외 개체를 준비한다\.")]
    public void GivenC_A_다음과같이메시지박스에나타낼문자열을사용하여예외개체를준비한다_(Table table)
    {
        _cWarnTitle = table.Rows[0][1];
        _cWarnError = new ApplicationException(table.Rows[0][2]);
        _cWarnButton = MessageBoxButton.OK;

        _cCritTitle = table.Rows[1][1];
        _cCritError = new ApplicationException(table.Rows[1][2]);
        _cCritButton = MessageBoxButton.OK;
    }

    private AutoMocker? _ccWarnMocker;
    private AutoMocker? _cdWarnMocker;
    private AutoMocker? _ceWarnMocker;
    private AutoMocker? _ccCritMocker;
    private AutoMocker? _cdCritMocker;
    private AutoMocker? _ceCritMocker;

    [Given(@"c\.b\. 기능 동작에 필요한 내부 컴포넌트들이 Mockup으로 설정되어 있다\.")]
    public void GivenC_B_기능동작에필요한내부컴포넌트들이Mockup으로설정되어있다_()
    {
        _ccWarnMocker = new AutoMocker(MockBehavior.Loose);
        _ccWarnMocker.Use(ScenarioDependencies.SharedApplication);

        _cdWarnMocker = new AutoMocker(MockBehavior.Loose);
        _cdWarnMocker.Use(ScenarioDependencies.SharedApplication);

        _ceWarnMocker = new AutoMocker(MockBehavior.Loose);
        _ceWarnMocker.Use(ScenarioDependencies.SharedApplication);

        _ccCritMocker = new AutoMocker(MockBehavior.Loose);
        _ccCritMocker.Use(ScenarioDependencies.SharedApplication);

        _cdCritMocker = new AutoMocker(MockBehavior.Loose);
        _cdCritMocker.Use(ScenarioDependencies.SharedApplication);

        _ceCritMocker = new AutoMocker(MockBehavior.Loose);
        _ceCritMocker.Use(ScenarioDependencies.SharedApplication);
    }

    [Given(@"c\.c\. 나중에 심각도 수준 지정에 따라 아이콘의 모양이 다르게 나타났는지 확인한다\.")]
    public void GivenC_C_나중에심각도수준지정에따라아이콘의모양이다르게나타났는지확인한다_()
    {
        _ccWarnMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), IsAny<MessageBoxButton>(),
            MessageBoxImage.Warning, IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );

        _ccCritMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), IsAny<MessageBoxButton>(),
            MessageBoxImage.Stop, IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [Given(@"c\.d\. 나중에 창 제목과 본문이 설정한대로 나타났는지 확인한다\.")]
    public void GivenC_D_나중에창제목과본문이설정한대로나타났는지확인한다_()
    {
        var warnMessage = StringResources.TableCloth_UnwrapException(_cWarnError);
        _cdWarnMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), warnMessage, _cWarnTitle, IsAny<MessageBoxButton>(),
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );

        var critMessage = StringResources.TableCloth_UnwrapException(_cCritError);
        _cdCritMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), critMessage, _cCritTitle, IsAny<MessageBoxButton>(),
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [Given(@"c\.e\. 나중에 버튼은 확인 버튼만 나타났는지 확인한다\.")]
    public void GivenC_E_나중에버튼은확인버튼만나타났는지확인한다_()
    {
        _ceWarnMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), _cWarnButton,
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );

        _ceCritMocker!.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), IsAny<string>(), IsAny<string>(), _cCritButton,
            IsAny<MessageBoxImage>(), IsAny<MessageBoxResult>(), default) == IsAny<MessageBoxResult>()
        );
    }

    [When(@"c\.f\. 예외 정보 표시를 위한 메시지 박스를 띄우는 메서드를 호출하면")]
    public void WhenC_F_예외정보표시를위한메시지박스를띄우는메서드를호출하면()
    {
        _ccWarnMocker!.CreateInstance<AppMessageBox>().DisplayError(_cWarnError, false);
        _cdWarnMocker!.CreateInstance<AppMessageBox>().DisplayError(_cWarnError, false);
        _ceWarnMocker!.CreateInstance<AppMessageBox>().DisplayError(_cWarnError, false);

        _ccCritMocker!.CreateInstance<AppMessageBox>().DisplayError(_cCritError, true);
        _cdCritMocker!.CreateInstance<AppMessageBox>().DisplayError(_cCritError, true);
        _ceCritMocker!.CreateInstance<AppMessageBox>().DisplayError(_cCritError, true);
    }

    [Then(@"c\.g\. 의도한 대로 작동했는지 확인한다\.")]
    public void ThenC_G_의도한대로작동했는지확인한다_()
    {
        _ccWarnMocker!.VerifyAll();
        _cdWarnMocker!.VerifyAll();
        _ceWarnMocker!.VerifyAll();

        _ccCritMocker!.VerifyAll();
        _cdCritMocker!.VerifyAll();
        _ceCritMocker!.VerifyAll();
    }

}
