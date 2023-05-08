namespace Sirenix.OdinSerializer;

public sealed class CharSerializer : Serializer<char>
{
	public override char ReadValue(IDataReader reader)
	{
		string name;
		EntryType entryType = reader.PeekEntry(out name);
		if (entryType == EntryType.String)
		{
			if (!reader.ReadChar(out var value))
			{
				reader.Context.Config.DebugContext.LogWarning("Failed to read entry '" + name + "' of type " + entryType);
			}
			return value;
		}
		reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.String.ToString() + ", but got entry '" + name + "' of type " + entryType);
		reader.SkipEntry();
		return '\0';
	}

	public override void WriteValue(string name, char value, IDataWriter writer)
	{
		writer.WriteChar(name, value);
	}
}
