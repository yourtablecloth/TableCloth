using System.Text;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

public sealed record SkillExecutionResult(string Status, int? ExitCode, string Stdout, string Stderr,
    bool WasAppContainer, bool TimedOut, bool OutputLimited, string? Failure)
{
    public bool IsError => Status != "completed";
}

public sealed class SkillExecutionRegistry
{
    // This constant is application code, never an MCP argument or installed skill payload. Embedded
    // Python's pinned ._pth file excludes script directories, so add only this registered package.
    internal const string PythonPackageBootstrap = "import os,runpy,sys; p=sys.argv[1]; r=sys.argv[2]; sys.argv=[p,*sys.argv[3:]]; sys.path[:0]=[os.path.dirname(p),r]; runpy.run_path(p,run_name='__main__')";
    internal const string PythonNetworkPackageBootstrap = """
import base64,email.message,io,json,os,sys,threading,time,urllib.error,urllib.request
_tc_network=sys.argv.pop(3)
_tc_sequence=0
_tc_exchange_lock=threading.Lock()
class _TcResponse(io.BytesIO):
    def __init__(self, reply, url):
        super().__init__(base64.b64decode(reply['body'], validate=True))
        self.status=reply['status']
        self.code=self.status
        self.url=url
        self.headers=email.message.Message()
        for k,v in reply.get('headers',{}).items(): self.headers[k]=v
    def getcode(self): return self.status
    def geturl(self): return self.url
    def info(self): return self.headers
    def getheader(self, name, default=None): return self.headers.get(name, default)
def _tc_urlopen(url, data=None, timeout=30, **kwargs):
    global _tc_sequence
    request=url if isinstance(url,urllib.request.Request) else urllib.request.Request(url,data=data)
    if data is not None or request.data is not None or request.get_method()!='GET':
        raise urllib.error.URLError('Only reviewed public GET requests are available')
    with _tc_exchange_lock:
        _tc_sequence+=1
        request_id=_tc_sequence
        if request_id>20: raise urllib.error.URLError('Network request budget exceeded')
        payload={'id':request_id,'method':'GET','url':request.full_url,'headers':dict(request.header_items())}
        encoded=json.dumps(payload,ensure_ascii=False,separators=(',',':'))
        if len(encoded.encode('utf-8'))>16384: raise urllib.error.URLError('Network request exceeds its budget')
        with open(os.path.join(_tc_network,'request.json'),'w',encoding='utf-8') as stream:
            stream.write(encoded)
            stream.flush()
        deadline=time.monotonic()+30
        while time.monotonic()<deadline:
            try:
                with open(os.path.join(_tc_network,'response.json'),'r',encoding='utf-8') as stream: reply=json.load(stream)
                if reply.get('id')==request_id:
                    if 'error' in reply: raise urllib.error.URLError(reply['error'])
                    response=_TcResponse(reply,request.full_url)
                    if response.status>=400:
                        raise urllib.error.HTTPError(request.full_url,response.status,'Public API response',response.headers,response)
                    return response
            except (json.JSONDecodeError,UnicodeDecodeError): pass
            time.sleep(0.01)
        raise urllib.error.URLError('Network bridge timeout')
urllib.request.urlopen=_tc_urlopen
""" + "\n" + PythonPackageBootstrap;
    internal const string NodeNetworkPackageBootstrap = """
'use strict';
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),https=require('node:https');
const {Readable,Writable}=require('node:stream');
const [tcEntry,tcNetwork,...tcArguments]=process.argv.slice(1);
process.argv=[process.execPath,tcEntry,...tcArguments];
let tcSequence=0,tcQueue=Promise.resolve();
const tcAbort=()=>Object.assign(new Error('The operation was aborted'),{name:'AbortError'});
function tcRpc(url,headers={},signal){
  const run=async()=>{
    if(signal?.aborted) throw tcAbort();
    const id=++tcSequence;
    if(id>20) throw new Error('Network request budget exceeded');
    const encoded=JSON.stringify({id,method:'GET',url:String(url),headers});
    if(Buffer.byteLength(encoded,'utf8')>16384) throw new Error('Network request exceeds its budget');
    fs.writeFileSync(path.join(tcNetwork,'request.json'),encoded,'utf8');
    const deadline=Date.now()+30000;
    while(Date.now()<deadline){
      if(signal?.aborted) throw tcAbort();
      let reply;
      try { reply=JSON.parse(fs.readFileSync(path.join(tcNetwork,'response.json'),'utf8')); }
      catch(error){ if(!(error instanceof SyntaxError)) throw error; }
      if(reply?.id===id){
        if(reply.error) throw new Error(reply.error);
        return reply;
      }
      await new Promise(resolve=>setTimeout(resolve,10));
    }
    throw new Error('Network bridge timeout');
  };
  const pending=tcQueue.then(run,run);
  tcQueue=pending.catch(()=>{});
  return pending;
}
globalThis.fetch=async(input,options={})=>{
  const request=new Request(input,options);
  if(request.method!=='GET'||request.body!==null||request.credentials==='include')
    throw new TypeError('Only credential-free reviewed public GET requests are available');
  const reply=await tcRpc(request.url,Object.fromEntries(request.headers),request.signal);
  const body=[204,205,304].includes(reply.status)?null:Buffer.from(reply.body,'base64');
  const response=new Response(body,{status:reply.status,headers:reply.headers});
  Object.defineProperty(response,'url',{value:request.url});
  return response;
};
function tcRequest(protocol,input,options,callback){
  if(typeof options==='function'){callback=options;options={};}
  let configuration,url;
  if(typeof input==='string'||input instanceof URL){url=new URL(input);configuration={...(options||{})};}
  else {
    configuration={...(input||{}),...(options||{})};
    const host=configuration.hostname||configuration.host||'localhost';
    url=new URL((configuration.protocol||protocol)+'//'+host+(configuration.port?':'+configuration.port:'')+(configuration.path||'/'));
  }
  if(configuration.hostname) url.hostname=configuration.hostname;
  else if(configuration.host&&typeof input==='string') url.host=configuration.host;
  if(configuration.port!==undefined) url.port=String(configuration.port);
  if(configuration.path!==undefined){
    const full=new URL(configuration.path,url);
    url.pathname=full.pathname;url.search=full.search;url.hash=full.hash;
  }
  if(configuration.protocol!==undefined) url.protocol=configuration.protocol;
  let headers={};
  if(Array.isArray(configuration.headers)){
    for(let i=0;i<configuration.headers.length;i+=2) headers[String(configuration.headers[i]).toLowerCase()]=String(configuration.headers[i+1]);
  }else for(const [name,value] of Object.entries(configuration.headers||{})) headers[name.toLowerCase()]=Array.isArray(value)?value.join(', '):String(value);
  const controller=new AbortController();
  let timer;
  const request=new Writable({
    autoDestroy:false,
    write(chunk,encoding,done){done(new Error('GET request bodies are not supported'));},
    final(done){
      done();
      Promise.resolve().then(async()=>{
        if(url.protocol!==protocol||String(configuration.method||'GET').toUpperCase()!=='GET'||configuration.auth||url.username||url.password)
          throw new Error('Only credential-free reviewed public GET requests are available');
        const reply=await tcRpc(url.href,headers,controller.signal);
        if(request.destroyed) return;
        clearTimeout(timer);
        const response=Readable.from([Buffer.from(reply.body,'base64')]);
        response.statusCode=reply.status;response.statusMessage=http.STATUS_CODES[reply.status]||'';
        response.headers=Object.fromEntries(Object.entries(reply.headers||{}).map(([key,value])=>[key.toLowerCase(),value]));
        response.rawHeaders=Object.entries(reply.headers||{}).flat();response.req=request;response.url=url.href;
        response.once('end',()=>{response.complete=true;request.destroy();});
        request.emit('response',response);
      }).catch(error=>{clearTimeout(timer);request.destroy(error);});
    },
    destroy(error,done){clearTimeout(timer);controller.abort();done(error);}
  });
  request.setHeader=(name,value)=>{headers[String(name).toLowerCase()]=String(value);return request;};
  request.getHeader=name=>headers[String(name).toLowerCase()];
  request.getHeaders=()=>({...headers});
  request.hasHeader=name=>Object.hasOwn(headers,String(name).toLowerCase());
  request.removeHeader=name=>{delete headers[String(name).toLowerCase()];};
  request.setTimeout=(milliseconds,handler)=>{
    clearTimeout(timer);
    if(handler) request.once('timeout',handler);
    if(milliseconds>0) timer=setTimeout(()=>{request.emit('timeout');request.destroy(new Error('Network request timed out'));},Math.min(milliseconds,30000));
    return request;
  };
  request.abort=()=>{request.emit('abort');request.destroy();};
  request.flushHeaders=()=>{};
  request.setNoDelay=()=>request;request.setSocketKeepAlive=()=>request;
  if(configuration.signal){
    if(configuration.signal.aborted) queueMicrotask(()=>request.destroy(tcAbort()));
    else configuration.signal.addEventListener('abort',()=>request.destroy(tcAbort()),{once:true});
  }
  if(configuration.timeout) request.setTimeout(configuration.timeout);
  if(callback) request.once('response',callback);
  return request;
}
for(const [module,protocol] of [[http,'http:'],[https,'https:']]){
  module.request=(...values)=>tcRequest(protocol,...values);
  module.get=(...values)=>{const request=tcRequest(protocol,...values);request.end();return request;};
}
require('node:module').syncBuiltinESMExports();
require('node:module').runMain(tcEntry);
""";
    private readonly EvaluationManifest _manifest;
    private readonly Func<SandboxRunRequest, CancellationToken, Task<SandboxRunResult>> _execute;
    private readonly SemaphoreSlim _operation = new(1, 1);
    public IReadOnlyList<SkillRegistration> Skills => _manifest.Skills;

