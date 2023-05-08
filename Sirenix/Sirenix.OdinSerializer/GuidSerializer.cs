using System;

namespace Sirenix.OdinSerializer;

public sealed class GuidSerializer : Serializer<Guid>
{
	public override Guid ReadValue(IDataReader reader)
	{
		string name;
		EntryType entryType = reader.PeekEntry(out name);
		if (entryType == EntryType.Guid)
		{
			if (!reader.ReadGuid(out var value))
			{
				reader.Context.Config.DebugContext.LogWarning("Failed to read entry '" + name + "' of type " + entryType);
			}
			return value;
		}
		reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.Guid.ToString() + ", but got entry '" + name + "' of type " + entryType);
		reader.SkipEntry();
		return default(Guid);
	}

	public override void WriteValue(string name, Guid value, IDataWriter writer)
	{
		writer.WriteGuid(name, value);
	}
}
