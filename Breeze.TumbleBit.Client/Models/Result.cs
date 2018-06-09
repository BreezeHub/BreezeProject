namespace Breeze.TumbleBit.Client.Models
{
	public enum ResultStatus
	{
		Success,
		Error
	}

	public class Result
	{
	    public Result()
	    {
	    }

        public bool CanContinue { get; private set; }

		public ResultStatus Status { get; private set; }
		public bool Success => this.Status == ResultStatus.Success; 

	    public string Message { get; private set; }

	    public bool Failure => !Success;

        protected Result(ResultStatus status, string message = null, bool canContinue = true)
	    {
            this.CanContinue = canContinue;
            this.Status = status;
	        this.Message = message;
	    }

	    public static Result Fail(string message, bool canContinue)
	    {
	        return new Result(ResultStatus.Error, message, canContinue);
	    }

	    public static Result<T> Fail<T>(string message, bool canContinue)
	    {
	        return new Result<T>(default(T), ResultStatus.Error, message, canContinue);
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

	    protected internal Result(T value, ResultStatus status, string message = null, bool canContinue = true)
	        : base(status, message, canContinue)
	    { 
	    	Value = value;
	    }
	}
}

