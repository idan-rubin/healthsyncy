using System.Linq.Expressions;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;
using HealthSyncy.Sync.Contracts;
using HealthSyncy.Sync.Domain;
using Microsoft.Extensions.Time.Testing;

namespace HealthSyncy.Sync.UnitTests;

public sealed class UploadServiceTests
{
    private const string Day = "2026-11-01";
    private const string Zone = "America/New_York";

    private static readonly DateTimeOffset _capturedAt = new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);
    private static readonly UploadRequest _upload = new(Zone, _capturedAt, new(1200, 66, 700, 13, 3, 500, 30, 12));

    private readonly AutoMocker _mocker = new();
    private readonly FakeTimeProvider _clock = new(_capturedAt.AddMinutes(1));
    private readonly List<SnapshotEntity> _candidates = [];
    private readonly List<Expression<Func<SnapshotEntity, bool>>> _conditions = [];
    private readonly UploadService _service;

    public UploadServiceTests()
    {
        _mocker.Use<TimeProvider>(_clock);
        Upserting().ReturnsAsync((SnapshotEntity candidate, Expression<Func<SnapshotEntity, bool>> _,
            CancellationToken _) => new UpsertResult<SnapshotEntity>(true, candidate));
        _service = _mocker.CreateInstance<UploadService>();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Mock<IRepository<SnapshotEntity>> Snapshots => _mocker.GetMock<IRepository<SnapshotEntity>>();

    [Fact]
    public async Task Saves_the_first_snapshot_of_a_day()
    {
        var outcome = await _service.AcceptAsync(Day, _upload, Cancellation);

        outcome.Value.Should().Be(new Accepted(UploadStatus.Saved));
        _candidates.Should().Equal(new SnapshotEntity("America/New_York|2026-11-01", Day, Zone,
            _capturedAt.UtcDateTime, _clock.GetUtcNow().UtcDateTime, _upload.Totals));
    }

    [Fact]
    public async Task Replaces_only_snapshots_captured_earlier()
    {
        await _service.AcceptAsync(Day, _upload, Cancellation);

        var canReplace = _conditions.Single().Compile();
        canReplace(Stored(_capturedAt.AddMilliseconds(-1))).Should().BeTrue();
        canReplace(Stored(_capturedAt)).Should().BeFalse();
        canReplace(Stored(_capturedAt.AddMilliseconds(1))).Should().BeFalse();
    }

    [Fact]
    public async Task Keeps_capture_times_to_the_millisecond()
    {
        var precise = _upload with { CapturedAt = _capturedAt.AddTicks(12_345) };

        await _service.AcceptAsync(Day, precise, Cancellation);

        _candidates.Single().CapturedAtUtc.Should().Be(_capturedAt.UtcDateTime.AddMilliseconds(1));
    }

    [Fact]
    public async Task Reports_a_newer_stored_snapshot_as_superseded()
    {
        Keeps(Stored(_capturedAt.AddMinutes(5)));

        var outcome = await _service.AcceptAsync(Day, _upload, Cancellation);

        outcome.Value.Should().Be(new Accepted(UploadStatus.Superseded));
    }

    [Fact]
    public async Task Reports_the_same_snapshot_as_a_duplicate()
    {
        Keeps(Stored(_capturedAt));

        var outcome = await _service.AcceptAsync(Day, _upload, Cancellation);

        outcome.Value.Should().Be(new Accepted(UploadStatus.Duplicate));
    }

    [Fact]
    public async Task Reports_different_totals_at_the_same_capture_time_as_a_conflict()
    {
        Keeps(Stored(_capturedAt) with { Totals = _upload.Totals with { Steps = 999 } });

        var outcome = await _service.AcceptAsync(Day, _upload, Cancellation);

        outcome.Value.Should().BeOfType<Conflicting>();
    }

    [Fact]
    public async Task Pauses_while_uploads_are_switched_off()
    {
        _mocker.GetMock<IRepository<SettingsEntity>>()
            .Setup(repository => repository.GetByIdAsync(SettingsEntity.SyncId, Cancellation))
            .ReturnsAsync(new SettingsEntity(SettingsEntity.SyncId, false, 180));

        var outcome = await _service.AcceptAsync(Day, _upload, Cancellation);

        outcome.Value.Should().BeOfType<Paused>();
        _candidates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Eastern Standard Time")]
    [InlineData("invalid/timezone")]
    [InlineData("")]
    public async Task Rejects_a_time_zone_that_is_not_IANA(string zone)
    {
        var outcome = await _service.AcceptAsync(Day, _upload with { TimeZone = zone }, Cancellation);

        outcome.Value.Should().Be(new Rejected("Use an IANA time zone."));
        _candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_a_missing_capture_time()
    {
        var outcome = await _service.AcceptAsync(Day, _upload with { CapturedAt = default }, Cancellation);

        outcome.Value.Should().Be(new Rejected("The capture time is missing or in the future."));
    }

    [Fact]
    public async Task Rejects_a_capture_time_ahead_of_the_clock()
    {
        var ahead = _upload with { CapturedAt = _clock.GetUtcNow().AddMinutes(5).AddSeconds(1) };

        var outcome = await _service.AcceptAsync(Day, ahead, Cancellation);

        outcome.Value.Should().Be(new Rejected("The capture time is missing or in the future."));
    }

    [Fact]
    public async Task Tolerates_a_phone_clock_that_runs_five_minutes_fast()
    {
        var fast = _upload with { CapturedAt = _clock.GetUtcNow().AddMinutes(5) };

        var outcome = await _service.AcceptAsync(Day, fast, Cancellation);

        outcome.Value.Should().Be(new Accepted(UploadStatus.Saved));
    }

    [Fact]
    public async Task Rejects_a_day_later_than_the_capture_time()
    {
        var outcome = await _service.AcceptAsync("2026-11-02", _upload, Cancellation);

        outcome.Value.Should().Be(new Rejected("The day is later than the capture time."));
    }

    [Fact]
    public async Task Accepts_a_correction_for_an_earlier_day()
    {
        var outcome = await _service.AcceptAsync("2026-10-31", _upload, Cancellation);

        outcome.Value.Should().Be(new Accepted(UploadStatus.Saved));
    }

    [Fact]
    public async Task Rejects_totals_that_are_missing_or_out_of_range()
    {
        UploadRequest[] uploads =
        [
            _upload with { Totals = null! },
            _upload with { Totals = _upload.Totals with { Steps = -1 } },
            _upload with { Totals = new(null, null, null, null, null, null, null, null) }
        ];

        foreach (var upload in uploads)
        {
            var outcome = await _service.AcceptAsync(Day, upload, Cancellation);

            outcome.Value.Should().Be(new Rejected("The totals are missing or out of range."));
        }
    }

    private static SnapshotEntity Stored(DateTimeOffset capturedAt) => new(SnapshotEntity.IdFor(Day, Zone), Day, Zone,
        capturedAt.UtcDateTime, capturedAt.UtcDateTime, _upload.Totals);

    private void Keeps(SnapshotEntity stored) =>
        Upserting().ReturnsAsync(new UpsertResult<SnapshotEntity>(false, stored));

    private Moq.Language.Flow.ISetup<IRepository<SnapshotEntity>, Task<UpsertResult<SnapshotEntity>>> Upserting() =>
        Snapshots.Setup(repository => repository.UpsertIfAsync(Capture.In(_candidates), Capture.In(_conditions),
            Cancellation));
}
