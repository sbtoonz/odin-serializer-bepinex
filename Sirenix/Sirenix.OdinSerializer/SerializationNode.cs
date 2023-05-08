using System;

namespace Sirenix.OdinSerializer;

[Serializable]
public struct SerializationNode
{
	public string Name;

	public EntryType Entry;

	public string Data;
}
