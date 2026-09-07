using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text;
using AventusSharp.Data;
using AventusSharp.Localization;
using AventusSharp.Tools;
using Newtonsoft.Json;
using NUnit.Framework;

namespace AventusSharpTest.Tools;

[TestFixture]
public class TranslationTests
{
    [Test]
    public void Satellite_resources_support_french_parent_cultures_and_english_fallback()
    {
        var localizer = new AventusLocalizer();
        Assert.Multiple(() =>
        {
            Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique), Is.EqualTo("The field must be unique"));
            Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique, CultureInfo.GetCultureInfo("fr-CH")),
                Is.EqualTo("Ce champ doit être unique."));
            Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique, CultureInfo.GetCultureInfo("de")),
                Is.EqualTo("The field must be unique"));
            Assert.That(localizer.Get("App.Missing"), Is.EqualTo("App.Missing"));
        });
    }

    [Test]
    public void Application_messages_and_overrides_use_a_snapshot_and_culture_fallbacks()
    {
        AventusTranslationOptions saved = null!;
        var localizer = new AventusLocalizer(options =>
        {
            saved = options;
            options.DefaultCulture = "fr";
            options.Add("fr", "App.Welcome", "Bonjour {0}");
            options.Add("fr", AventusMessageKeys.Validation.Unique, "Valeur déjà utilisée");
            options.Add("fr-CH", AventusMessageKeys.Validation.Unique, "Valeur suisse déjà utilisée");
        });
        saved.Add("fr", "App.Welcome", "Changed");
        Assert.Multiple(() =>
        {
            Assert.That(localizer.Get("App.Welcome", "Alice"), Is.EqualTo("Bonjour Alice"));
            Assert.That(localizer.Get("App.Welcome", CultureInfo.GetCultureInfo("de"), "Alice"),
                Is.EqualTo("Bonjour Alice"));
            Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique, CultureInfo.GetCultureInfo("fr-CH")),
                Is.EqualTo("Valeur suisse déjà utilisée"));
            Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique, CultureInfo.GetCultureInfo("fr-FR")),
                Is.EqualTo("Valeur déjà utilisée"));
        });
    }

    [Test]
    public void Provider_can_partially_override_the_catalog()
    {
        var localizer = new AventusLocalizer(options => options.Provider = new CustomProvider());
        Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique, CultureInfo.GetCultureInfo("fr-CH")),
            Is.EqualTo("Personnalisé"));
        Assert.That(localizer.Get(AventusMessageKeys.Data.ManagerNotFound, CultureInfo.GetCultureInfo("fr"), "Customer"),
            Is.EqualTo("Aucun gestionnaire de données trouvé pour le type Customer."));
    }

    [Test]
    public void Application_resx_resources_can_supply_translations()
    {
        // Exercise ResourceManager integration with an existing satellite resource set.
        var manager = new ResourceManager("AventusSharp.Localization.Messages", typeof(AventusLocalizer).Assembly);
        var localizer = new AventusLocalizer(options => options.AddResources(manager));
        Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique, CultureInfo.GetCultureInfo("fr-CH")),
            Is.EqualTo("Ce champ doit être unique."));
        Assert.That(localizer.Get(AventusMessageKeys.Validation.Unique, CultureInfo.GetCultureInfo("en")),
            Is.EqualTo("The field must be unique"));
    }

    [Test]
    public async Task Nested_async_scopes_restore_the_previous_language_even_after_an_exception()
    {
        using var root = AventusTranslations.Use(new AventusLocalizer());
        using (AventusTranslations.UseCulture("fr"))
        {
            await Task.Yield();
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var nested = AventusTranslations.UseCulture("en");
                Assert.That(AventusTranslations.Get(AventusMessageKeys.Validation.Unique), Is.EqualTo("The field must be unique"));
                throw new InvalidOperationException();
            });
            Assert.That(AventusTranslations.Get(AventusMessageKeys.Validation.Unique), Is.EqualTo("Ce champ doit être unique."));
        }
        Assert.That(AventusTranslations.Get(AventusMessageKeys.Validation.Unique), Is.EqualTo("The field must be unique"));
    }

    [Test]
    public async Task Concurrent_scopes_keep_their_own_language()
    {
        var localizer = new AventusLocalizer();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        async Task<string> Translate(string culture)
        {
            using var scope = AventusTranslations.Use(localizer, CultureInfo.GetCultureInfo(culture));
            if (Interlocked.Increment(ref count) == 2) ready.SetResult();
            await ready.Task;
            return AventusTranslations.Get(AventusMessageKeys.Validation.Unique);
        }
        var results = await Task.WhenAll(Translate("fr"), Translate("en"));
        Assert.That(results, Is.EqualTo(new[] { "Ce champ doit être unique.", "The field must be unique" }));
    }

    [Test]
    public void Library_error_is_localized_once_and_keeps_its_code_and_serialized_message()
    {
        DataError error;
        using (AventusTranslations.Use(new AventusLocalizer(options => options.DefaultCulture = "fr")))
        {
            error = TypeTools.GetTypeDataObject("Missing.TranslationTestType").Errors.Single();
        }
        using var scope = AventusTranslations.Use(new AventusLocalizer());
        var json = JsonConvert.SerializeObject(error);
        var restored = JsonConvert.DeserializeObject<GenericError>(json)!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(DataErrorCode.WrongType));
            Assert.That(error.Message, Is.EqualTo("Le type Missing.TranslationTestType est introuvable."));
            Assert.That(restored.Message, Is.EqualTo(error.Message));
            Assert.That(error.File, Does.EndWith("TypeTools.cs"));
            Assert.That(error.Line, Is.GreaterThan(0));
            Assert.That(new DataError(DataErrorCode.ValidationError, "Custom literal").Message,
                Is.EqualTo("Custom literal"));
        });
    }

    [Test]
    public void All_french_templates_have_the_same_parameter_count_as_english()
    {
        var manager = new ResourceManager("AventusSharp.Localization.Messages", typeof(AventusLocalizer).Assembly);
        var english = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        var french = manager.GetResourceSet(CultureInfo.GetCultureInfo("fr"), true, false)!;
        Assert.That(french, Is.Not.Null, "French satellite assembly must be deployed with the library");
        Assert.That(french.Cast<DictionaryEntry>().Count(), Is.EqualTo(english.Cast<DictionaryEntry>().Count()));
        foreach (DictionaryEntry entry in english)
        {
            var translated = french.GetString((string)entry.Key);
            Assert.That(translated, Is.Not.Null, (string)entry.Key);
            var en = CompositeFormat.Parse((string)entry.Value!);
            var fr = CompositeFormat.Parse(translated!);
            Assert.That(fr.MinimumArgumentCount, Is.EqualTo(en.MinimumArgumentCount), (string)entry.Key);
        }
    }

    [Test]
    public void Generated_keys_match_every_catalog_entry()
    {
        var manager = new ResourceManager("AventusSharp.Localization.Messages", typeof(AventusLocalizer).Assembly);
        var catalog = manager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        IEnumerable<string> Keys(Type type) => type.GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(type.GetNestedTypes().SelectMany(Keys));
        Assert.That(Keys(typeof(AventusMessageKeys)),
            Is.EquivalentTo(catalog.Cast<DictionaryEntry>().Select(entry => (string)entry.Key)));
    }

    [Test]
    public void Formatting_uses_the_requested_culture_and_preserves_escaped_braces()
    {
        var localizer = new AventusLocalizer(options => options.Add("fr", "App.Total", "{{Total}} : {0:F2}"));
        Assert.That(localizer.Get("App.Total", CultureInfo.GetCultureInfo("fr-FR"), 12.5m),
            Is.EqualTo("{Total} : 12,50"));
        Assert.Throws<FormatException>(() => new AventusLocalizer(options => options.Add("en", "Bad", "{")));
    }

    [Test]
    public async Task Cached_validation_attributes_resolve_each_calls_language_and_keep_custom_text()
    {
        var size = new AventusSharp.Data.Attributes.Size(2, 5);
        var required = new AventusSharp.Data.Attributes.NotNullable();
        var context = new AventusSharp.Data.Attributes.ValidationContext(
            "Name", typeof(string), null, null!, StorableAction.Create, null);
        using var root = AventusTranslations.Use(new AventusLocalizer());
        using (AventusTranslations.UseCulture("fr"))
        {
            Assert.That((await size.IsValid("x", context)).Errors.Single().Message,
                Is.EqualTo("La longueur du champ Name doit être comprise entre 2 et 5 caractères."));
            Assert.That((await required.IsValid(null, context)).Errors.Single().Message,
                Is.EqualTo("Le champ Name est obligatoire."));
            var custom = new AventusSharp.Data.Attributes.NotNullable("Literal {0}");
            Assert.That((await custom.IsValid(null, context)).Errors.Single().Message,
                Is.EqualTo("Literal {0}"));
        }
        Assert.That((await required.IsValid(null, context)).Errors.Single().Message,
            Is.EqualTo("The field Name is required."));
    }

    private sealed class CustomProvider : IAventusMessageProvider
    {
        public string? GetTemplate(string key, CultureInfo culture) =>
            key == AventusMessageKeys.Validation.Unique && culture.Name == "fr" ? "Personnalisé" : null;
    }
}
