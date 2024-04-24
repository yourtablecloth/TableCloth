#nullable enable

using System;

namespace TableCloth.Models
{
    public struct ApiInvokeResult<TResult>
    {
        public ApiInvokeResult(TResult? result)
        {
            _result = result;
            _thrownException = default;
        }

        public ApiInvokeResult(Exception? thrownException)
        {
            _result = default;
            _thrownException = thrownException;
        }

        private readonly TResult? _result;
        private readonly Exception? _thrownException;

        public TResult? Result => _result;

        public Exception? ThrownException => _thrownException;

        public static implicit operator TResult? (ApiInvokeResult<TResult> item)
            => item._result;

        public static implicit operator ApiInvokeResult<TResult>(TResult? result)
            => new ApiInvokeResult<TResult>(result);
    }
}
