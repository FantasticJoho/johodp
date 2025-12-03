namespace Johodp.Application.CustomConfigurations.Queries;

using Johodp.Application.Common.Interfaces;
using Johodp.Messaging.Mediator;
using Johodp.Application.Common.Results;
using Johodp.Contracts.CustomConfigurations;
using Johodp.Domain.CustomConfigurations.ValueObjects;
using Microsoft.Extensions.Logging;

public class GetCustomConfigurationByIdQuery : IRequest<Result<CustomConfigurationDto>>
{
    public Guid CustomConfigurationId { get; set; }
}

public class GetCustomConfigurationByIdQueryHandler : BaseHandler<GetCustomConfigurationByIdQuery, Result<CustomConfigurationDto>>
{
    private readonly ICustomConfigurationRepository _repository;

    public GetCustomConfigurationByIdQueryHandler(
        ICustomConfigurationRepository repository,
        ILogger<GetCustomConfigurationByIdQueryHandler> logger) : base(logger)
    {
        _repository = repository;
    }

    protected override async Task<Result<CustomConfigurationDto>> HandleCore(GetCustomConfigurationByIdQuery query, CancellationToken cancellationToken)
    {
        var configId = CustomConfigurationId.From(query.CustomConfigurationId);
        var config = await _repository.GetByIdAsync(configId);

        if (config == null)
        {
            return Result<CustomConfigurationDto>.Failure(Error.NotFound(
                "CUSTOM_CONFIG_NOT_FOUND",
                $"CustomConfiguration with ID '{query.CustomConfigurationId}' not found"));
        }

        return Result<CustomConfigurationDto>.Success(MapToDto(config));
    }

    private static CustomConfigurationDto MapToDto(Domain.CustomConfigurations.Aggregates.CustomConfiguration config)
    {
        return new CustomConfigurationDto
        {
            Id = config.Id.Value,
            Name = config.Name,
            Description = config.Description,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
            PrimaryColor = config.PrimaryColor,
            SecondaryColor = config.SecondaryColor,
            LogoUrl = config.LogoUrl,
            BackgroundImageUrl = config.BackgroundImageUrl,
            CustomCss = config.CustomCss,
            SupportedLanguages = config.SupportedLanguages.ToList(),
            DefaultLanguage = config.DefaultLanguage
        };
    }
}

public class GetAllCustomConfigurationsQuery : IRequest<IEnumerable<CustomConfigurationDto>> { }

public class GetAllCustomConfigurationsQueryHandler : BaseHandler<GetAllCustomConfigurationsQuery, IEnumerable<CustomConfigurationDto>>
{
    private readonly ICustomConfigurationRepository _repository;

    public GetAllCustomConfigurationsQueryHandler(
        ICustomConfigurationRepository repository,
        ILogger<GetAllCustomConfigurationsQueryHandler> logger) : base(logger)
    {
        _repository = repository;
    }

    protected override async Task<IEnumerable<CustomConfigurationDto>> HandleCore(GetAllCustomConfigurationsQuery query, CancellationToken cancellationToken)
    {
        var configs = await _repository.GetAllAsync();
        return configs.Select(MapToDto);
    }

    private static CustomConfigurationDto MapToDto(Domain.CustomConfigurations.Aggregates.CustomConfiguration config)
    {
        return new CustomConfigurationDto
        {
            Id = config.Id.Value,
            Name = config.Name,
            Description = config.Description,
            IsActive = config.IsActive,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
            PrimaryColor = config.PrimaryColor,
            SecondaryColor = config.SecondaryColor,
            LogoUrl = config.LogoUrl,
            BackgroundImageUrl = config.BackgroundImageUrl,
            CustomCss = config.CustomCss,
            SupportedLanguages = config.SupportedLanguages.ToList(),
            DefaultLanguage = config.DefaultLanguage
        };
    }
}