    private SkillExecutionRegistry(EvaluationManifest manifest,
        Func<SandboxRunRequest, CancellationToken, Task<SandboxRunResult>>? execute)
    {
        _manifest = manifest;
        _execute = execute ?? new AppContainerProcessRunner(manifest.OwnedRoot).RunAsync;
    }

    public static Task<SkillExecutionRegistry> LoadAsync(string manifestPath, CancellationToken token)
        => LoadAsync(manifestPath, null, token);

    // Executor injection is an offline test seam, not part of MCP input or production configuration.
    public static async Task<SkillExecutionRegistry> LoadAsync(string manifestPath,
        Func<SandboxRunRequest, CancellationToken, Task<SandboxRunResult>>? execute, CancellationToken token)
        => new(await BrokerFiles.ReadManifestAsync(manifestPath, token), execute);

    public async Task<SkillExecutionResult> ExecuteAsync(string skillId, JsonElement input, CancellationToken token)
    {
        var skill = Skills.FirstOrDefault(x => string.Equals(x.SkillId, skillId, StringComparison.Ordinal));
        if (skill is null) return Blocked("unknown-skill");
        try { ValidateInput(skill, input); }
        catch (Exception error) when (error is BrokerValidationException or InvalidOperationException)
        { return Blocked("invalid-input"); }
        var generic = skill.Kind is "python-script" or "node-script";
        // Workers never receive Internet capability. Reviewed script APIs use a bounded host GET
        // bridge; a declaration alone is insufficient and unimplemented transports fail closed.
        var networkEnabled = skill.RequiresNetwork && generic && skill.NetworkHosts is { Count: > 0 };
        if (skill.RequiresNetwork && !networkEnabled) return Blocked("network-policy");
        await _operation.WaitAsync(token);
        string? work = null;
        SkillExecutionResult result = Blocked("worker-unavailable");
        var cleanupFailed = false;
        try
        {
            var node = skill.Kind is "node-json-stdin" or "node-script";
            var executable = node ? _manifest.NodeExecutable! : _manifest.PythonExecutable;
            var executableHash = node ? _manifest.NodeSha256! : _manifest.PythonSha256;
            await using var executableLock = await BrokerFiles.OpenVerifiedAsync(_manifest.OwnedRoot, executable, executableHash, token);
            await using var scriptLock = await BrokerFiles.OpenVerifiedAsync(_manifest.OwnedRoot, skill.ScriptPath, skill.Sha256, token);
            await using var runtimeLocks = await BrokerFiles.OpenRuntimeFilesAsync(_manifest.OwnedRoot, _manifest.RuntimeFiles, token);
            await using var packageLocks = await BrokerFiles.OpenPackageFilesAsync(_manifest.OwnedRoot, skill, token);
            var workRoot = Path.Combine(_manifest.OwnedRoot, "broker-work");
            BrokerFiles.RequireUnder(_manifest.OwnedRoot, workRoot);
            BrokerFiles.RejectReparsePoints(workRoot);
            Directory.CreateDirectory(workRoot);
            BrokerFiles.RejectReparsePoints(workRoot);
            work = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
            BrokerFiles.RequireUnder(_manifest.OwnedRoot, work);
            Directory.CreateDirectory(work);
            BrokerFiles.RejectReparsePoints(work);
            await using var networkBridge = networkEnabled ? new SkillNetworkBridge(work, skill.NetworkHosts!) : null;
            var arguments = new List<string>();
            if (node)
            {
                arguments.Add("--preserve-symlinks-main");
                // Module dependency realpath also traverses drive ancestors on Windows. Packages
                // already reject reparse points; preserve their verified paths without granting
                // AppContainer access to drive roots or other parent directories.
                if (skill.Kind == "node-script") arguments.Add("--preserve-symlinks");
            }
            else arguments.AddRange(["-I", "-S", "-B", "-X", "utf8"]);
            if (skill.Kind == "python-script")
            {
                arguments.AddRange(["-c", networkEnabled ? PythonNetworkPackageBootstrap : PythonPackageBootstrap,
                    skill.ScriptPath, skill.PackageRoot!]);
                if (networkBridge is not null) arguments.Add(networkBridge.DirectoryPath);
            }
            else if (skill.Kind == "node-script" && networkEnabled)
                arguments.AddRange(["--eval", NodeNetworkPackageBootstrap, "--", skill.ScriptPath, networkBridge!.DirectoryPath]);
            else arguments.Add(skill.ScriptPath);
            if (generic && input.TryGetProperty("arguments", out var suppliedArguments))
                arguments.AddRange(suppliedArguments.EnumerateArray().Select(x => x.GetString()!));
            else if (skill.Kind == "python-cli-fixed") arguments.AddRange(skill.FixedArguments);
            var stdin = generic ? input.TryGetProperty("stdin", out var suppliedInput) ? suppliedInput.GetString()! : ""
                : skill.Kind == "python-cli-fixed" ? "" : input.GetRawText() + "\n";
            var request = new SandboxRunRequest(executable, arguments.AsReadOnly(), work,
                stdin,
                Array.AsReadOnly(new[] { Path.GetDirectoryName(executable)!, skill.PackageRoot ?? Path.GetDirectoryName(skill.ScriptPath)! }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),
                Array.AsReadOnly(new[] { work }), AllowNetwork: false,
                TimeoutMilliseconds: generic ? 60000 : 15000);
            using var networkCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var networkTask = networkBridge?.RunAsync(networkCancellation.Token) ?? Task.CompletedTask;
            SandboxRunResult outcome;
            try { outcome = await _execute(request, token); }
            finally
            {
                networkCancellation.Cancel();
                try { await networkTask; }
                catch (OperationCanceledException) when (networkCancellation.IsCancellationRequested) { }
            }
            var outputSize = Encoding.UTF8.GetByteCount(outcome.Stdout) + (long)Encoding.UTF8.GetByteCount(outcome.Stderr);
            if (outcome.OutputLimited || outputSize > request.MaxOutputBytes)
                result = new("failed", outcome.ExitCode, "", "", outcome.WasAppContainer, outcome.TimedOut, true, "output-limit");
            else if (!outcome.WasAppContainer)
                result = new("blocked", outcome.ExitCode, "", "", false, outcome.TimedOut, false, "appcontainer-required");
            else
            {
                var completed = outcome.ExitCode == 0 && !outcome.TimedOut && outcome.Failure is null;
                var failure = outcome.TimedOut ? "timeout" : outcome.Failure is not null ? "worker-failure" : outcome.ExitCode != 0 ? "worker-exit" : null;
                if (completed && skill.Kind == "python-json-stdin" && !ValidCalculatorOutput(outcome.Stdout))
                { completed = false; failure = "invalid-worker-output"; }
                result = new(completed ? "completed" : "failed", outcome.ExitCode, outcome.Stdout, outcome.Stderr,
                    true, outcome.TimedOut, false, failure);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (BrokerValidationException) { result = Blocked("registration-integrity"); }
        catch { result = Blocked("worker-unavailable"); }
        finally
        {
            try { if (work is not null) DeleteWorkDirectory(work); }
            catch { cleanupFailed = true; }
            finally { _operation.Release(); }
        }
        return cleanupFailed ? result with { Status = "failed", Failure = "cleanup-failed" } : result;
    }

    private static void ValidateInput(SkillRegistration skill, JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(input.GetRawText()) > BrokerFiles.MaximumInputBytes) throw BrokerFiles.Invalid();
        BrokerFiles.ValidateJson(input);
        if (skill.Kind is "python-script" or "node-script")
        {
            BrokerFiles.ObjectFields(input, ["arguments", "stdin"]);
            var argumentCharacters = 0;
            if (input.TryGetProperty("arguments", out var arguments))
            {
                if (arguments.ValueKind != JsonValueKind.Array || arguments.GetArrayLength() > 64) throw BrokerFiles.Invalid();
                foreach (var argument in arguments.EnumerateArray())
                {
                    if (argument.ValueKind != JsonValueKind.String) throw BrokerFiles.Invalid();
                    var text = argument.GetString()!;
                    if (text.Length > 4096 || text.Contains('\0') || (argumentCharacters += text.Length + 3) > 24000) throw BrokerFiles.Invalid();
                }
            }
            if (input.TryGetProperty("stdin", out var stdin) && (stdin.ValueKind != JsonValueKind.String ||
                Encoding.UTF8.GetByteCount(stdin.GetString()!) > BrokerFiles.MaximumInputBytes)) throw BrokerFiles.Invalid();
        }
        else if (skill.Kind == "python-cli-fixed")
        {
            if (input.EnumerateObject().Any()) throw BrokerFiles.Invalid();
        }
        else if (skill.Kind == "python-json-stdin")
        {
            var operation = BrokerFiles.Text(input, "operation", 64);
            if (!skill.AllowedOperations.Contains(operation, StringComparer.Ordinal)) throw BrokerFiles.Invalid();
            BrokerFiles.ValidateCalculatorInput(input, operation);
        }
        else
        {
            BrokerFiles.ObjectFields(input, ["a", "b"]);
            foreach (var field in new[] { "a", "b" })
                if (!input.TryGetProperty(field, out var number) || number.ValueKind != JsonValueKind.Number ||
                    !number.TryGetDouble(out var parsed) || !double.IsFinite(parsed)) throw BrokerFiles.Invalid();
        }
    }

    private static bool ValidCalculatorOutput(string text)
    {
        try
        {
            using var result = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            BrokerFiles.ValidateJson(result.RootElement);
            return result.RootElement.ValueKind == JsonValueKind.Object && result.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
        }
        catch (Exception error) when (error is JsonException or BrokerValidationException) { return false; }
    }

    private void DeleteWorkDirectory(string directory)
    {
        // A worker cannot turn cleanup into a recursive operation on an unowned/reparse target.
        BrokerFiles.RequireUnder(_manifest.OwnedRoot, directory);
        BrokerFiles.RejectReparsePoints(directory);
        if (!Directory.Exists(directory)) return;
        var pending = new Stack<string>();
        pending.Push(directory);
        var count = 0;
        while (pending.TryPop(out var current))
        {
            BrokerFiles.RejectReparsePoints(current);
            foreach (var child in Directory.EnumerateFileSystemEntries(current))
            {
                if (++count > 4096) throw BrokerFiles.Invalid();
                BrokerFiles.RequireUnder(directory, child);
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) throw BrokerFiles.Invalid();
                if (Directory.Exists(child)) pending.Push(child);
            }
        }
        Directory.Delete(directory, recursive: true);
    }

    private static SkillExecutionResult Blocked(string reason) => new("blocked", null, "", "", false, false, false, reason);
}
