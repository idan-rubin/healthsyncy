using HealthSyncy.Mobile.Domain;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class SyncExceptionTests
{
    [Fact]
    public void Carries_the_message_shown_on_the_phone_and_its_cause()
    {
        var cause = new HttpRequestException("offline");

        var error = new SyncException("Upload failed.", cause);

        error.Message.Should().Be("Upload failed.");
        error.InnerException.Should().BeSameAs(cause);
    }

    [Fact]
    public void Can_be_raised_without_details() => new SyncException().InnerException.Should().BeNull();
}
