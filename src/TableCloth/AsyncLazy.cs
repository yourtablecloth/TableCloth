using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TableCloth;

// https://devblogs.microsoft.com/pfxteam/asynclazyt/

public sealed class AsyncLazy<T> : Lazy<Task<T>>
{
    public AsyncLazy(Func<T> valueFactory) :
        base(() => Task.Factory.StartNew(valueFactory))
    { }

    public AsyncLazy(Func<Task<T>> taskFactory) :
        base(() => Task.Factory.StartNew(() => taskFactory()).Unwrap())
    { }

    public TaskAwaiter<T> GetAwaiter()
        => Value.GetAwaiter();
}
