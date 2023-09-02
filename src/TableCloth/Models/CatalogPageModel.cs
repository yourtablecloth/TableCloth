namespace TableCloth.Models
{
    public sealed class CatalogPageModel
    {
        public CatalogPageModel(string searchKeyword)
        {
            SearchKeyword = searchKeyword ?? string.Empty;
        }

        public string SearchKeyword { get; private set; } = string.Empty;
    }
}
