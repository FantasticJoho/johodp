namespace Johodp.Tests;

using Xunit;
using Johodp.Domain.Users.Aggregates;
using Johodp.Domain.Users.ValueObjects;

public class UserAggregateTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var email = "test@example.com";
        var firstName = "Jean";
        var lastName = "Dupont";

        // Act
        var user = User.Create(email, firstName, lastName);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(email, user.Email.Value);
        Assert.Equal(firstName, user.FirstName);
        Assert.Equal(lastName, user.LastName);
        Assert.False(user.EmailConfirmed);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        // Arrange
        var email = "test@example.com";
        var firstName = "Jean";
        var lastName = "Dupont";

        // Act
        var user = User.Create(email, firstName, lastName);

        // Assert
        Assert.Single(user.DomainEvents);
        var @event = user.DomainEvents.First();
        Assert.IsType<Domain.Users.Events.UserRegisteredEvent>(@event);
    }

    [Fact]
    public void ConfirmEmail_ShouldSetEmailConfirmed()
    {
        // Arrange
        var user = User.Create("test@example.com", "Jean", "Dupont");

        // Act
        user.ConfirmEmail();

        // Assert
        Assert.True(user.EmailConfirmed);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void ConfirmEmail_WhenAlreadyConfirmed_ShouldThrow()
    {
        // Arrange
        var user = User.Create("test@example.com", "Jean", "Dupont");
        user.ConfirmEmail();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => user.ConfirmEmail());
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var user = User.Create("test@example.com", "Jean", "Dupont");

        // Act
        user.Deactivate();

        // Assert
        Assert.False(user.IsActive);
    }
}

public class EmailValueObjectTests
{
    [Fact]
    public void Create_WithValidEmail_ShouldSucceed()
    {
        // Act
        var email = Email.Create("test@example.com");

        // Assert
        Assert.Equal("test@example.com", email.Value);
    }

    [Fact]
    public void Create_WithInvalidEmail_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Email.Create("invalid-email"));
    }

    [Fact]
    public void Create_WithEmptyEmail_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Email.Create(""));
    }

    [Fact]
    public void Equals_WithSameValue_ShouldBeEqual()
    {
        // Arrange
        var email1 = Email.Create("test@example.com");
        var email2 = Email.Create("test@example.com");

        // Act & Assert
        Assert.Equal(email1, email2);
    }
}
