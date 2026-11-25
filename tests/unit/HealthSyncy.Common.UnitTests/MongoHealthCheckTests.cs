using HealthSyncy.Common.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HealthSyncy.Common.UnitTests;

public sealed class MongoHealthCheckTests
{
    private readonly AutoMocker _mocker = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Is_healthy_when_MongoDB_answers_a_ping()
    {
        var check = _mocker.CreateInstance<MongoHealthCheck>();

        var result = await check.CheckHealthAsync(new HealthCheckContext(), Cancellation);

        result.Status.Should().Be(HealthStatus.Healthy);
        _mocker.GetMock<IMongoDatabase>().Verify(database => database.RunCommandAsync(
            It.Is<Command<BsonDocument>>(command => Rendered(command) == """{ "ping" : 1 }"""), null, Cancellation));
    }

    [Fact]
    public async Task Lets_the_failure_surface_when_MongoDB_is_unreachable()
    {
        _mocker.GetMock<IMongoDatabase>()
            .Setup(database => database.RunCommandAsync(It.IsAny<Command<BsonDocument>>(), null, Cancellation))
            .ThrowsAsync(new TimeoutException("No server available."));
        var check = _mocker.CreateInstance<MongoHealthCheck>();

        var ping = () => check.CheckHealthAsync(new HealthCheckContext(), Cancellation);

        await ping.Should().ThrowAsync<TimeoutException>();
    }

    private static string Rendered(Command<BsonDocument> command) =>
        ((BsonDocumentCommand<BsonDocument>)command).Document.ToJson();
}
