namespace Falcon.Contracts;

[Serializable]
public class ContractException : Exception
{
    public ContractException(string message) : base(message) { }
    public ContractException(string message, Exception exception) : base(message, exception)
    { }
    protected ContractException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));
        
        base.GetObjectData(info, context);
    }
}
