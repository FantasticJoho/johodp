namespace Johodp.Application.Common.Results;

/// <summary>
/// Represents the result of an operation that can succeed or fail
/// </summary>
/// <typeparam name="T">The type of the value returned on success</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// Indicates whether the operation succeeded
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the value if the operation succeeded
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result</exception>
    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access Value on a failed result. Check IsSuccess first.");

            return _value!;
        }
    }

    /// <summary>
    /// Gets the error if the operation failed
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result</exception>
    public Error Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error on a successful result. Check IsFailure first.");

            return _error!;
        }
    }

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result with a value
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with an error
    /// </summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(_value!);

        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(_error!);

        return this;
    }

    /// <summary>
    /// Maps the value to a new type if successful
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess
            ? Result<TNew>.Success(mapper(_value!))
            : Result<TNew>.Failure(_error!);
    }

    /// <summary>
    /// Binds the result to a new result-returning function if successful
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return IsSuccess
            ? binder(_value!)
            : Result<TNew>.Failure(_error!);
    }

    /// <summary>
    /// Matches the result to execute different actions based on success or failure
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Implicitly converts a value to a successful result
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an error to a failed result
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
}

/// <summary>
/// Represents the result of an operation that doesn't return a value
/// </summary>
public sealed class Result
{
    private readonly Error? _error;

    /// <summary>
    /// Indicates whether the operation succeeded
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error if the operation failed
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result</exception>
    public Error Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error on a successful result. Check IsFailure first.");

            return _error!;
        }
    }

    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with an error
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a successful result with a value
    /// </summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>
    /// Creates a failed result with a value type
    /// </summary>
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
            action();

        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(_error!);

        return this;
    }

    /// <summary>
    /// Implicitly converts an error to a failed result
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);
}
