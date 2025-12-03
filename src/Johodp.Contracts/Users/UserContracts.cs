namespace Johodp.Contracts.Users;

/// <summary>
/// User data transfer object
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public bool EmailConfirmed { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response after user registration
/// </summary>
public class RegisterUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
}
