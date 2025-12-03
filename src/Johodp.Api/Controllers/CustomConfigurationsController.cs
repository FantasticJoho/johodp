namespace Johodp.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Johodp.Application.Common.Mediator;
using Johodp.Application.Common.Interfaces;
using Johodp.Application.CustomConfigurations.Commands;
using Johodp.Application.CustomConfigurations.DTOs;
using Johodp.Domain.CustomConfigurations.ValueObjects;
using Johodp.Application.Common.Results;
using Johodp.Api.Extensions;

[ApiController]
[Route("api/custom-configurations")]
[AllowAnonymous]
public class CustomConfigurationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICustomConfigurationRepository _repository;
    private readonly ILogger<CustomConfigurationsController> _logger;

    public CustomConfigurationsController(
        ISender sender,
        ICustomConfigurationRepository repository,
        ILogger<CustomConfigurationsController> logger)
    {
        _sender = sender;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new custom configuration with branding and language settings
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomConfigurationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomConfigurationDto>> CreateCustomConfiguration([FromBody] CreateCustomConfigurationDto dto)
    {
        _logger.LogInformation("Creating custom configuration: {Name}", dto.Name);

        var result = await _sender.Send(new CreateCustomConfigurationCommand { Data = dto });
        if (!result.IsSuccess)
            return result.ToActionResult();

        _logger.LogInformation("Created custom configuration: {Id}", result.Value.Id);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Updates an existing custom configuration (branding, languages, etc.)
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomConfigurationDto>> UpdateCustomConfiguration(Guid id, [FromBody] UpdateCustomConfigurationDto dto)
    {
        _logger.LogInformation("Updating custom configuration: {Id}", id);

        var result = await _sender.Send(new UpdateCustomConfigurationCommand { Id = id, Data = dto });
        if (!result.IsSuccess)
            return result.ToActionResult();

        _logger.LogInformation("Updated custom configuration: {Id}", result.Value.Id);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets a custom configuration by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomConfigurationDto>> GetById(Guid id)
    {
        var config = await _repository.GetByIdAsync(CustomConfigurationId.From(id));
        if (config == null)
        {
            _logger.LogWarning("Custom configuration not found: {Id}", id);
            return NotFound();
        }

        return Ok(MapToDto(config));
    }

    /// <summary>
    /// Gets a custom configuration by name
    /// </summary>
    [HttpGet("by-name/{name}")]
    [ProducesResponseType(typeof(CustomConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomConfigurationDto>> GetByName(string name)
    {
        var config = await _repository.GetByNameAsync(name);
        if (config == null)
        {
            _logger.LogWarning("Custom configuration not found: {Name}", name);
            return NotFound();
        }

        return Ok(MapToDto(config));
    }

    /// <summary>
    /// Gets all custom configurations
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CustomConfigurationDto>>> GetAll()
    {
        var configurations = await _repository.GetAllAsync();
        return Ok(configurations.Select(MapToDto));
    }

    /// <summary>
    /// Gets all active custom configurations
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<CustomConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CustomConfigurationDto>>> GetActive()
    {
        _logger.LogInformation("Getting active custom configurations");

        var configurations = await _repository.GetActiveAsync();
        var dtos = configurations.Select(MapToDto);

        _logger.LogInformation("Retrieved {Count} active custom configurations", dtos.Count());
        return Ok(dtos);
    }

    private CustomConfigurationDto MapToDto(Domain.CustomConfigurations.Aggregates.CustomConfiguration config)
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
            DefaultLanguage = config.DefaultLanguage,
            SupportedLanguages = config.SupportedLanguages.ToList()
        };
    }
}
