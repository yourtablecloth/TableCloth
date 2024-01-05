using TableCloth.Models.Catalog;
using TableCloth.SpecFlow.Support;

namespace TableCloth.SpecFlow.StepDefinitions;

[Binding]
public partial class ResourceResolverStepDefinitions(
    IResourceResolver resourceResolver) { }

partial class ResourceResolverStepDefinitions
{
    private CatalogDocument? _catalogDocument;

    [When(@"a\.a\. 카탈로그 문서를 불러오는 함수를 호출하면")]
    public async Task When카탈로그문서를불러오는함수를호출하면()
    {
        _catalogDocument = await resourceResolver.DeserializeCatalogAsync();
    }

    [Then(@"a\.b\. 카탈로그 문서에 (.*)개 이상의 사이트 정보가 들어있다\.")]
    public void Then카탈로그문서에개이상의사이트정보가들어있다_(int p0)
    {
        Assert.NotNull(_catalogDocument);
        Assert.True(_catalogDocument!.Services.Count >= p0);
    }

    [Then(@"a\.c\. 마지막으로 카탈로그를 불러온 날짜와 시간 정보를 확인할 수 있다\.")]
    public void Then마지막으로카탈로그를불러온날짜와시간정보를확인할수있다_()
    {
        Assert.True(resourceResolver.CatalogLastModified.HasValue);
    }
}

partial class ResourceResolverStepDefinitions
{
    private string _owner = string.Empty;
    private string _repositoryName = string.Empty;
    private string? _latestVersion;

    [Given(@"b\.a\. 다음의 리포지터리에서 정보를 가져오려 한다\.")]
    public void Given다음의리포지터리에서정보를가져오려한다_(Table table)
    {
        _owner = table.Rows[0][0];
        _repositoryName = table.Rows[0][1];
    }

    [When(@"b\.b\. 버전 정보를 가져오는 함수를 호출하면")]
    public async Task When버전정보를가져오는함수를호출하면()
    {
        _latestVersion = await resourceResolver.GetLatestVersion(_owner, _repositoryName);
    }

    [Then(@"b\.c\. GitHub에 출시한 최신 버전 정보를 반환한다\.")]
    public void ThenGitHub에출시한최신버전정보를반환한다_()
    {
        Assert.NotNull(_latestVersion);
        Assert.NotEmpty(_latestVersion);
    }
}
