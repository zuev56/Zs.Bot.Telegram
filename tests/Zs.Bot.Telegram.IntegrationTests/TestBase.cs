using System.Diagnostics.CodeAnalysis;
using AutoFixture;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Zs.Bot.Telegram.Extensions;

namespace Zs.Bot.Telegram.IntegrationTests;

[ExcludeFromCodeCoverage]
public abstract class TestBase
{
    protected readonly IFixture Fixture = new Fixture();
    protected readonly Settings Settings;
    protected readonly ServiceProvider ServiceProvider;

    protected TestBase()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("./appsettings.json")
            .AddJsonFile("./appsettings.Development.json", optional: true)
            .Build();

        Settings = configuration.Get<Settings>()!;
        ServiceProvider = CreateServiceProvider(configuration);
    }

    private ServiceProvider CreateServiceProvider(IConfiguration configuration)
    {
        var settings = configuration.Get<Settings>();
        var services = new ServiceCollection()
            .AddSingleton(Mock.Of<ILogger<BotClient>>())
            .AddTelegramBotClient(settings.Token);
        return services.BuildServiceProvider();
    }
}