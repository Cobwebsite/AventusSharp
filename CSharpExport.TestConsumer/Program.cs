using CSharpExport.TestFixture;
using Newtonsoft.Json;

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
