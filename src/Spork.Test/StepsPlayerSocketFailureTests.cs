using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spork.Steps.Implementations;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;

namespace Spork.Test
{
    /// <summary>
    /// 소켓/연결 수준 다운로드 실패(연결 거부·리셋, 스트림 중간 끊김 등)가 일시적 오류로 분류되어
    /// 재시도 대상이 되는지 검증한다. Spork의 실제 다운로드 경로(PackageInstallStep → HttpClient.GetAsync
    /// + ReadAsStreamAsync)에서 소켓 오류는 아래 형태로 표면화되므로 그 형태를 그대로 재현한다.
    ///  - 연결 수립 단계 실패     → HttpRequestException (StatusCode 없음), 내부는 SocketException
    ///  - 다운로드(스트리밍) 끊김 → IOException (내부는 SocketException) / .NET 8+ HttpIOException : IOException
    /// </summary>
    [TestClass]
    public class StepsPlayerSocketFailureTests
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

        // ── 연결 수립 단계: HttpClient는 소켓 실패를 StatusCode 없는 HttpRequestException으로 던진다 ──

        [TestMethod]
        public void ConnectionFailure_HttpRequestExceptionWithoutStatusCode_IsTransient()
        {
            // 연결 거부/DNS 실패 등 HTTP 응답 자체를 받지 못한 경우 StatusCode == null.
            var exception = new HttpRequestException("No such host is known.");
            Assert.IsTrue(IsTransient(exception));
        }

        [TestMethod]
        public void ConnectionReset_HttpRequestExceptionWrappingSocketException_IsTransient()
        {
            // 실제 HttpClient가 연결 리셋을 감싸는 형태: StatusCode 없음 + inner SocketException.
            var socket = new SocketException((int)SocketError.ConnectionReset);
            var exception = new HttpRequestException("connection reset by peer", socket);
            Assert.IsTrue(IsTransient(exception));
        }

        // ── 다운로드(스트리밍) 중간 끊김: IOException으로 표면화 ──

        [TestMethod]
        public void MidStreamDrop_IOException_IsTransient()
        {
            var exception = new IOException("Unable to read data from the transport connection.");
            Assert.IsTrue(IsTransient(exception));
        }

        [TestMethod]
        public void MidStreamDrop_IOExceptionWrappingSocketException_IsTransient()
        {
            // "An existing connection was forcibly closed by the remote host." (WSAECONNABORTED)
            var socket = new SocketException((int)SocketError.ConnectionAborted);
            var exception = new IOException("forcibly closed", socket);
            Assert.IsTrue(IsTransient(exception));
        }

        // ── 재귀적 InnerException 검사: 분류 대상이 아닌 래퍼가 일시적 내부 예외를 감싸도 잡힌다 ──

        [TestMethod]
        public void WrappedTransientInner_ViaRecursion_IsTransient()
        {
            // 겉은 분류 대상이 아니지만(Exception) 내부가 IOException → 재귀로 true.
            var exception = new Exception("aggregate-like wrapper", new IOException("socket closed mid-stream"));
            Assert.IsTrue(IsTransient(exception));
        }

        // ── 래핑되지 않은 순수 SocketException도 명시 검사로 일시적으로 취급된다 ──
        //     (HttpClient는 보통 래핑하지만, 다른 다운로드 계층/핸들러가 순수 SocketException을 던져도 재시도.)

        [TestMethod]
        [DataRow((int)SocketError.ConnectionReset)]     // 연결 리셋
        [DataRow((int)SocketError.ConnectionRefused)]   // 연결 거부
        [DataRow((int)SocketError.ConnectionAborted)]   // 연결 강제 종료
        [DataRow((int)SocketError.TimedOut)]            // 타임아웃
        [DataRow((int)SocketError.HostUnreachable)]     // 호스트 도달 불가
        [DataRow((int)SocketError.HostNotFound)]        // DNS 해석 실패
        public void BareSocketException_IsTransient(int socketErrorCode)
        {
            var exception = new SocketException(socketErrorCode);
            Assert.IsTrue(IsTransient(exception));
        }
    }
}
