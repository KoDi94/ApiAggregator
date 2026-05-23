using System.Reflection;
using ApiAggregator.Domain.Interfaces;
using ApiAggregator.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ApiAggregator.Tests.Services;

public class PerformanceAnomalyServiceTests
{
    [Fact]
    public void DetectsAnomaly_WhenRecentAverageExceedsOverallBy50Percent()
    {
        var statsMock = new Mock<IStatisticsService>();
        statsMock.Setup(x => x.GetAveragesForApi("TestApi"))
            .Returns((100, 200));

        var clientMock = new Mock<IExternalApiClient>();
        clientMock.Setup(x => x.Name).Returns("TestApi");

        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(s => s.GetService(typeof(IStatisticsService)))
            .Returns(statsMock.Object);
        scopeServiceProvider.Setup(s => s.GetService(typeof(IEnumerable<IExternalApiClient>)))
            .Returns(new[] { clientMock.Object });

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(scopeFactoryMock.Object);

        var loggerMock = new Mock<ILogger<PerformanceAnomalyService>>();
        var service = new PerformanceAnomalyService(serviceProviderMock.Object, loggerMock.Object);

        InvokeCheckForAnomalies(service);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PERFORMANCE ANOMALY")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogsNothing_WhenRecentAverageIsWithinThreshold()
    {
        var statsMock = new Mock<IStatisticsService>();
        statsMock.Setup(x => x.GetAveragesForApi("TestApi"))
            .Returns((100, 120));

        var clientMock = new Mock<IExternalApiClient>();
        clientMock.Setup(x => x.Name).Returns("TestApi");

        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(s => s.GetService(typeof(IStatisticsService)))
            .Returns(statsMock.Object);
        scopeServiceProvider.Setup(s => s.GetService(typeof(IEnumerable<IExternalApiClient>)))
            .Returns(new[] { clientMock.Object });

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(scopeFactoryMock.Object);

        var loggerMock = new Mock<ILogger<PerformanceAnomalyService>>();
        var service = new PerformanceAnomalyService(serviceProviderMock.Object, loggerMock.Object);

        InvokeCheckForAnomalies(service);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PERFORMANCE ANOMALY")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private static void InvokeCheckForAnomalies(PerformanceAnomalyService service)
    {
        var method = typeof(PerformanceAnomalyService)
            .GetMethod("CheckForAnomalies", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(service, null);
    }
}
