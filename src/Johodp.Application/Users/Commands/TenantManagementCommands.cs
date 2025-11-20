namespace Johodp.Application.Users.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.ValueObjects;

public class AddUserToTenantCommand
{
    public Guid UserId { get; set; }
    public string TenantId { get; set; } = string.Empty;
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
        var tenant = await _tenantRepository.GetByNameAsync(command.TenantId);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant '{command.TenantId}' not found");
        }

        user.AddTenant(command.TenantId);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }
}

public class RemoveUserFromTenantCommand
{
    public Guid UserId { get; set; }
    public string TenantId { get; set; } = string.Empty;
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
