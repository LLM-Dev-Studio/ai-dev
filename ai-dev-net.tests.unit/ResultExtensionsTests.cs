namespace AiDevNet.Tests.Unit;

public class ResultExtensionsTests
{
    [Fact]
    public async Task Then_WhenOk_ComposesNextResult()
    {
        Task<Result<int>> result = Task.FromResult<Result<int>>(new Ok<int>(2));

        var chained = await result.Then(value => Task.FromResult<Result<int>>(new Ok<int>(value * 3)));

        chained.ShouldBe(new Ok<int>(6));
    }

    [Fact]
    public async Task Then_WhenErr_PropagatesError()
    {
        var error = new DomainError("FAIL", "boom");
        Task<Result<int>> result = Task.FromResult<Result<int>>(new Err<int>(error));

        var chained = await result.Then(value => Task.FromResult<Result<int>>(new Ok<int>(value * 3)));

        chained.ShouldBe(new Err<int>(error));
    }

    [Fact]
    public void Match_WhenErr_ReturnsErrorProjection()
    {
        Result<int> result = new Err<int>(new DomainError("FAIL", "boom"));

        var message = result.Match(value => value.ToString(), error => error.Message);

        message.ShouldBe("boom");
    }
}
