namespace Falcon.Contracts;

public record ErrorResponse : IErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? StackTrace { get; set; }

    public class Builder
    {
        private readonly ErrorResponse _errorResponse = new();

        public Builder Message(string message)
        {
            _errorResponse.Message = message;
            return this;
        }
        public Builder Code(string code)
        {
            _errorResponse.Code = code;
            return this;
        }
        public Builder StackTrace(string stackTrace)
        {
#if DEBUG
            _errorResponse.StackTrace = stackTrace;
#endif
            return this;
        }

        public Builder FromException(Exception exception)
        {
            _errorResponse.Message = exception.Message;
            _errorResponse.StackTrace = InnerStackTrace(exception);
            return this;
        }

        private static string InnerStackTrace(Exception? exception)
        {
            if (exception is null)
                return string.Empty;
            var sb = new StringBuilder();
            int level = 0;
            for (var ex = exception; ex != null; ex = ex.InnerException)
            {
                sb.AppendFormat("{3}Level: {0}{3}, Exception: {1}{3}, StackTrace: {2}{3}",
                                level,
                                ex.Message,
                                ex.StackTrace,
                                Environment.NewLine);
                sb.AppendLine(new string('-', 40));
                level++;
            }
            return sb.ToString();
        }

        public ErrorResponse Build()
        {
            return _errorResponse;
        }
    }
}
