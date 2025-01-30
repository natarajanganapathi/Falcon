namespace Falcon.Infrastructure.Abstractions;

[Serializable]
public class PersistenceException : Exception
{
    public PersistenceException(string message) : base(message) { }
    public PersistenceException(string message, Exception exception) : base(message, exception) { }

    protected PersistenceException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));
        
        base.GetObjectData(info, context);
    }
}
