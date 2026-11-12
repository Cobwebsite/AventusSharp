using CSharpExport.TestFixture;
using Newtonsoft.Json;
using CSharpExport.TestFixture.Routes;
using CSharpExport.TestFixture.Generated.Http;
using System.Net;
using System.Net.Http;
using System.Text;

var person = new Person { Id = 42, Name = "Test", Settings = new JsonSerializerSettings() };
if (person.Id != 42 || person.Settings is null) throw new Exception("Propriétés exportées incorrectes.");
if (typeof(Person).GetProperty(nameof(Person.Name))!.GetCustomAttributes(typeof(JsonPropertyAttribute), false)
    .SingleOrDefault() is not JsonPropertyAttribute json || json.PropertyName != "fullName")
    throw new Exception("L'attribut externe n'a pas été conservé.");
if (typeof(Person).GetProperty(nameof(Person.Id))!.GetCustomAttributes(false).Any())
    throw new Exception("Un attribut de stockage a été exporté.");
if (typeof(Person).GetCustomAttributes(false).Any(a => a.GetType().Namespace?.StartsWith("AventusSharp") == true))
    throw new Exception("Un attribut de classe AventusSharp a été exporté.");
if (typeof(Person).Assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("AventusSharp")))
    throw new Exception("La DLL dépend d'AventusSharp.");
if (typeof(Person).GetProperty("Secret") != null || typeof(Person).GetMethod("DisplayName") != null)
    throw new Exception("Un membre exclu a été exporté.");
Console.WriteLine("Import DLL indépendant, Id et package externe : OK");
var profile = new Profile { Label = "A" };
if (profile.Label != "A" || typeof(Profile).GetProperty("InternalValue") != null)
    throw new Exception("Classe [Export] incorrecte.");
if ((int)ProfileState.Published != 4) throw new Exception("Enum incorrecte.");
if (Enum.IsDefined(typeof(ProfileState), "Internal")) throw new Exception("Valeur [NoExport] exportée.");
var error = new SampleError { Code = 1, Message = "Oops", Category = "Test" };
var result = new SampleResult { RequestId = "req-1" };
if (error.Code != 1 || result.RequestId != "req-1")
    throw new Exception("GenericError ou WithError incorrect.");
Console.WriteLine("Classes Export, enum, GenericError et WithError : OK");

var handler = new ProbeHandler();
var router = new ApiRouter(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });
var first = await router.GetPerson(42);
if (first.Value != "ok" || handler.Count != 1 || handler.LastPath != "/api/people/42" || handler.LastMethod != "GET")
    throw new Exception("Route GET ou préfixe incorrect.");
await router.GetPerson(42);
if (handler.Count != 2) throw new Exception("La route GET n'a pas �t� appel�e deux fois.");
await router.CreatePerson("Ada", 34);
if (handler.LastMethod != "POST" || handler.LastBody?.Contains("\"name\":\"Ada\"") != true)
    throw new Exception("Corps POST incorrect.");
await router.SendForm("file");
if (handler.LastContentType?.StartsWith("multipart/form-data") != true ||
    handler.LastBody?.Contains("file") != true)
    throw new Exception("FormData incorrect.");
await router.Optional();
if (handler.LastPath != "/api/optional") throw new Exception("Paramètre facultatif incorrect.");
await router.Upload(new HttpUpload("sample.txt", Encoding.UTF8.GetBytes("payload"), "text/plain"));
if (handler.LastPath != "/api/upload" || handler.LastContentType != "multipart/form-data" ||
    handler.LastBody?.Contains("sample.txt") != true)
    throw new Exception("Fichier multipart incorrect.");
await router.UploadMany([new HttpUpload("a.txt", Encoding.UTF8.GetBytes("a"))]);
if (handler.LastBody?.Contains("files[0]") != true)
    throw new Exception("Liste de fichiers incorrecte.");
await router.UploadNested(new UploadPayload { Name = "A", File = new HttpUpload("nested.txt", Encoding.UTF8.GetBytes("n")) });
if (handler.LastBody?.Contains("payload.File") != true)
    throw new Exception("Fichier imbriqué incorrect.");
await router.FetchText();
await router.Download();
await router.Ping();
await router.Multiple_POST4();
if (handler.LastPath != "/api/second" || handler.LastMethod != "POST")
    throw new Exception("Routes ou verbes multiples incorrects.");
