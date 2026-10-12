namespace Extensions.Infrastructure.Ai;

/// <summary>CV payload accepted by AI endpoints; every field is optional because it comes straight from a draft.</summary>
public sealed record PortfolioInputData(
    PortfolioPersonalInfoInput? PersonalInfo,
    string? Summary,
    IReadOnlyList<PortfolioExperienceInput>? Experience,
    IReadOnlyList<PortfolioProjectInput>? Projects,
    IReadOnlyList<PortfolioSkillInput>? Skills,
    IReadOnlyList<PortfolioEducationInput>? Education,
    IReadOnlyList<PortfolioCertificationInput>? Certifications,
    IReadOnlyList<PortfolioLanguageInput>? Languages
);

/// <summary>Contact and identity section of a CV.</summary>
public sealed record PortfolioPersonalInfoInput(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? Location,
    string? LinkedIn,
    string? Website
);

/// <summary>A work experience entry.</summary>
public sealed record PortfolioExperienceInput(
    string? Company,
    string? Position,
    string? StartDate,
    string? EndDate,
    bool Current,
    string? Description
);

/// <summary>A project entry.</summary>
public sealed record PortfolioProjectInput(
    string? Name,
    string? Description,
    IReadOnlyList<string>? Technologies,
    string? Link
);

/// <summary>A skill with its proficiency level.</summary>
public sealed record PortfolioSkillInput(
    string? Name,
    string? Level
);

/// <summary>An education entry.</summary>
public sealed record PortfolioEducationInput(
    string? Institution,
    string? Degree,
    string? Field,
    string? StartDate,
    string? EndDate
);

/// <summary>A certification entry.</summary>
public sealed record PortfolioCertificationInput(
    string? Name,
    string? Issuer,
    string? Date
);

/// <summary>A spoken language with its level.</summary>
public sealed record PortfolioLanguageInput(
    string? Name,
    string? Level
);
