namespace Johodp.Tests.Application;

using Xunit;
using Moq;
using Johodp.Application.Users.Commands;
using Johodp.Application.Common.Interfaces;
using Johodp.Domain.Users.Aggregates;

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IDomainEventPublisher> _domainEventPublisherMock;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _domainEventPublisherMock = new Mock<IDomainEventPublisher>();

        _unitOfWorkMock.Setup(x => x.Users).Returns(_userRepositoryMock.Object);
        
        _handler = new RegisterUserCommandHandler(
            _unitOfWorkMock.Object,
            _domainEventPublisherMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "test@example.com",
            FirstName = "Jean",
            LastName = "Dupont",
            CreateAsPending = false
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(command.Email, result.Email);
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _domainEventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<IEnumerable<Johodp.Domain.Common.DomainEvent>>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldThrowException()
    {
        // Arrange
        var existingUser = User.Create("test@example.com", "Existing", "User");
        var command = new RegisterUserCommand
        {
            Email = "test@example.com",
            FirstName = "Jean",
            LastName = "Dupont"
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command));
        
        Assert.Contains("already exists", exception.Message);
        _userRepositoryMock.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithPendingStatus_ShouldCreatePendingUser()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "pending@example.com",
            FirstName = "Pending",
            LastName = "User",
            CreateAsPending = true
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync((User?)null);

        User? capturedUser = null;
        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Callback<User>(user => capturedUser = user)
            .ReturnsAsync((User u) => u);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.NotNull(capturedUser);
        Assert.Equal(UserStatus.PendingActivation, capturedUser.Status);
        Assert.False(capturedUser.IsActive);
    }

    [Fact]
    public async Task Handle_WithTenantId_ShouldAssociateTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        var command = new RegisterUserCommand
        {
            Email = "tenant@example.com",
            FirstName = "Tenant",
            LastName = "User",
            TenantId = tenantId
        };

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync((User?)null);

        User? capturedUser = null;
        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Callback<User>(user => capturedUser = user)
            .ReturnsAsync((User u) => u);

        // Act
        await _handler.Handle(command);

        // Assert
        Assert.NotNull(capturedUser);
        Assert.Contains(tenantId, capturedUser.TenantIds);
    }
}
