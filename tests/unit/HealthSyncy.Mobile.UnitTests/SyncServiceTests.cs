using HealthSyncy.Common.Contracts;
using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Health;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Mobile.Domain.ServiceAgents;
using HealthSyncy.Mobile.Domain.Services;
using HealthSyncy.Sync.Contracts;
using Microsoft.Extensions.Time.Testing;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class SyncServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo _tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
    private static readonly DateOnly _today = new(2026, 11, 2);
    private static readonly Pairing _pairing = new("https://sync.example.org", "device-token");
    private static readonly DailyTotals _totals = new(1200, 66, 700, 13, 3, 500, 30, 12);
    private static readonly DailyTotals _nothing = new(null, null, null, null, null, null, null, null);

    private readonly AutoMocker _mocker = new();
    private readonly SyncService _service;

    public SyncServiceTests()
    {
        var clock = new FakeTimeProvider(_now);
        clock.SetLocalTimeZone(_tokyo);
        _mocker.Use<TimeProvider>(clock);
        Reader.Setup(reader => reader.ReadAsync(It.IsAny<DateOnly>(), _tokyo)).ReturnsAsync(_nothing);
        _service = _mocker.CreateInstance<SyncService>();
    }

    private Mock<IHealthReader> Reader => _mocker.GetMock<IHealthReader>();

    private Mock<ISyncServiceAgent> Agent => _mocker.GetMock<ISyncServiceAgent>();

    private Mock<IPairingRepository> Pairings => _mocker.GetMock<IPairingRepository>();

    [Fact]
    public async Task Reads_the_last_seven_days_in_the_phones_zone()
    {
        Pairings.Setup(pairings => pairings.Load()).Returns(_pairing);

        await _service.RefreshAsync();

        foreach (var offset in Enumerable.Range(0, 7))
            Reader.Verify(reader => reader.ReadAsync(_today.AddDays(-offset), _tokyo), Times.Once);
        Reader.Verify(reader => reader.ReadAsync(It.IsAny<DateOnly>(), It.IsAny<TimeZoneInfo>()), Times.Exactly(7));
    }

    [Fact]
    public async Task Uploads_each_day_that_has_readable_activity()
    {
        Pairings.Setup(pairings => pairings.Load()).Returns(_pairing);
        Readable(_today, _today.AddDays(-5));

        await _service.RefreshAsync();

        var upload = new UploadRequest("Asia/Tokyo", _now, _totals);
        Agent.Verify(agent => agent.UploadAsync(_pairing, _today, upload), Times.Once);
        Agent.Verify(agent => agent.UploadAsync(_pairing, _today.AddDays(-5), upload), Times.Once);
        Agent.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Announces_how_many_days_were_uploaded()
    {
        List<int> announced = [];
        _service.Uploaded += (_, uploaded) => announced.Add(uploaded.Days);
        Pairings.Setup(pairings => pairings.Load()).Returns(_pairing);
        Readable(_today, _today.AddDays(-1), _today.AddDays(-6));

        await _service.RefreshAsync();

        announced.Should().Equal(3);
    }

    [Fact]
    public async Task Asks_to_pair_first_when_no_pairing_can_be_read()
    {
        var refresh = () => _service.RefreshAsync();

        await refresh.Should().ThrowAsync<SyncException>().WithMessage("Unlock your phone or pair it first.");
        Agent.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Pairs_once_an_upload_with_the_candidate_is_accepted()
    {
        Readable(_today);

        await _service.PairAsync(_pairing);

        Reader.Verify(reader => reader.AuthorizeAsync(), Times.Once);
        Agent.Verify(agent => agent.UploadAsync(_pairing, _today, new UploadRequest("Asia/Tokyo", _now, _totals)));
        Pairings.Verify(pairings => pairings.Save(_pairing), Times.Once);
    }

    [Fact]
    public async Task Keeps_the_current_pairing_when_the_candidate_is_rejected()
    {
        Readable(_today);
        Agent.Setup(agent => agent.UploadAsync(_pairing, _today, It.IsAny<UploadRequest>()))
            .ThrowsAsync(new SyncException("Upload failed (HTTP 401)."));

        var pair = () => _service.PairAsync(_pairing);

        await pair.Should().ThrowAsync<SyncException>().WithMessage("Upload failed (HTTP 401).");
        Pairings.Verify(pairings => pairings.Save(It.IsAny<Pairing>()), Times.Never);
    }

    [Fact]
    public async Task Keeps_the_current_pairing_when_there_is_no_activity_to_verify_with()
    {
        var pair = () => _service.PairAsync(_pairing);

        await pair.Should().ThrowAsync<SyncException>()
            .WithMessage("No readable activity to verify the pairing. Try again when activity is available.");
        Pairings.Verify(pairings => pairings.Save(It.IsAny<Pairing>()), Times.Never);
    }

    [Fact]
    public void Reports_whether_and_where_it_is_paired()
    {
        _service.IsPaired.Should().BeFalse();
        _service.PairedOrigin.Should().BeNull();

        Pairings.SetupGet(pairings => pairings.IsPaired).Returns(true);
        Pairings.Setup(pairings => pairings.Load()).Returns(_pairing);

        _service.IsPaired.Should().BeTrue();
        _service.PairedOrigin.Should().Be("https://sync.example.org");
    }

    [Fact]
    public async Task Uploads_again_whenever_Health_reports_a_change()
    {
        Func<Task> changed = () => Task.CompletedTask;
        Action<string> report = _ => { };
        Reader.Setup(reader => reader.ObserveAsync(It.IsAny<Func<Task>>(), report))
            .Callback((Func<Task> onChange, Action<string> _) => changed = onChange)
            .ReturnsAsync(["energy"]);
        Pairings.Setup(pairings => pairings.Load()).Returns(_pairing);
        Readable(_today);

        var unavailable = await _service.ObserveAsync(report);
        await changed();

        unavailable.Should().Equal("energy");
        Agent.Verify(agent => agent.UploadAsync(_pairing, _today, It.IsAny<UploadRequest>()), Times.Once);
    }

    private void Readable(params DateOnly[] days)
    {
        foreach (var day in days)
            Reader.Setup(reader => reader.ReadAsync(day, _tokyo)).ReturnsAsync(_totals);
    }
}
