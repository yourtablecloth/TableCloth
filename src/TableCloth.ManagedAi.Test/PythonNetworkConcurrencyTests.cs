using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class PythonNetworkConcurrencyTests
{
    [TestMethod]
    public async Task RegistrySelectsTheProductionNetworkBootstrapForTheContainedConcurrencyFixture()
    {
        using var fixture = new InstallFixture();
        var root = fixture.Paths.Under("skill-execution");
        var package = Path.Combine(root, "concurrency-fixture");
        Directory.CreateDirectory(package);
        var python = Path.Combine(root, "python.exe");
        File.WriteAllBytes(python, [0x54, 0x43, 0x50, 0x00]);
        var script = Path.Combine(package, "concurrent-urlopen.py");
        File.WriteAllText(script, ConcurrentRequestsFixture);
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var registration = new SkillRegistration("python-network-concurrency", "python-script", script, Hash(script), [], [], true,
            package, [new(script, Hash(script))], "Contained synthetic request/reply concurrency regression.", ["ecos.bok.or.kr"]);
        var manifest = Path.Combine(root, "manifest.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new EvaluationManifest(root, python, Hash(python), null, null, [registration]),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
        SandboxRunRequest? observed = null;
        var registry = await SkillExecutionRegistry.LoadAsync(manifest, (request, _) =>
        {
            observed = request;
            // This seam verifies production routing and containment requirements only.
            // The Python concurrency assertions below are executed by the native smoke
            // fixture, not simulated or counted as passed by this managed unit test.
            return Task.FromResult(new SandboxRunResult(0, "fixture-not-executed-by-offline-test", "", false, false, true));
        }, CancellationToken.None);

        var result = await registry.ExecuteAsync("python-network-concurrency",
            JsonSerializer.SerializeToElement(new { arguments = Array.Empty<string>(), stdin = "{\"legacyControl\":false}" }), CancellationToken.None);

        Assert.IsNotNull(observed);
        Assert.AreEqual("completed", result.Status);
        Assert.AreEqual(python, observed.Executable);
        CollectionAssert.AreEqual(new[] { "-I", "-S", "-B", "-X", "utf8", "-c", SkillExecutionRegistry.PythonNetworkPackageBootstrap,
            script, package, Path.Combine(observed.WorkingDirectory, "network") }, observed.Arguments.ToArray());
        Assert.IsFalse(observed.AllowNetwork, "The test fixture and normal workers must not receive direct network capability.");
        Assert.AreEqual("{\"legacyControl\":false}", observed.Stdin);
        CollectionAssert.AreEqual(new[] { observed.WorkingDirectory }, observed.ReadWriteDirectories.ToArray());
        Assert.Contains(package, observed.ReadOnlyDirectories);
        Assert.IsFalse(Directory.Exists(observed.WorkingDirectory), "The registry must clean its owned worker directory after completion.");
        Assert.AreEqual("fixture-not-executed-by-offline-test", result.Stdout);
    }

    // Run this fixed source only through the native AppContainer smoke path. A local
    // responder redirects the production bootstrap's RPC files inside the worker's
    // scratch directory. It makes no real network requests and needs no credentials.
    // legacyControl selects the pre-fix function solely for a negative control; all
    // function code and response data remain fixed test source, never model input.
    internal const string ConcurrentRequestsFixture = """
import base64,builtins,json,os,pathlib,threading,time,types,urllib.error,urllib.request

options=json.load(__import__('sys').stdin)
production_urlopen=urllib.request.urlopen
bootstrap=production_urlopen.__globals__
original_open=builtins.open
real_time=time
context=None

def legacy_urlopen(url, data=None, timeout=30, **kwargs):
    global _tc_sequence
    request=url if isinstance(url,urllib.request.Request) else urllib.request.Request(url,data=data)
    if data is not None or request.data is not None or request.get_method()!='GET':
        raise urllib.error.URLError('Only reviewed public GET requests are available')
    _tc_sequence+=1
    if _tc_sequence>20: raise urllib.error.URLError('Network request budget exceeded')
    payload={'id':_tc_sequence,'method':'GET','url':request.full_url,'headers':dict(request.header_items())}
    encoded=json.dumps(payload,ensure_ascii=False,separators=(',',':'))
    if len(encoded.encode('utf-8'))>16384: raise urllib.error.URLError('Network request exceeds its budget')
    with open(os.path.join(_tc_network,'request.json'),'w',encoding='utf-8') as stream:
        stream.write(encoded)
        stream.flush()
    deadline=time.monotonic()+30
    while time.monotonic()<deadline:
        try:
            with open(os.path.join(_tc_network,'response.json'),'r',encoding='utf-8') as stream: reply=json.load(stream)
            if reply.get('id')==_tc_sequence:
                if 'error' in reply: raise urllib.error.URLError(reply['error'])
                response=_TcResponse(reply,request.full_url)
                if response.status>=400:
                    raise urllib.error.HTTPError(request.full_url,response.status,'Public API response',response.headers,response)
                return response
        except (json.JSONDecodeError,UnicodeDecodeError): pass
        time.sleep(0.01)
    raise urllib.error.URLError('Network bridge timeout')

class ObservedLock:
    def __init__(self, inner): self.inner=inner
    def __enter__(self):
        if context is not None and threading.current_thread().name=='second': context['second_boundary'].set()
        self.inner.acquire()
        return self
    def __exit__(self, *unused): self.inner.release()

class ObservedRequest:
    def __init__(self, inner): self.inner=inner
    def __enter__(self): self.inner.__enter__(); return self
    def __exit__(self, *values): return self.inner.__exit__(*values)
    def __getattr__(self, name): return getattr(self.inner,name)
    def flush(self):
        self.inner.flush()
        context['second_boundary'].set()

def observed_open(file, mode='r', *args, **kwargs):
    stream=original_open(file,mode,*args,**kwargs)
    if context is not None and 'w' in mode and threading.current_thread().name=='second' and os.path.abspath(file)==context['request']:
        return ObservedRequest(stream)
    return stream

bootstrap['_tc_exchange_lock']=ObservedLock(bootstrap.get('_tc_exchange_lock',threading.Lock()))
if options.get('legacyControl',False):
    urllib.request.urlopen=types.FunctionType(legacy_urlopen.__code__,bootstrap,'_tc_urlopen',legacy_urlopen.__defaults__)
builtins.open=observed_open

def setup(name):
    global context
    directory=pathlib.Path.cwd()/('synthetic-rpc-'+name)
    directory.mkdir()
    request=directory/'request.json'
    response=directory/'response.json'
    request.write_text('',encoding='utf-8')
    response.write_text('{}',encoding='utf-8')
    bootstrap['_tc_network']=str(directory)
    bootstrap['_tc_sequence']=0
    context={'request':str(request),'response':str(response),'first_seen':threading.Event(),
        'second_boundary':threading.Event(),'first_done':threading.Event(),'second_body_received':threading.Event()}
    return context

def read_request(state, after=0):
    deadline=real_time.monotonic()+5
    while real_time.monotonic()<deadline:
        try:
            with original_open(state['request'],encoding='utf-8') as stream: value=json.load(stream)
            if value.get('id',0)>after: return value
        except (json.JSONDecodeError,UnicodeDecodeError): pass
        real_time.sleep(0.001)
    raise RuntimeError('synthetic responder request deadline')

def reply(state, request, error=None, status=200):
    value={'id':request['id']}
    if error is not None: value['error']=error
    else:
        body=json.dumps({'url':request['url'],'requestId':request['id']},separators=(',',':')).encode('utf-8')
        value.update(status=status,headers={'Content-Type':'application/json'},body=base64.b64encode(body).decode('ascii'))
    with original_open(state['response'],'w',encoding='utf-8') as stream: json.dump(value,stream);stream.flush()

def run_pair(name, first_error=None, first_status=200):
    state=setup(name)
    urls={'first':'https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/',
        'second':'https://ecos.bok.or.kr/api/StatisticTableList/sample/json/kr/1/1/'}
    results={}
    responder_errors=[]
    disposal_check={}
    def responder():
        try:
            first=read_request(state)
            state['first_seen'].set()
            if not state['second_boundary'].wait(5): raise RuntimeError('second caller did not reach the exchange boundary')
            reply(state,first,first_error,first_status)
            state['first_done'].wait(0.2)
            second=read_request(state,first['id'])
            reply(state,second)
        except Exception as error: responder_errors.append(str(error))
    def fetch(label):
        try:
            with urllib.request.urlopen(urls[label]) as response:
                results[label]={'body':json.load(response)}
                if label=='second': state['second_body_received'].set()
                elif first_error is None and first_status<400:
                    disposal_check['secondFinishedBeforeFirstClose']=state['second_body_received'].wait(5)
        except Exception as error: results[label]={'error':str(error),'type':type(error).__name__}
        finally:
            if label=='first': state['first_done'].set()
    server=threading.Thread(target=responder,name='synthetic-responder',daemon=True)
    first=threading.Thread(target=lambda:fetch('first'),name='first',daemon=True)
    second=threading.Thread(target=lambda:fetch('second'),name='second',daemon=True)
    server.start();first.start()
    if not state['first_seen'].wait(5): raise RuntimeError('first request was not observed')
    second.start()
    first.join(6);second.join(6);server.join(6)
    finished=not first.is_alive() and not second.is_alive() and not server.is_alive() and not responder_errors
    own_first=results.get('first',{}).get('body',{}).get('url')==urls['first']
    own_second=results.get('second',{}).get('body',{}).get('url')==urls['second']
    error_expected=first_error is not None or first_status>=400
    first_result=results.get('first',{})
    if first_error is not None:
        first_ok=first_result.get('type')=='URLError' and first_error in first_result.get('error','')
    elif first_status>=400:
        first_ok=first_result.get('type')=='HTTPError' and str(first_status) in first_result.get('error','')
    else: first_ok=own_first
    disposal_ok=error_expected or disposal_check.get('secondFinishedBeforeFirstClose',False)
    return {'name':name,'passed':bool(finished and first_ok and own_second and disposal_ok),'finished':bool(finished),
        'firstExpectedError':error_expected,'ownFirstBody':own_first,'ownSecondBody':own_second,
        'crossTalkObserved':bool(not error_expected and own_second and results.get('first',{}).get('body',{}).get('url')==urls['second']),
        'responseDisposal':disposal_check,'results':results,'responderErrors':responder_errors}

def run_timeout_recovery():
    state=setup('timeout')
    class DeadlineClock:
        def __init__(self): self.calls=0
        def monotonic(self): self.calls+=1;return 0 if self.calls==1 else 31
        def sleep(self, duration): real_time.sleep(duration)
    bootstrap['time']=DeadlineClock()
    timed_out=False
    try:
        urllib.request.urlopen('https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/')
    except urllib.error.URLError as error: timed_out='Network bridge timeout' in str(error)
    finally: bootstrap['time']=real_time
    results={}
    errors=[]
    url='https://ecos.bok.or.kr/api/StatisticTableList/sample/json/kr/1/1/'
    def responder():
        try: reply(state,read_request(state,1))
        except Exception as error: errors.append(str(error))
    def fetch():
        try:
            with urllib.request.urlopen(url) as response: results['body']=json.load(response)
        except Exception as error: errors.append(str(error))
    server=threading.Thread(target=responder,name='timeout-responder',daemon=True)
    client=threading.Thread(target=fetch,name='timeout-second',daemon=True)
    server.start();client.start();server.join(6);client.join(6)
    passed=timed_out and not server.is_alive() and not client.is_alive() and not errors and results.get('body',{}).get('url')==url
    return {'name':'timeout-release','passed':bool(passed),'timeoutObserved':timed_out,'results':results,'errors':errors}

try:
    checks=[run_pair('correlation'),run_pair('urlerror',first_error='synthetic-first-error'),
        run_pair('httperror',first_status=503),run_timeout_recovery()]
    summary={'kind':'python-network-concurrency','legacyControl':bool(options.get('legacyControl',False)),
        'syntheticResponder':True,'realNetworkRequests':0,'passed':all(check['passed'] for check in checks),'checks':checks}
    print(json.dumps(summary,ensure_ascii=False,separators=(',',':')))
    raise SystemExit(0 if summary['passed'] else 1)
finally:
    builtins.open=original_open
    bootstrap['time']=real_time
""";
}
