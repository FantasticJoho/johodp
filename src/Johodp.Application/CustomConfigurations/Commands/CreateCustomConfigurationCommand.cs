namespace Johodp.Application.CustomConfigurations.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Application.CustomConfigurations.DTOs;
using Johodp.Domain.CustomConfigurations.Aggregates;

public class CreateCustomConfigurationCommand : IRequest<Result<CustomConfigurationDto>>
{
    public CreateCustomConfigurationDto Data { get; set; } = null!;
}

public class CreateCustomConfigurationCommandHandler : IRequestHandler<CreateCustomConfigurationCommand, Result<CustomConfigurationDto>>
{
    private readonly ICustomConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomConfigurationCommandHandler(
        ICustomConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CustomConfigurationDto>> Handle(CreateCustomConfigurationCommand command, CancellationToken cancellationToken = default)
    {
        var dto = command.Data;

        // Verify uniqueness
        var existing = await _repository.GetByNameAsync(dto.Name);
        if (existing != null)
        {
            return Result<CustomConfigurationDto>.Failure(CustomConfigurationErrors.AlreadyExists(dto.Name));
        }

        // Create aggregate
        var customConfig = CustomConfiguration.Create(
            dto.Name,
            dto.Description,
            dto.DefaultLanguage); // Can be null, will default to "fr-FR"

        // Apply branding if provided
        if (dto.PrimaryColor != null || dto.SecondaryColor != null || 
            dto.LogoUrl != null || dto.BackgroundImageUrl != null || dto.CustomCss != null)
        {
            customConfig.UpdateBranding(
                dto.PrimaryColor,
                dto.SecondaryColor,
                dto.LogoUrl,
                dto.BackgroundImageUrl,
                dto.CustomCss);
        }

        // Add additional supported languages
        if (dto.AdditionalLanguages != null)
        {
            foreach (var languageCode in dto.AdditionalLanguages)
            {
                customConfig.AddSupportedLanguage(languageCode);
            }
        }

        await _repository.AddAsync(customConfig);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CustomConfigurationDto>.Success(MapToDto(customConfig));
    }

    private static CustomConfigurationDto MapToDto(CustomConfiguration customConfig)
    {
        return new CustomConfigurationDto
        {
            Id = customConfig.Id.Value,
            Name = customConfig.Name,
            Description = customConfig.Description,
            IsActive = customConfig.IsActive,
            CreatedAt = customConfig.CreatedAt,
            UpdatedAt = customConfig.UpdatedAt,
            PrimaryColor = customConfig.PrimaryColor,
            SecondaryColor = customConfig.SecondaryColor,
            LogoUrl = customConfig.LogoUrl,
            BackgroundImageUrl = customConfig.BackgroundImageUrl,
            CustomCss = customConfig.CustomCss,
            SupportedLanguages = customConfig.SupportedLanguages.ToList(),
            DefaultLanguage = customConfig.DefaultLanguage
        };
    }
}
