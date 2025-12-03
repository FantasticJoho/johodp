namespace Johodp.Application.CustomConfigurations.Commands;

using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Contracts.CustomConfigurations;
using Johodp.Domain.CustomConfigurations.ValueObjects;
using Microsoft.Extensions.Logging;

public class UpdateCustomConfigurationCommand : IRequest<Result<CustomConfigurationDto>>
{
    public Guid Id { get; set; }
    public UpdateCustomConfigurationDto Data { get; set; } = null!;
}

public class UpdateCustomConfigurationCommandHandler : BaseHandler<UpdateCustomConfigurationCommand, Result<CustomConfigurationDto>>
{
    private readonly ICustomConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomConfigurationCommandHandler(
        ICustomConfigurationRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateCustomConfigurationCommandHandler> logger) : base(logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task<Result<CustomConfigurationDto>> HandleCore(UpdateCustomConfigurationCommand command, CancellationToken cancellationToken)
    {
        var dto = command.Data;
        var configId = CustomConfigurationId.From(command.Id);

        // Get existing configuration
        var customConfig = await _repository.GetByIdAsync(configId);
        if (customConfig == null)
        {
            return Result<CustomConfigurationDto>.Failure(CustomConfigurationErrors.NotFound(command.Id));
        }

        // Update branding (colors, logo, images, CSS)
        customConfig.UpdateBranding(
            dto.PrimaryColor,
            dto.SecondaryColor,
            dto.LogoUrl,
            dto.BackgroundImageUrl,
            dto.CustomCss);

        // Update default language if provided
        if (!string.IsNullOrWhiteSpace(dto.DefaultLanguage))
        {
            customConfig.SetDefaultLanguage(dto.DefaultLanguage);
        }

        // Update description if provided
        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            customConfig.UpdateDescription(dto.Description);
        }

        // Update supported languages if provided
        if (dto.SupportedLanguages != null && dto.SupportedLanguages.Any())
        {
            // Clear existing languages (except default)
            var currentLanguages = customConfig.SupportedLanguages.ToList();
            foreach (var lang in currentLanguages)
            {
                if (lang != customConfig.DefaultLanguage)
                {
                    customConfig.RemoveSupportedLanguage(lang);
                }
            }

            // Add new languages
            foreach (var languageCode in dto.SupportedLanguages)
            {
                if (languageCode != customConfig.DefaultLanguage && 
                    !customConfig.SupportedLanguages.Contains(languageCode))
                {
                    customConfig.AddSupportedLanguage(languageCode);
                }
            }
        }

        // Save changes
        await _repository.UpdateAsync(customConfig);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return DTO
        return Result<CustomConfigurationDto>.Success(new CustomConfigurationDto
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
            DefaultLanguage = customConfig.DefaultLanguage,
            SupportedLanguages = customConfig.SupportedLanguages.ToList()
        });
    }
}
