using System;
using JetBrains.Annotations;

namespace Sirenix.OdinSerializer;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class OdinSerializeAttribute : Attribute
{
}
