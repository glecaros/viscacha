using System.Collections.Generic;
using YAYL.Attributes;

namespace Viscacha.CLI.Test.Model;

[YamlPolymorphic("type")]
[YamlDerivedType("all", typeof(All))]
[YamlDerivedType("single", typeof(Single))]
[YamlDerivedType("multiple", typeof(Multiple))]
public abstract record Target()
{
    public record All() : Target;
    public record Single(int Index) : Target;
    public record Multiple(List<int> Indices) : Target;
};
