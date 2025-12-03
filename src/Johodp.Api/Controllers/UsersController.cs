namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Johodp.Contracts.Users;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Users.Commands;
using Johodp.Application.Users.Queries;
using Johodp.Application.Common.Mediator;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;
using Johodp.Application.Common.Results;
using Johodp.Api.Extensions;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<UsersController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<User> _userManager;

    public UsersController(
        ISender sender,
        ILogger<UsersController> logger,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        UserManager<User> userManager)
    {
        _sender = sender;
        _logger = logger;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    /// <summary>
    /// Register a new user (called by external application after validation)
    /// TODO: Add API key authentication in the future
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterUserResponse>> Register([FromBody] RegisterUserCommand command)
    {
        _logger.LogInformation("User registration: {Email}, tenant: {TenantId}, role: {Role}, scope: {Scope}",
            command.Email, command.TenantId.Value, command.Role, command.Scope);

        // Force CreateAsPending for API calls (external app requests creation)
        command.CreateAsPending = true;

        var result = await _sender.Send(command);
        if (!result.IsSuccess)
            return result.ToActionResult();

        // Activation email sent automatically via SendActivationEmailHandler (listens to UserPendingActivationEvent)
        _logger.LogInformation("User registered: {Email}, UserId: {UserId}, Status: PendingActivation, Tenant: {TenantId}",
            command.Email, result.Value.UserId, command.TenantId.Value);

        return Created($"/api/users/{result.Value.UserId}", new
        {
            userId = result.Value.UserId,
            email = result.Value.Email,
            status = "PendingActivation",
            tenantId = command.TenantId.Value,
            role = command.Role,
            scope = command.Scope,
            message = "User created successfully. Activation email will be sent."
        });
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid userId)
    {
        var result = await _sender.Send(new GetUserByIdQuery(userId));
        if (result.IsSuccess)
            _logger.LogDebug("User found: {UserId}, Email: {Email}", userId, result.Value.Email);
        return result.ToActionResult();
    }

    // Note: Multi-tenant user management endpoints removed.
    // Users now belong to a single tenant with role and scope stored directly in User entity.
    // To update role/scope, use PATCH/PUT on the user entity itself.
}
