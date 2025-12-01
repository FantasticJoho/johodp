namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Users.Commands;
using Johodp.Application.Users.Queries;
using Johodp.Application.Common.Mediator;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<UsersController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _mediator;
    private readonly UserManager<User> _userManager;

    public UsersController(
        ISender sender,
        ILogger<UsersController> logger,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        UserManager<User> userManager)
    {
        _sender = sender;
        _mediator = sender;
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
        _logger.LogInformation(
            "User registration requested for email: {Email}, tenant: {TenantId}, role: {Role}, scope: {Scope}", 
            command.Email, 
            command.TenantId.Value,
            command.Role,
            command.Scope);

        // Force CreateAsPending = true pour les appels API (l'app tierce demande la création)
        command.CreateAsPending = true;

        try
        {
            var result = await _sender.Send(command);
            
            // L'email d'activation sera envoyé automatiquement via l'Event Handler
            // (SendActivationEmailHandler) qui écoute UserPendingActivationEvent
            
            _logger.LogInformation(
                "User successfully registered via API: {Email}, UserId: {UserId}, Status: PendingActivation, Tenant: {TenantId}", 
                command.Email, 
                result.UserId,
                command.TenantId.Value);

            return Created($"/api/users/{result.UserId}", new
            {
                userId = result.UserId,
                email = result.Email,
                status = "PendingActivation",
                tenantId = command.TenantId.Value,
                role = command.Role,
                scope = command.Scope,
                message = "User created successfully. Activation email will be sent."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("User registration failed for {Email}: {Error}", command.Email, ex.Message);
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during user registration for {Email}", command.Email);
            return StatusCode(500, new { error = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<Johodp.Application.Users.DTOs.UserDto>> GetUser(Guid userId)
    {
        _logger.LogDebug("Get user requested for UserId: {UserId}", userId);
        try
        {
            var query = new GetUserByIdQuery(userId);
            var result = await _sender.Send(query);
            _logger.LogDebug("User found: {UserId}, Email: {Email}", userId, result.Email);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", userId);
            throw;
        }
    }

    // Note: Multi-tenant user management endpoints removed.
    // Users now belong to a single tenant with role and scope stored directly in User entity.
    // To update role/scope, use PATCH/PUT on the user entity itself.
}
