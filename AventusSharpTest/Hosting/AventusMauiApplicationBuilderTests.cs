using AventusSharp;
using AventusSharp.Hosting;
using AventusSharp.Maui;
using AventusSharp.Routes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using NUnit.Framework;

namespace AventusSharpTest.Hosting;

public sealed class AventusMauiApplicationBuilderTests
{
    [Test]
    public void AddAventus_registers_the_dispatcher_and_bridge_as_singletons()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder(useDefaults: false);

        MauiAppBuilder returnedBuilder = builder.AddAventus();

        Assert.That(returnedBuilder, Is.SameAs(builder));
        using ServiceProvider services = builder.Services.BuildServiceProvider();
        Assert.Multiple(() =>
        {
            Assert.That(
                services.GetRequiredService<IAventusRequestDispatcher>(),
                Is.TypeOf<AventusRequestDispatcher>());
            Assert.That(
                services.GetRequiredService<AventusMauiBridge>(),
                Is.SameAs(services.GetRequiredService<AventusMauiBridge>()));
        });
    }
}
