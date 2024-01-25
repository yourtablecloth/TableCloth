using System;

namespace TableCloth.Models
{
    public struct ApiInvokeResult<TResult>
    {
        public ApiInvokeResult(TResult
#if !NETFX
            ?
#endif
            result)
        {
            _result = result;
            _thrownException = default;
        }

        public ApiInvokeResult(Exception
#if !NETFX
            ?
#endif
            thrownException)
        {
            _result = default;
            _thrownException = thrownException;
        }

        private readonly TResult
#if !NETFX
            ?
#endif
            _result;
        private readonly Exception
#if !NETFX
            ?
#endif
            _thrownException;

        public TResult
#if !NETFX
            ?
#endif
            Result => _result;

        public Exception
#if !NETFX
            ?
#endif
            ThrownException => _thrownException;

        public static implicit operator TResult
#if !NETFX
            ?
#endif
            (ApiInvokeResult<TResult> item)
            => item._result;

        public static implicit operator ApiInvokeResult<TResult>(TResult
#if !NETFX
            ?
#endif
            result)
            => new ApiInvokeResult<TResult>(result);
    }
}
