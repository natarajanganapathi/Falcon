namespace Falcon.Infrastructure.Abstractions;

[Serializable]
public class QueryException : Exception
{
    public QueryException(string message) : base(message) { }
    public QueryException(string message, Exception exception) : base(message, exception) { }

    protected QueryException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));
        
        base.GetObjectData(info, context);
    }
}
