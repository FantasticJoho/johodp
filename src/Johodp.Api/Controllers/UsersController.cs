namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MediatR;
using Johodp.Application.Users.Commands;
using Johodp.Application.Users.Queries;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterUserResponse>> Register(RegisterUserCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<Johodp.Application.Users.DTOs.UserDto>> GetUser(Guid userId)
    {
        try
        {
            var query = new GetUserByIdQuery(userId);
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
