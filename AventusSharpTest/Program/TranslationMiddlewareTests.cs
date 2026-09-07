using System.Globalization;
using AventusSharp;
using AventusSharp.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace AventusSharpTest.Program;

[TestFixture]
public class TranslationMiddlewareTests
{
    [Test]
    public async Task Middleware_selects_request_languages_and_isolates_application_configuration()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var entered = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RequestDelegate Build(string french)
        {
            var app = new ApplicationBuilder(services);
            app.UseAventusTranslations(options =>
            {
                options.DefaultCulture = "fr";
                options.Add("fr", "App.Hello", french);
                options.Add("en", "App.Hello", "Hello");
            });
            app.Run(async context =>
            {
                if (Interlocked.Increment(ref entered) == 3) ready.SetResult();
                await ready.Task;
                context.Items["text"] = AventusTranslations.Get("App.Hello");
            });
            return app.Build();
        }
        var first = Build("Bonjour");
        var second = Build("Salut");
        var fr = new DefaultHttpContext { RequestServices = services };
        fr.Request.Headers.AcceptLanguage = "fr-CH";
        var en = new DefaultHttpContext { RequestServices = services };
        en.Request.QueryString = new QueryString("?culture=en");
        var fallback = new DefaultHttpContext { RequestServices = services };
        fallback.Request.Headers.AcceptLanguage = "de";

        await Task.WhenAll(first(fr), first(en), second(fallback));
        Assert.Multiple(() =>
        {
            Assert.That(fr.Items["text"], Is.EqualTo("Bonjour"));
            Assert.That(en.Items["text"], Is.EqualTo("Hello"));
            Assert.That(fallback.Items["text"], Is.EqualTo("Salut"));
        });
    }

    [Test]
    public async Task Existing_request_localization_can_be_reused()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseRequestLocalization(options => options.SetDefaultCulture("fr").AddSupportedCultures("fr").AddSupportedUICultures("fr"));
        app.UseAventusTranslations(useRequestLocalization: false);
        app.Run(context =>
        {
            context.Items["text"] = AventusTranslations.Get(AventusMessageKeys.Validation.Unique);
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext { RequestServices = services };
        await app.Build()(context);
        Assert.That(context.Items["text"], Is.EqualTo("Ce champ doit être unique."));
    }
}
