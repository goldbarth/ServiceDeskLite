using ServiceDeskLite.Application.Common;
using FluentAssertions;

namespace ServiceDeskLite.Tests.Application;

public class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var r = Result.Success();
        r.IsSuccess.Should().BeTrue();
        r.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_has_error()
    {
        var err = ApplicationError.Validation("val.test", "nope");
        var r = Result.Failure(err);
        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(err);
    }
    
    [Fact]
    public void Generic_success_contains_value()
    {
        var r = Result<string>.Success("OK");
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be("OK");
    }
}
