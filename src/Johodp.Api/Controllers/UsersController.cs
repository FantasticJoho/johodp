namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.Users.Commands;
using Johodp.Application.Users.Queries;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;
    private readonly IUserRepository _userRepository;
    private readonly AddUserToTenantCommandHandler _addUserToTenantHandler;
    private readonly RemoveUserFromTenantCommandHandler _removeUserFromTenantHandler;

    public UsersController(
        IMediator mediator, 
        ILogger<UsersController> logger,
        IUserRepository userRepository,
        AddUserToTenantCommandHandler addUserToTenantHandler,
        RemoveUserFromTenantCommandHandler removeUserFromTenantHandler)
    {
        _mediator = mediator;
        _logger = logger;
        _userRepository = userRepository;
        _addUserToTenantHandler = addUserToTenantHandler;
        _removeUserFromTenantHandler = removeUserFromTenantHandler;
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
            "User registration requested for email: {Email}, tenant: {TenantId}", 
            command.Email, 
            command.TenantId);

        // Force CreateAsPending = true pour les appels API (l'app tierce demande la création)
        command.CreateAsPending = true;

        try
        {
            var result = await _mediator.Send(command);
            
            _logger.LogInformation(
                "User successfully registered via API: {Email}, UserId: {UserId}, Status: PendingActivation", 
                command.Email, 
                result.UserId);

            return Created($"/api/users/{result.UserId}", new
            {
                userId = result.UserId,
                email = result.Email,
                status = "PendingActivation",
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
            var result = await _mediator.Send(query);
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

    /// <summary>
    /// Add user to a tenant
    /// </summary>
    [HttpPost("{userId}/tenants/{tenantId}")]
    public async Task<IActionResult> AddToTenant(Guid userId, string tenantId)
    {
        _logger.LogInformation("Adding user {UserId} to tenant {TenantId}", userId, tenantId);
        
        try
        {
            var command = new AddUserToTenantCommand { UserId = userId, TenantId = tenantId };
            await _addUserToTenantHandler.Handle(command);
            
            _logger.LogInformation("Successfully added user {UserId} to tenant {TenantId}", userId, tenantId);
            return Ok(new { message = "User added to tenant successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to add user {UserId} to tenant {TenantId}: {Message}", userId, tenantId, ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to tenant {TenantId}", userId, tenantId);
            return StatusCode(500, "An error occurred while adding user to tenant");
        }
    }

    /// <summary>
    /// Remove user from a tenant
    /// </summary>
    [HttpDelete("{userId}/tenants/{tenantId}")]
    public async Task<IActionResult> RemoveFromTenant(Guid userId, string tenantId)
    {
        _logger.LogInformation("Removing user {UserId} from tenant {TenantId}", userId, tenantId);
        
        try
        {
            var command = new RemoveUserFromTenantCommand { UserId = userId, TenantId = tenantId };
            await _removeUserFromTenantHandler.Handle(command);
            
            _logger.LogInformation("Successfully removed user {UserId} from tenant {TenantId}", userId, tenantId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to remove user {UserId} from tenant {TenantId}: {Message}", userId, tenantId, ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from tenant {TenantId}", userId, tenantId);
            return StatusCode(500, "An error occurred while removing user from tenant");
        }
    }

    /// <summary>
    /// Get user's tenants
    /// </summary>
    [HttpGet("{userId}/tenants")]
    public async Task<ActionResult<List<string>>> GetUserTenants(Guid userId)
    {
        _logger.LogInformation("Getting tenants for user {UserId}", userId);
        
        try
        {
            var user = await _userRepository.GetByIdAsync(Johodp.Domain.Users.ValueObjects.UserId.From(userId));
            
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "User not found" });
            }

            _logger.LogInformation("User {UserId} belongs to {TenantCount} tenant(s)", userId, user.TenantIds.Count);
            return Ok(new { userId = userId, tenants = user.TenantIds });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenants for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving user tenants");
        }
    }
}