await router.Dynamic("chosen");
if (handler.LastPath != "/api/dynamic/chosen") throw new Exception("Segment dynamique incorrect.");
await router.RegexRoute("/api/regex/value");
if (handler.LastPath != "/api/regex/value") throw new Exception("Route regex incorrecte.");
var typedProfile = await router.GetProfile();
if (typedProfile.Value?.Label != "Ada") throw new Exception("Type de retour JSON incorrect.");
var wrapped = await router.GetWrapped();
if (!wrapped.Success || wrapped.Value?.Label != "Ada") throw new Exception("ResultWithError incorrect.");
var emptyResult = await router.EmptyResult();
if (emptyResult.Success || emptyResult.Errors?.GetArrayLength() != 1)
    throw new Exception("VoidWithError incorrect.");
var customResult = await router.GetCustomResult();
if (!customResult.Success || customResult.Value?.RequestId != "req-1")
    throw new Exception("Classe WithError incorrecte.");
if (typeof(ApiRouter).GetMethod("Hidden") != null || typeof(ApiRouter).GetMethod("PrivateClient") != null)
    throw new Exception("Une route exclue a été exportée.");
var personRouter = new PersonRouter(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });
var inherited = await personRouter.GetById(7);
if (handler.LastPath != "/Person/7" || inherited.Value?.Id != 7 || inherited.Value.Name != "Ada")
    throw new Exception("Route héritée de StorableRouter<T> incorrecte.");
var genericRouter = new TestGenericRouter(new HttpClient(handler) { BaseAddress = new Uri("https://example.test") });
await genericRouter.Echo(new Profile { Label = "Generic" });
if (handler.LastPath != "/echo" || handler.LastMethod != "POST")
    throw new Exception("Routeur générique incorrect.");
await router.Overload(12);
if (handler.LastPath != "/api/overload/int/12" || handler.LastMethod != "GET" || handler.LastBody != null)
    throw new Exception("Wrong int overload route.");
await router.Overload("text");
if (handler.LastPath != "/api/overload/text" || handler.LastMethod != "POST" || handler.LastBody?.Contains("\"value\":\"text\"") != true)
    throw new Exception("Wrong string overload route.");
await router.OverloadList([1, 2]);
if (handler.LastPath != "/api/overload/list-int") throw new Exception("Wrong List<int> route.");
await router.OverloadList(["a"]);
if (handler.LastPath != "/api/overload/list-string") throw new Exception("Wrong List<string> route.");
await router.Mixed(31);
if (handler.LastPath != "/api/mixed/31" || handler.LastBody != null)
    throw new Exception("Path parameter sent in body.");
await router.Mixed_POST2(31);
if (handler.LastPath != "/api/mixed/body" || handler.LastBody?.Contains("\"id\":31") != true)
    throw new Exception("Body parameter missing on second route.");
Console.WriteLine("Routes HTTP, corps et exclusions : OK");

sealed class TestGenericRouter(HttpClient client) : GenericRouter<Profile>(client);

sealed class ProbeHandler : HttpMessageHandler
{
    public int Count { get; private set; }
    public string? LastPath { get; private set; }
    public string? LastMethod { get; private set; }
    public string? LastBody { get; private set; }
    public string? LastContentType { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Count++;
        LastPath = request.RequestUri?.AbsolutePath;
        LastMethod = request.Method.Method;
        LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        LastContentType = request.Content?.Headers.ContentType?.MediaType;
        var content = LastPath == "/api/text" ? "text" :
            LastPath?.EndsWith("/profile") == true ? "{\"Label\":\"Ada\"}" :
            LastPath?.EndsWith("/wrapped") == true ? "{\"Success\":true,\"Result\":{\"Label\":\"Ada\"},\"Errors\":[]}" :
            LastPath == "/Person/7" ? "{\"Success\":true,\"Result\":{\"Id\":7,\"fullName\":\"Ada\"},\"Errors\":[]}" :
            LastPath == "/echo" ? "{\"Label\":\"Generic\"}" :
            LastPath?.EndsWith("/empty-result") == true ? "{\"Success\":false,\"Errors\":[{\"Code\":1}]}" :
            LastPath?.EndsWith("/custom-result") == true ? "{\"Success\":true,\"Errors\":[],\"RequestId\":\"req-1\"}" : "\"ok\"";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8) };
    }
}
