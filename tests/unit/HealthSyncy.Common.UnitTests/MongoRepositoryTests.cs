using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace HealthSyncy.Common.UnitTests;

public sealed class MongoRepositoryTests
{
    private static readonly DateTime _capturedAt = new(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);

    private readonly AutoMocker _mocker = new();
    private readonly List<FilterDefinition<SnapshotEntity>> _filters = [];
    private readonly MongoRepository<SnapshotEntity> _repository;

    public MongoRepositoryTests()
    {
        _mocker.GetMock<IMongoDatabase>()
            .Setup(database => database.GetCollection<SnapshotEntity>("days", null))
            .Returns(Collection.Object);
        _repository = _mocker.CreateInstance<MongoRepository<SnapshotEntity>>();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Mock<IMongoCollection<SnapshotEntity>> Collection => _mocker.GetMock<IMongoCollection<SnapshotEntity>>();

    [Fact]
    public async Task GetByIdAsync_looks_the_entity_up_by_its_id()
    {
        var stored = Entity(_capturedAt);
        Finds([stored]);

        var found = await _repository.GetByIdAsync(stored.Id, Cancellation);

        found.Should().Be(stored);
        Rendered(_filters.Single()).Should().Be("""{ "_id" : "UTC|2026-11-01" }""");
    }

    [Fact]
    public async Task GetByIdAsync_returns_nothing_for_an_unknown_id()
    {
        Finds([]);

        var found = await _repository.GetByIdAsync("UTC|2026-11-01", Cancellation);

        found.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_translates_the_predicate_into_a_filter()
    {
        var stored = Entity(_capturedAt);
        Finds([stored]);
        List<string> ids = ["UTC|2026-10-31", "UTC|2026-11-01"];

        var found = await _repository.QueryAsync(snapshot => ids.Contains(snapshot.Id), Cancellation);

        found.Should().Equal(stored);
        Rendered(_filters.Single()).Should().Be("""{ "_id" : { "$in" : ["UTC|2026-10-31", "UTC|2026-11-01"] } }""");
    }

    [Fact]
    public async Task UpsertAsync_replaces_the_entity_stored_under_the_same_id()
    {
        var entity = Entity(_capturedAt);

        await _repository.UpsertAsync(entity, Cancellation);

        Collection.Verify(collection => collection.ReplaceOneAsync(
            It.Is<FilterDefinition<SnapshotEntity>>(filter => Rendered(filter) == """{ "_id" : "UTC|2026-11-01" }"""),
            entity,
            It.Is<ReplaceOptions>(options => options.IsUpsert),
            Cancellation));
    }

    [Fact]
    public async Task UpsertIfAsync_writes_when_nothing_newer_is_stored()
    {
        var entity = Entity(_capturedAt);

        var result = await _repository.UpsertIfAsync(entity, Older(entity), Cancellation);

        result.Should().Be(new UpsertResult<SnapshotEntity>(true, entity));
        Collection.Verify(collection => collection.ReplaceOneAsync(
            It.Is<FilterDefinition<SnapshotEntity>>(filter => Rendered(filter) ==
                """{ "_id" : "UTC|2026-11-01", "CapturedAtUtc" : { "$lt" : { "$date" : "2026-11-01T18:00:00Z" } } }"""),
            entity,
            It.Is<ReplaceOptions>(options => options.IsUpsert),
            Cancellation));
    }

    [Fact]
    public async Task UpsertIfAsync_keeps_a_stored_entity_that_cannot_be_replaced()
    {
        var entity = Entity(_capturedAt);
        var newer = Entity(_capturedAt.AddMinutes(5));
        Replacing().ThrowsAsync(DuplicateKey());
        Finds([newer]);

        var result = await _repository.UpsertIfAsync(entity, Older(entity), Cancellation);

        result.Should().Be(new UpsertResult<SnapshotEntity>(false, newer));
    }

    [Fact]
    public async Task UpsertIfAsync_tries_again_when_a_concurrent_write_left_a_replaceable_entity()
    {
        var entity = Entity(_capturedAt);
        var older = Entity(_capturedAt.AddMinutes(-5));
        Collection
            .SetupSequence(collection => collection.ReplaceOneAsync(It.IsAny<FilterDefinition<SnapshotEntity>>(), entity,
                It.IsAny<ReplaceOptions>(), Cancellation))
            .ThrowsAsync(DuplicateKey())
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, BsonNull.Value));
        Finds([older]);

        var result = await _repository.UpsertIfAsync(entity, Older(entity), Cancellation);

        result.Should().Be(new UpsertResult<SnapshotEntity>(true, entity));
        Collection.Verify(collection => collection.ReplaceOneAsync(It.IsAny<FilterDefinition<SnapshotEntity>>(), entity,
            It.IsAny<ReplaceOptions>(), Cancellation), Times.Exactly(2));
    }

    private static SnapshotEntity Entity(DateTime capturedAt) =>
        new("UTC|2026-11-01", "2026-11-01", "UTC", capturedAt, capturedAt, new(1200, 66, 700, 13, 3, 500, 30, 12));

    private static Expression<Func<SnapshotEntity, bool>> Older(SnapshotEntity candidate) =>
        stored => stored.CapturedAtUtc < candidate.CapturedAtUtc;

    private static string Rendered(FilterDefinition<SnapshotEntity> filter)
    {
        var registry = BsonSerializer.SerializerRegistry;
        return filter.Render(new(registry.GetSerializer<SnapshotEntity>(), registry)).ToJson();
    }

    private static MongoWriteException DuplicateKey()
    {
        var error = (WriteError)Activator.CreateInstance(
            typeof(WriteError),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [ServerErrorCategory.DuplicateKey, 11000, "duplicate key", new BsonDocument()],
            culture: null)!;
        var connection = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));
        return new(connection, error, writeConcernError: null, innerException: null);
    }

    private void Finds(IReadOnlyList<SnapshotEntity> found)
    {
        var cursor = new Mock<IAsyncCursor<SnapshotEntity>>();
        cursor.SetupSequence(batches => batches.MoveNextAsync(Cancellation)).ReturnsAsync(true).ReturnsAsync(false);
        cursor.SetupGet(batches => batches.Current).Returns(found);
        Collection
            .Setup(collection => collection.FindAsync(Capture.In(_filters),
                It.IsAny<FindOptions<SnapshotEntity, SnapshotEntity>>(), Cancellation))
            .ReturnsAsync(cursor.Object);
    }

    private Moq.Language.Flow.ISetup<IMongoCollection<SnapshotEntity>, Task<ReplaceOneResult>> Replacing() =>
        Collection.Setup(collection => collection.ReplaceOneAsync(It.IsAny<FilterDefinition<SnapshotEntity>>(),
            It.IsAny<SnapshotEntity>(), It.IsAny<ReplaceOptions>(), Cancellation));
}
