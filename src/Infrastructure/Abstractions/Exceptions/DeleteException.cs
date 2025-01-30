namespace Falcon.Infrastructure.Abstractions;

[Serializable]
public class DeleteException : Exception
{
    public DeleteException(string message) : base(message) { }
    public DeleteException(string message, Exception exception) : base(message, exception) { }

    protected DeleteException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));
        
        base.GetObjectData(info, context);
    }
}
