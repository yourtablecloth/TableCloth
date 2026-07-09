using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spork.Steps.Implementations;
using System;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace Spork.Test
{
    [TestClass]
    public class StepsPlayerTransientDownloadTests
    {
        private static readonly MethodInfo IsTransientDownloadExceptionMethod = typeof(StepsPlayer)
            .GetMethod("IsTransientDownloadException", BindingFlags.NonPublic | BindingFlags.Static)!;

        [TestMethod]
        public void IsTransientDownloadException_ShouldReturnTrue_For503HttpRequestException()
        {
            var exception = new HttpRequestException("server error", null, HttpStatusCode.ServiceUnavailable);
            Assert.IsTrue(Invoke(exception));
        }

        [TestMethod]
        public void IsTransientDownloadException_ShouldReturnFalse_For404HttpRequestException()
        {
            var exception = new HttpRequestException("not found", null, HttpStatusCode.NotFound);
            Assert.IsFalse(Invoke(exception));
        }

        [TestMethod]
        public void IsTransientDownloadException_ShouldReturnTrue_ForTaskCanceledException()
        {
            Assert.IsTrue(Invoke(new TaskCanceledException("timeout")));
        }

        [TestMethod]
        public void IsTransientDownloadException_ShouldReturnFalse_ForInvalidOperationException()
        {
            Assert.IsFalse(Invoke(new InvalidOperationException("permanent failure")));
        }

        private static bool Invoke(Exception exception)
        {
            Assert.IsNotNull(IsTransientDownloadExceptionMethod);
            var result = IsTransientDownloadExceptionMethod.Invoke(null, [exception]);
            Assert.IsNotNull(result);
            return (bool)result;
        }
    }
}
