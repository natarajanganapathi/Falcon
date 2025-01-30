namespace Falcon.Contracts;

public interface ICompositeRequest : IApiRequest
{
    Dictionary<string, IApiRequest> Requests { get; set; }
}