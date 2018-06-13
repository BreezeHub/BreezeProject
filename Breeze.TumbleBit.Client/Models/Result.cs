namespace Breeze.TumbleBit.Client.Models
{
	public enum ResultStatus
	{
		Success,
		Error
	}

    public enum PostResultActionType
    {
        CanContinue,
        ShouldStop
    }

	public class Result
	{
	    public Result()
	    {
	    }

        public PostResultActionType PostResultAction { get; private set; }

		public ResultStatus Status { get; private set; }
		public bool Success => this.Status == ResultStatus.Success; 

	    public string Message { get; private set; }

	    public bool Failure => !Success;

        protected Result(ResultStatus status, string message = null, PostResultActionType postResultAction = PostResultActionType.CanContinue)
	    {
            this.PostResultAction = postResultAction;
            this.Status = status;
	        this.Message = message;
	    }

	    public static Result Fail(string message, PostResultActionType postResultAction)
	    {
	        return new Result(ResultStatus.Error, message, postResultAction);
	    }

	    public static Result<T> Fail<T>(string message, PostResultActionType postResultAction)
	    {
	        return new Result<T>(default(T), ResultStatus.Error, message, postResultAction);
	    }

	    public static Result Ok()
	    {
	        return new Result(ResultStatus.Success, string.Empty);
	    }

	    public static Result<T> Ok<T>(T value)
	    {
	        return new Result<T>(value, ResultStatus.Success, string.Empty);
	    }

	    public static Result Combine(params Result[] results)
	    {
	        foreach (Result result in results)
	        {
	            if (result.Failure)
	                return result;
	        }

	        return Ok();
	    }
	}


	public class Result<T> : Result
	{
	    public Result()
	    {
	        
	    }

	    private T _value;

		public T Value { get; set; }

	    protected internal Result(T value, ResultStatus status, string message = null, PostResultActionType postResultAction = PostResultActionType.CanContinue)
	        : base(status, message, postResultAction)
	    { 
	    	Value = value;
	    }
	}
}

