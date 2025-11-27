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
    private readonly AddUserToTenantCommandHandler _addUserToTenantHandler;
    private readonly RemoveUserFromTenantCommandHandler _removeUserFromTenantHandler;

    public UsersController(
        ISender sender,
        ILogger<UsersController> logger,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        UserManager<User> userManager,
        AddUserToTenantCommandHandler addUserToTenantHandler,
        RemoveUserFromTenantCommandHandler removeUserFromTenantHandler)
    {
        _sender = sender;
        _mediator = sender;
        _logger = logger;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
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
            "User registration requested for email: {Email}, tenants: {TenantCount}", 
            command.Email, 
            command.Tenants?.Count ?? (command.TenantId != null ? 1 : 0));

        // Force CreateAsPending = true pour les appels API (l'app tierce demande la création)
        command.CreateAsPending = true;

        try
        {
            var result = await _sender.Send(command);
            
            // L'email d'activation sera envoyé automatiquement via l'Event Handler
            // (SendActivationEmailHandler) qui écoute UserPendingActivationEvent
            
            _logger.LogInformation(
                "User successfully registered via API: {Email}, UserId: {UserId}, Status: PendingActivation, Tenants: {TenantCount}", 
                command.Email, 
                result.UserId,
                command.Tenants?.Count ?? (command.TenantId != null ? 1 : 0));

            return Created($"/api/users/{result.UserId}", new
            {
                userId = result.UserId,
                email = result.Email,
                status = "PendingActivation",
                tenantCount = command.Tenants?.Count ?? (command.TenantId != null ? 1 : 0),
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

    /// <summary>
    /// Add user to a tenant
    /// </summary>
    [HttpPost("{userId}/tenants")]
    public async Task<IActionResult> AddToTenant(Guid userId, [FromBody] AddUserToTenantRequest request)
    {
        _logger.LogInformation("Adding user {UserId} to tenant {TenantId}", userId, request.TenantId);
        
        try
        {
            var command = new AddUserToTenantCommand 
            { 
                UserId = userId, 
                TenantId = TenantId.From(request.TenantId),
                Role = request.Role,
                Scope = request.Scope
            };
            await _addUserToTenantHandler.Handle(command);
            
            _logger.LogInformation("Successfully added user {UserId} to tenant {TenantId}", userId, request.TenantId);
            return Ok(new { message = "User added to tenant successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to add user {UserId} to tenant {TenantId}: {Message}", userId, request.TenantId, ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to tenant {TenantId}", userId, request.TenantId);
            return StatusCode(500, "An error occurred while adding user to tenant");
        }
    }

    public class AddUserToTenantRequest
    {
        public Guid TenantId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }

    /// <summary>
    /// Remove user from a tenant
    /// </summary>
    [HttpDelete("{userId}/tenants/{tenantId:guid}")]
    public async Task<IActionResult> RemoveFromTenant(Guid userId, Guid tenantId)
    {
        _logger.LogInformation("Removing user {UserId} from tenant {TenantId}", userId, tenantId);
        
        try
        {
            var command = new RemoveUserFromTenantCommand { UserId = userId, TenantId = TenantId.From(tenantId) };
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
    /// Update user's role and scope for a specific tenant
    /// </summary>
    [HttpPut("{userId}/tenants/{tenantId:guid}")]
    public async Task<IActionResult> UpdateTenantRoleAndScope(Guid userId, Guid tenantId, [FromBody] UpdateTenantRoleAndScopeRequest request)
    {
        _logger.LogInformation("Updating role and scope for user {UserId} on tenant {TenantId}: role={Role}, scope={Scope}", 
            userId, tenantId, request.Role, request.Scope);
        
        try
        {
            var userIdVO = UserId.From(userId);
            var user = await _mediator.Send(new GetUserByIdQuery(userId));
            
            if (user == null)
                return NotFound(new { message = $"User with ID '{userId}' not found" });

            // Get domain user to update
            var domainUser = await _userRepository.GetByIdAsync(userIdVO);
            if (domainUser == null)
                return NotFound(new { message = $"User with ID '{userId}' not found" });

            domainUser.UpdateTenantRoleAndScope(TenantId.From(tenantId), request.Role, request.Scope);
            
            await _userRepository.UpdateAsync(domainUser);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Successfully updated role and scope for user {UserId} on tenant {TenantId}", userId, tenantId);
            return Ok(new { message = "Role and scope updated successfully", role = request.Role, scope = request.Scope });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update role/scope for user {UserId} on tenant {TenantId}: {Message}", userId, tenantId, ex.Message);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role/scope for user {UserId} on tenant {TenantId}", userId, tenantId);
            return StatusCode(500, "An error occurred while updating role and scope");
        }
    }

    public class UpdateTenantRoleAndScopeRequest
    {
        public string Role { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
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
