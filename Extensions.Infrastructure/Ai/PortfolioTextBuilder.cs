namespace Extensions.Infrastructure.Ai;

/// <summary>Renders a <see cref="PortfolioInputData"/> as plain text for LLM prompts.</summary>
public static class PortfolioTextBuilder
{
    /// <summary>Builds the prompt text; contact details are included only when <paramref name="includeContactDetails"/> is set.</summary>
    public static string Build(PortfolioInputData portfolio, bool includeContactDetails = false)
    {
        var parts = new List<string>();

        if (portfolio.PersonalInfo is not null)
        {
            var info = portfolio.PersonalInfo;
            if (!string.IsNullOrWhiteSpace(info.FirstName) || !string.IsNullOrWhiteSpace(info.LastName))
                parts.Add($"Name: {info.FirstName} {info.LastName}".Trim());

            if (includeContactDetails)
            {
                if (!string.IsNullOrWhiteSpace(info.Email))
                    parts.Add($"Email: {info.Email}");
                if (!string.IsNullOrWhiteSpace(info.Phone))
                    parts.Add($"Phone: {info.Phone}");
            }

            if (!string.IsNullOrWhiteSpace(info.Location))
                parts.Add($"Location: {info.Location}");

            if (includeContactDetails)
            {
                if (!string.IsNullOrWhiteSpace(info.LinkedIn))
                    parts.Add($"LinkedIn: {info.LinkedIn}");
                if (!string.IsNullOrWhiteSpace(info.Website))
                    parts.Add($"Website: {info.Website}");
            }
        }

        if (!string.IsNullOrWhiteSpace(portfolio.Summary))
            parts.Add($"\nProfessional summary:\n{portfolio.Summary}");

        if (portfolio.Skills is { Count: > 0 })
        {
            var skills = portfolio.Skills
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .Select(s => $"{s.Name} ({s.Level ?? "Unknown"})");
            parts.Add($"\nSkills: {string.Join(", ", skills)}");
        }

        if (portfolio.Experience is { Count: > 0 })
        {
            parts.Add("\nWork experience:");
            foreach (var exp in portfolio.Experience)
            {
                var period = exp.Current ? $"{exp.StartDate} - present" : $"{exp.StartDate} - {exp.EndDate}";
                parts.Add($"- {exp.Position} at {exp.Company} ({period})");
                if (!string.IsNullOrWhiteSpace(exp.Description))
                    parts.Add($"  {exp.Description}");
            }
        }

        if (portfolio.Projects is { Count: > 0 })
        {
            parts.Add("\nProjects:");
            foreach (var proj in portfolio.Projects)
            {
                parts.Add($"- {proj.Name}: {proj.Description}");
                if (proj.Technologies is { Count: > 0 })
                    parts.Add($"  Technologies: {string.Join(", ", proj.Technologies)}");
            }
        }

        if (portfolio.Education is { Count: > 0 })
        {
            parts.Add("\nEducation:");
            foreach (var edu in portfolio.Education)
                parts.Add($"- {edu.Degree} in {edu.Field} at {edu.Institution} ({edu.StartDate} - {edu.EndDate})");
        }

        if (portfolio.Certifications is { Count: > 0 })
        {
            parts.Add("\nCertifications:");
            foreach (var cert in portfolio.Certifications)
                parts.Add($"- {cert.Name} by {cert.Issuer} ({cert.Date})");
        }

        if (portfolio.Languages is { Count: > 0 })
        {
            var langs = portfolio.Languages
                .Where(l => !string.IsNullOrWhiteSpace(l.Name))
                .Select(l => $"{l.Name} ({l.Level ?? "Unknown"})");
            parts.Add($"\nLanguages: {string.Join(", ", langs)}");
        }

        return string.Join("\n", parts);
    }
}
