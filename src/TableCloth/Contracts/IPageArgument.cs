using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IPageArgument<T>
    {
        IEnumerable<T> Arguments { get; set; }
    }
}
