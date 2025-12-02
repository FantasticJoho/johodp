namespace Johodp.Api.Extensions;

using Microsoft.AspNetCore.Mvc;
using Johodp.Application.Common.Results;

/// <summary>
/// Extension methods for converting Result to ActionResult
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result<T> to an appropriate ActionResult<T>
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return result.Error.Type switch
        {
            ErrorType.Validation => new BadRequestObjectResult(new
            {
                title = "Validation Error",
                detail = result.Error.Message,
                status = 400,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            ErrorType.NotFound => new NotFoundObjectResult(new
            {
                title = "Not Found",
                detail = result.Error.Message,
                status = 404,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            ErrorType.Conflict => new ConflictObjectResult(new
            {
                title = "Conflict",
                detail = result.Error.Message,
                status = 409,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            ErrorType.Forbidden => new ObjectResult(new
            {
                title = "Forbidden",
                detail = result.Error.Message,
                status = 403,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            })
            {
                StatusCode = 403
            },
            
            ErrorType.Unauthorized => new UnauthorizedObjectResult(new
            {
                title = "Unauthorized",
                detail = result.Error.Message,
                status = 401,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            _ => new ObjectResult(new
            {
                title = "Internal Server Error",
                detail = result.Error.Message,
                status = 500,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            })
            {
                StatusCode = 500
            }
        };
    }

    /// <summary>
    /// Converts a Result (without value) to an appropriate ActionResult
    /// </summary>
    public static ActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return result.Error.Type switch
        {
            ErrorType.Validation => new BadRequestObjectResult(new
            {
                title = "Validation Error",
                detail = result.Error.Message,
                status = 400,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            ErrorType.NotFound => new NotFoundObjectResult(new
            {
                title = "Not Found",
                detail = result.Error.Message,
                status = 404,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            ErrorType.Conflict => new ConflictObjectResult(new
            {
                title = "Conflict",
                detail = result.Error.Message,
                status = 409,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            ErrorType.Forbidden => new ObjectResult(new
            {
                title = "Forbidden",
                detail = result.Error.Message,
                status = 403,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            })
            {
                StatusCode = 403
            },
            
            ErrorType.Unauthorized => new UnauthorizedObjectResult(new
            {
                title = "Unauthorized",
                detail = result.Error.Message,
                status = 401,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            }),
            
            _ => new ObjectResult(new
            {
                title = "Internal Server Error",
                detail = result.Error.Message,
                status = 500,
                errorCode = result.Error.Code,
                metadata = result.Error.Metadata
            })
            {
                StatusCode = 500
            }
        };
    }

    /// <summary>
    /// Creates a CreatedAtAction result from a successful Result
    /// </summary>
    public static ActionResult<T> ToCreatedAtActionResult<T>(
        this Result<T> result,
        string actionName,
        object? routeValues)
    {
        if (result.IsSuccess)
        {
            return new CreatedAtActionResult(actionName, null, routeValues, result.Value);
        }

        return result.ToActionResult();
    }
}
