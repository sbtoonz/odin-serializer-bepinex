using System;

namespace Sirenix.OdinSerializer;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public sealed class AlwaysFormatsSelfAttribute : Attribute
{
}
