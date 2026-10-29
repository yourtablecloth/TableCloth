using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillNetworkProxyTransportTests
{
    private static readonly Uri PublicApi = new("https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/");

    [TestMethod]
    public async Task ConfiguredProxyCarriesHttpsWithOriginTlsValidationAndNoCredentials()
    {
        using var fixture = new InstallFixture();
        await using var proxy = new ConnectTlsProxy();
        var selection = new MutableProxy(proxy.Address)
        {
            ConfiguredCredentials = new NetworkCredential("synthetic-unused-user", "synthetic-unused-password")
        };
        var resolved = new List<string>();
        await using var bridge = new SkillNetworkBridge(fixture.Paths.Root, [PublicApi.IdnHost],
            useSystemProxy: true, transportTestOptions: new(selection, Resolve, proxy.TrustPolicy));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var reply = await FetchWithServerDiagnosticsAsync(bridge, proxy, deadline.Token);
        await proxy.Completion.WaitAsync(deadline.Token);

        Assert.AreEqual(200, reply.Status);
        Assert.AreEqual(ConnectTlsProxy.ResponseBody, Encoding.UTF8.GetString(reply.Body));
        Assert.AreEqual("application/json", reply.Headers["Content-Type"]);
        StringAssert.StartsWith(proxy.ConnectRequest!, "CONNECT ecos.bok.or.kr:443 HTTP/1.1\r\n");
        StringAssert.Contains(proxy.OriginRequest!, "GET /api/KeyStatisticList/sample/json/kr/1/1/ HTTP/1.1\r\n");
        Assert.IsFalse(proxy.ConnectRequest!.Contains("Proxy-Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(proxy.OriginRequest!.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, selection.CredentialReads, "Selecting a host proxy must never read or copy its credentials.");
        Assert.Contains(PublicApi.IdnHost, resolved, "The API origin still needs a public DNS result before a proxy tunnel is opened.");
        Assert.Contains(IPAddress.Loopback.ToString(), resolved, "Only the selected host proxy may resolve to loopback.");
        Assert.IsNull(proxy.ServerFailure);

        Task<IPAddress[]> Resolve(string host, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            resolved.Add(host);
            return Task.FromResult(new[] { host == PublicApi.IdnHost ? IPAddress.Parse("1.1.1.1") : IPAddress.Loopback });
        }
    }

    [TestMethod]
    public async Task ProxySelectionIsAnImmutableSnapshotForTheWorkerLifetime()
    {
        using var fixture = new InstallFixture();
        await using var proxy = new ConnectTlsProxy();
        var selection = new MutableProxy(proxy.Address);
        await using var bridge = new SkillNetworkBridge(fixture.Paths.Root, [PublicApi.IdnHost],
            useSystemProxy: true, transportTestOptions: new(selection, ResolvePublicAndProxy, proxy.TrustPolicy));
        var initialSelectionCalls = selection.SelectionCalls;
        selection.Address = new Uri("http://127.0.0.1:1/");
        selection.Bypass = true;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var reply = await FetchWithServerDiagnosticsAsync(bridge, proxy, deadline.Token);
        await proxy.Completion.WaitAsync(deadline.Token);

        Assert.AreEqual(200, reply.Status);
        Assert.IsGreaterThan(0, initialSelectionCalls);
        Assert.AreEqual(initialSelectionCalls, selection.SelectionCalls,
            "An administrator proxy implementation must not be consulted again while a worker is running.");
        Assert.IsNotNull(proxy.ConnectRequest);
        Assert.IsNull(proxy.ServerFailure);
    }

    [TestMethod]
    [DataRow("hostname")]
    [DataRow("expired")]
    [DataRow("untrusted")]
    public async Task ProxyDoesNotDisableHostnameExpirationOrIssuerValidation(string invalidCertificate)
    {
        using var fixture = new InstallFixture();
        await using var proxy = new ConnectTlsProxy(invalidCertificate);
        await using var bridge = new SkillNetworkBridge(fixture.Paths.Root, [PublicApi.IdnHost],
            useSystemProxy: true, transportTestOptions: new(new MutableProxy(proxy.Address), ResolvePublicAndProxy, proxy.TrustPolicy));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => bridge.FetchAsync(PublicApi, deadline.Token));
        await proxy.Completion.WaitAsync(deadline.Token);

        Assert.IsTrue(ContainsAuthenticationFailure(failure),
            "The negative control must fail certificate validation, rather than DNS or TCP setup.\n" +
            FailureDetails(failure, proxy));
        Assert.IsNotNull(proxy.ConnectRequest, "A real CONNECT tunnel must precede the rejected TLS handshake.");
        Assert.IsNull(proxy.OriginRequest, "No application HTTP request may pass a rejected certificate.");
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("10.1.2.3")]
    [DataRow("::1")]
    [DataRow("mixed")]
    public async Task ATrustedProxyDoesNotPermitPrivateOrMixedOriginDns(string address)
    {
        using var fixture = new InstallFixture();
        var resolved = new List<string>();
        var selection = new MutableProxy(new Uri("http://127.0.0.1:1/"));
        await using var bridge = new SkillNetworkBridge(fixture.Paths.Root, [PublicApi.IdnHost],
            useSystemProxy: true, transportTestOptions: new(selection, Resolve));

        await Assert.ThrowsAsync<BrokerValidationException>(() => bridge.FetchAsync(PublicApi, CancellationToken.None));

        CollectionAssert.AreEqual(new[] { PublicApi.IdnHost }, resolved,
            "The private origin must be rejected before resolving or connecting to the trusted proxy endpoint.");

        Task<IPAddress[]> Resolve(string host, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            resolved.Add(host);
            return Task.FromResult(address == "mixed"
                ? new[] { IPAddress.Parse("1.1.1.1"), IPAddress.Parse("192.168.1.1") }
                : new[] { IPAddress.Parse(address) });
        }
    }

    [TestMethod]
    [DataRow("http://synthetic-user:synthetic-password@127.0.0.1:8080/")]
    [DataRow("http://127.0.0.1:8080/non-root")]
    [DataRow("http://127.0.0.1:8080/?query=value")]
    [DataRow("http://127.0.0.1:8080/#fragment")]
    [DataRow("ftp://127.0.0.1:8080/")]
    [DataRow("relative-proxy")]
    public void ProxySnapshotRejectsNonEndpointData(string address)
    {
        using var fixture = new InstallFixture();
        var selection = new MutableProxy(new Uri(address, UriKind.RelativeOrAbsolute));

        Assert.Throws<BrokerValidationException>(() => new SkillNetworkBridge(fixture.Paths.Root, [PublicApi.IdnHost],
            useSystemProxy: true, transportTestOptions: new(selection)));

        Assert.AreEqual(0, selection.CredentialReads);
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Paths.Root, "network")),
            "Reject a malformed proxy before creating the worker exchange files.");
    }

    [TestMethod]
    public async Task DisabledProxyPolicyDoesNotConsultConfiguredProxy()
    {
        using var fixture = new InstallFixture();
        var selection = new MutableProxy(new Uri("http://synthetic-user:synthetic-password@127.0.0.1:8080/"));
        await using var bridge = new SkillNetworkBridge(fixture.Paths.Root, [PublicApi.IdnHost],
            useSystemProxy: false, transportTestOptions: new(selection,
                (_, _) => Task.FromResult(new[] { IPAddress.Loopback })));

        await Assert.ThrowsAsync<BrokerValidationException>(() => bridge.FetchAsync(PublicApi, CancellationToken.None));

        Assert.AreEqual(0, selection.SelectionCalls);
        Assert.AreEqual(0, selection.CredentialReads);
    }

    private static Task<IPAddress[]> ResolvePublicAndProxy(string host, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new[] { host == PublicApi.IdnHost ? IPAddress.Parse("1.1.1.1") : IPAddress.Loopback });
    }

    private static bool ContainsAuthenticationFailure(Exception failure)
        => failure is AuthenticationException || failure.InnerException is { } inner && ContainsAuthenticationFailure(inner);

    private static async Task<SkillNetworkBridge.Reply> FetchWithServerDiagnosticsAsync(
        SkillNetworkBridge bridge, ConnectTlsProxy proxy, CancellationToken token)
    {
        try { return await bridge.FetchAsync(PublicApi, token); }
        catch (Exception clientFailure)
        {
            await proxy.Completion.WaitAsync(token);
            throw new InvalidOperationException(FailureDetails(clientFailure, proxy), clientFailure);
        }
    }

    private static string FailureDetails(Exception clientFailure, ConnectTlsProxy proxy)
        => "Synthetic CONNECT/TLS client failure: " + clientFailure + "\nSynthetic proxy failure: " +
            (proxy.ServerFailure?.ToString() ?? "none") + "\nCONNECT received: " + (proxy.ConnectRequest is not null);

    private sealed class MutableProxy(Uri address) : IWebProxy
    {
        internal Uri Address { get; set; } = address;
        internal bool Bypass { get; set; }
        internal ICredentials? ConfiguredCredentials { get; init; }
        internal int SelectionCalls { get; private set; }
        internal int CredentialReads { get; private set; }
        public ICredentials? Credentials
        {
            get { CredentialReads++; return ConfiguredCredentials; }
            set => throw new NotSupportedException();
        }
        public Uri GetProxy(Uri destination) { SelectionCalls++; return Address; }
        public bool IsBypassed(Uri host) { SelectionCalls++; return Bypass; }
    }

    // A local synthetic CONNECT proxy terminates HTTPS using only ephemeral test
    // certificates. No certificate store, environment or global proxy is changed.
    private sealed class ConnectTlsProxy : IAsyncDisposable
    {
        internal const string ResponseBody = "{\"synthetic\":true}";
        private readonly CancellationTokenSource _shutdown = new(TimeSpan.FromSeconds(20));
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly X509Certificate2 _issuer;
        private readonly X509Certificate2 _leaf;
        private readonly X509Certificate2? _unrelatedRoot;
        internal Uri Address { get; }
        internal X509ChainPolicy TrustPolicy { get; }
        internal Task Completion { get; }
        internal string? ConnectRequest { get; private set; }
        internal string? OriginRequest { get; private set; }
        internal Exception? ServerFailure { get; private set; }

        internal ConnectTlsProxy(string? invalidCertificate = null)
        {
            _issuer = CreateRoot();
            _leaf = CreateLeaf(_issuer, invalidCertificate == "hostname" ? "wrong.example.invalid" : PublicApi.IdnHost,
                invalidCertificate == "expired");
            TrustPolicy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.NoFlag,
                DisableCertificateDownloads = true
            };
            if (invalidCertificate == "untrusted")
            {
                _unrelatedRoot = CreateRoot();
                TrustPolicy.CustomTrustStore.Add(_unrelatedRoot);
            }
            else TrustPolicy.CustomTrustStore.Add(_issuer);
            _listener.Start();
            Address = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/");
            Completion = ServeAsync();
        }

        private async Task ServeAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
                await using var connection = client.GetStream();
                ConnectRequest = await ReadHeadersAsync(connection, _shutdown.Token);
                await connection.WriteAsync("HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray(), _shutdown.Token);
                await using var tls = new SslStream(connection, leaveInnerStreamOpen: true);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = _leaf,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                }, _shutdown.Token);
                OriginRequest = await ReadHeadersAsync(tls, _shutdown.Token);
                var body = Encoding.UTF8.GetBytes(ResponseBody);
                var headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                await tls.WriteAsync(headers, _shutdown.Token);
                await tls.WriteAsync(body, _shutdown.Token);
                await tls.FlushAsync(_shutdown.Token);
            }
            catch (Exception failure)
            {
                // Negative TLS controls intentionally terminate the handshake. The
                // test verifies the client-side exception and application boundary.
                ServerFailure = failure;
            }
        }

        private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken token)
        {
            using var bytes = new MemoryStream();
            var next = new byte[1];
            var matched = 0;
            ReadOnlyMemory<byte> terminator = "\r\n\r\n"u8.ToArray();
            while (bytes.Length < 16384)
            {
                if (await stream.ReadAsync(next, token) == 0) throw new EndOfStreamException();
                bytes.WriteByte(next[0]);
                matched = next[0] == terminator.Span[matched] ? matched + 1 : next[0] == (byte)'\r' ? 1 : 0;
                if (matched == terminator.Length) return Encoding.ASCII.GetString(bytes.ToArray());
            }
            throw new IOException("Synthetic proxy header limit exceeded.");
        }

        private static X509Certificate2 CreateRoot()
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=TableCloth synthetic proxy CA " + Guid.NewGuid().ToString("N"), key,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow.AddYears(1));
        }

        private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string host, bool expired)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=" + host, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(host);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
            var notBefore = DateTimeOffset.UtcNow.AddDays(expired ? -3 : -1);
            var notAfter = DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1);
            using var certificate = request.Create(issuer, notBefore, notAfter, RandomNumberGenerator.GetBytes(16));
            using var withKey = certificate.CopyWithPrivateKey(key);
            // Windows Schannel server credentials can reject CopyWithPrivateKey's
            // ephemeral RSA handle. Import a temporary user key container instead.
            // No trust-store registration or PersistKeySet flag is used; disposing
            // this certificate releases the temporary imported key container.
            var pfx = withKey.Export(X509ContentType.Pkcs12);
            try { return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.UserKeySet); }
            finally { CryptographicOperations.ZeroMemory(pfx); }
        }

        public async ValueTask DisposeAsync()
        {
            await _shutdown.CancelAsync();
            _listener.Stop();
            try { await Completion; }
            finally
            {
                _leaf.Dispose();
                _issuer.Dispose();
                _unrelatedRoot?.Dispose();
                _shutdown.Dispose();
            }
        }
    }
}
