using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IPageArgument<T>
    {
        T Arguments { get; set; }
    }
}
