using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spork.Steps.Implementations;
using System;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace Spork.Test
{
    /// <summary>
    /// HTTP 상태 코드 기준 재시도 판정과, "재시도하지 않을 종류"(비일시적)가 잘 건너뛰어지는지를 검증한다.
    /// 재시도 대상: 408/429 + 일시적 5xx(500/502/503/504). 건너뜀: 4xx 대부분 + 영구성 5xx(501/505 등)
    /// 및 요청/구성 오류성 예외 타입.
    /// </summary>
    [TestClass]
    public class StepsPlayerStatusCodeClassificationTests
    {
        private static readonly MethodInfo IsTransientDownloadExceptionMethod = typeof(StepsPlayer)
            .GetMethod("IsTransientDownloadException", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static bool IsTransient(Exception exception)
        {
            Assert.IsNotNull(IsTransientDownloadExceptionMethod);
            var result = IsTransientDownloadExceptionMethod.Invoke(null, [exception]);
            Assert.IsNotNull(result);
            return (bool)result;
        }

        // ── 재시도 대상 상태 코드 ──
        [TestMethod]
        [DataRow(408)] // Request Timeout
        [DataRow(429)] // Too Many Requests
        [DataRow(500)] // Internal Server Error
        [DataRow(502)] // Bad Gateway
        [DataRow(503)] // Service Unavailable
        [DataRow(504)] // Gateway Timeout
        public void RetryableStatusCode_IsTransient(int statusCode)
        {
            var exception = new HttpRequestException("server-side failure", null, (HttpStatusCode)statusCode);
            Assert.IsTrue(IsTransient(exception));
        }

        // ── 재시도하지 않고 건너뛸 상태 코드(4xx 대부분 + 영구성 5xx) ──
        [TestMethod]
        [DataRow(400)] // Bad Request
        [DataRow(401)] // Unauthorized
        [DataRow(403)] // Forbidden
        [DataRow(404)] // Not Found
        [DataRow(405)] // Method Not Allowed
        [DataRow(409)] // Conflict
        [DataRow(410)] // Gone
        [DataRow(501)] // Not Implemented (영구성 5xx → 재시도 안 함)
        [DataRow(505)] // HTTP Version Not Supported (영구성 5xx)
        public void NonRetryableStatusCode_IsSkipped(int statusCode)
        {
            var exception = new HttpRequestException("permanent/client failure", null, (HttpStatusCode)statusCode);
            Assert.IsFalse(IsTransient(exception));
        }

        // ── 재시도하지 않을 예외 타입들이 잘 건너뛰어지는지 ──
        [TestMethod]
        public void InvalidOperationException_IsSkipped()
            => Assert.IsFalse(IsTransient(new InvalidOperationException("permanent")));

        [TestMethod]
        public void ArgumentException_IsSkipped()
            => Assert.IsFalse(IsTransient(new ArgumentException("bad argument")));

        [TestMethod]
        public void NotSupportedException_IsSkipped()
            => Assert.IsFalse(IsTransient(new NotSupportedException("unsupported")));

        [TestMethod]
        public void UnauthorizedAccessException_IsSkipped()
            => Assert.IsFalse(IsTransient(new UnauthorizedAccessException("denied")));

        // 비일시적 예외가 비일시적 예외를 감싸는 경우에도 재귀 결과가 false여서 건너뛰어져야 한다.
        [TestMethod]
        public void NonTransientWrappingNonTransient_IsSkipped()
        {
            var exception = new InvalidOperationException("outer", new ArgumentException("inner"));
            Assert.IsFalse(IsTransient(exception));
        }
    }
}
