namespace Johodp.Application.Users.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.ValueObjects;
using Johodp.Domain.Tenants.ValueObjects;

public class AddUserToTenantCommand
{
    public Guid UserId { get; set; }
    public TenantId TenantId { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}

public class AddUserToTenantCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddUserToTenantCommandHandler(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AddUserToTenantCommand command)
    {
        var userId = UserId.From(command.UserId);
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{command.UserId}' not found");
        }

        // Verify tenant exists
        var tenant = await _tenantRepository.GetByIdAsync(command.TenantId);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with ID '{command.TenantId.Value}' not found");
        }

        user.AddTenant(command.TenantId, command.Role, command.Scope);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class RemoveUserFromTenantCommand
{
    public Guid UserId { get; set; }
    public TenantId TenantId { get; set; } = null!;
}

public class RemoveUserFromTenantCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveUserFromTenantCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RemoveUserFromTenantCommand command)
    {
        var userId = UserId.From(command.UserId);
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{command.UserId}' not found");
        }

        user.RemoveTenant(command.TenantId);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }
}
