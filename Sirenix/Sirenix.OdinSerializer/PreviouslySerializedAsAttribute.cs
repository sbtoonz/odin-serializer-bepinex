using System;

namespace Sirenix.OdinSerializer;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class PreviouslySerializedAsAttribute : Attribute
{
	public string Name { get; private set; }

	public PreviouslySerializedAsAttribute(string name)
	{
		Name = name;
	}
}
