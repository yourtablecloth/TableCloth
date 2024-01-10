using TableCloth.Models.Catalog;

namespace TableCloth.SpecFlow.StepDefinitions;

[Binding]
public partial class ResourceResolverStepDefinitions(
    IResourceResolver resourceResolver)
{ }

partial class ResourceResolverStepDefinitions
{
    private CatalogDocument? _aCatalogDocument;

    [When(@"a\.a\. 카탈로그 문서를 불러오는 함수를 호출하면")]
    public async Task WhenA_A_카탈로그문서를불러오는함수를호출하면()
    {
        _aCatalogDocument = await resourceResolver.DeserializeCatalogAsync();
    }

    [Then(@"a\.b\. 카탈로그 문서에 (.*)개 이상의 사이트 정보가 들어있다\.")]
    public void ThenA_B_카탈로그문서에개이상의사이트정보가들어있다_(int p0)
    {
        Assert.NotNull(_aCatalogDocument);
        Assert.True(_aCatalogDocument!.Services.Count >= p0);
    }

    [Then(@"a\.c\. 마지막으로 카탈로그를 불러온 날짜와 시간 정보를 확인할 수 있다\.")]
    public void ThenA_C_마지막으로카탈로그를불러온날짜와시간정보를확인할수있다_()
    {
        Assert.True(resourceResolver.CatalogLastModified.HasValue);
    }
}

partial class ResourceResolverStepDefinitions
{
    private string _bOwner = string.Empty;
    private string _bRepositoryName = string.Empty;
    private string? _bLatestVersion;

    [Given(@"b\.a\. 다음의 리포지터리에서 정보를 가져오려 한다\.")]
    public void GivenB_A_다음의리포지터리에서정보를가져오려한다_(Table table)
    {
        _bOwner = table.Rows[0][0];
        _bRepositoryName = table.Rows[0][1];
    }

    [When(@"b\.b\. 버전 정보를 가져오는 함수를 호출하면")]
    public async Task WhenB_B_버전정보를가져오는함수를호출하면()
    {
        _bLatestVersion = await resourceResolver.GetLatestVersion(_bOwner, _bRepositoryName);
    }

    [Then(@"b\.c\. GitHub에 출시한 최신 버전 정보를 반환한다\.")]
    public void ThenGitHubB_C_에출시한최신버전정보를반환한다_()
    {
        Assert.NotNull(_bLatestVersion);
        Assert.NotEmpty(_bLatestVersion);
    }
}

partial class ResourceResolverStepDefinitions
{
    private string _cOwner = string.Empty;
    private string _cRepositoryName = string.Empty;
    private Uri? _cUrl;

    [Given(@"c\.a\. 다음의 리포지터리에서 정보를 가져오려 한다\.")]
    public void GivenC_A_다음의리포지터리에서정보를가져오려한다_(Table table)
    {
        _cOwner = table.Rows[0][0];
        _cRepositoryName = table.Rows[0][1];
    }

    [When(@"c\.b\. 다운로드 URL을 가져오는 함수를 호출하면")]
    public async Task WhenC_B_다운로드URL을가져오는함수를호출하면()
    {
        _cUrl = await resourceResolver.GetDownloadUrl(_cOwner, _cRepositoryName);
    }

    [Then(@"c\.c\. GitHub에서 최신 버전의 리소스를 다운로드할 수 있는 URL을 반환한다\.")]
    public void ThenC_C_GitHub에서최신버전의리소스를다운로드할수있는URL을반환한다_()
    {
        Assert.NotNull(_cUrl);
        Assert.Equal(Uri.UriSchemeHttps, _cUrl?.Scheme);
    }
}