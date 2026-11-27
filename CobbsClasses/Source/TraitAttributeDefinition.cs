namespace CobbsClasses.Source
{
    public sealed record TraitAttributeDefinition(
        string Code,
        AttributeKind Kind,
        double DefaultValue,
        double Minimum,
        double Maximum,
        string[]? RequiredMods = null
    );
}
