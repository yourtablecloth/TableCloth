namespace TableCloth.SpecFlow.StepDefinitions;

[Binding]
public class ResourceResolverStepDefinitions
{
    [ScenarioDependencies]
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddHttpClient();

        return services;
    }

    [Given(@"ResourceResolver를 초기화한다\.")]
    public void GivenResourceResolver를초기화한다_()
    {
        throw new PendingStepException();
    }

    [When(@"최신 정보를 가져오는 함수를 호출하면")]
    public void When최신정보를가져오는함수를호출하면()
    {
        throw new PendingStepException();
    }

    [Then(@"GitHub에 출시한 최신 버전 정보를 반환한다\.")]
    public void ThenGitHub에출시한최신버전정보를반환한다_()
    {
        throw new PendingStepException();
    }
}
