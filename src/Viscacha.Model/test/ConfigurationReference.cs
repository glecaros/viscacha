namespace Viscacha.Model.Test;

public record ConfigurationReference(
    string Name,
    string Path
)
{
    public bool Baseline { get; init; }
};
