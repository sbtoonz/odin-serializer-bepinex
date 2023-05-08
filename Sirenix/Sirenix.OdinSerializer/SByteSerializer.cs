namespace Sirenix.OdinSerializer;

public sealed class SByteSerializer : Serializer<sbyte>
{
	public override sbyte ReadValue(IDataReader reader)
	{
		string name;
		EntryType entryType = reader.PeekEntry(out name);
		if (entryType == EntryType.Integer)
		{
			if (!reader.ReadSByte(out var value))
			{
				reader.Context.Config.DebugContext.LogWarning("Failed to read entry '" + name + "' of type " + entryType);
			}
			return value;
		}
		reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.Integer.ToString() + ", but got entry '" + name + "' of type " + entryType);
		reader.SkipEntry();
		return 0;
	}

	public override void WriteValue(string name, sbyte value, IDataWriter writer)
	{
		writer.WriteSByte(name, value);
	}
}
