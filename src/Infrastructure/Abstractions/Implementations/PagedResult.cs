namespace Falcon.Infrastructure.Abstractions;

public record PagedResult<TResult>
{
    public IEnumerable<TResult> Items { get; set; } = Array.Empty<TResult>();
    public long Count { get; set; }
}