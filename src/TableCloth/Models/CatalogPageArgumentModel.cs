namespace TableCloth.Models;

public sealed class CatalogPageArgumentModel
{
    public CatalogPageArgumentModel(string searchKeyword)
    {
        SearchKeyword = searchKeyword ?? string.Empty;
    }

    public string SearchKeyword { get; private set; } = string.Empty;
}
