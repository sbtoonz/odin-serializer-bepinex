namespace Sirenix.OdinSerializer;

public sealed class BooleanSerializer : Serializer<bool>
{
	public override bool ReadValue(IDataReader reader)
	{
		string name;
		EntryType entryType = reader.PeekEntry(out name);
		if (entryType == EntryType.Boolean)
		{
			if (!reader.ReadBoolean(out var value))
			{
				reader.Context.Config.DebugContext.LogWarning("Failed to read entry '" + name + "' of type " + entryType);
			}
			return value;
		}
		reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.Boolean.ToString() + ", but got entry '" + name + "' of type " + entryType);
		reader.SkipEntry();
		return false;
	}

	public override void WriteValue(string name, bool value, IDataWriter writer)
	{
		writer.WriteBoolean(name, value);
	}
}
