namespace Falcon.Contracts;

public class CommandRequest<TEntity> : ICommandRequest<TEntity>
{
    public CommandRequest(TEntity data = default)
    {
        Data = data;
    }
    public required TEntity Data { get; set; }
}