namespace Sirenix.OdinSerializer;

public sealed class DecimalSerializer : Serializer<decimal>
{
	public override decimal ReadValue(IDataReader reader)
	{
		string name;
		EntryType entryType = reader.PeekEntry(out name);
		if (entryType == EntryType.FloatingPoint || entryType == EntryType.Integer)
		{
			if (!reader.ReadDecimal(out var value))
			{
				reader.Context.Config.DebugContext.LogWarning("Failed to read entry of type " + entryType);
			}
			return value;
		}
		reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.FloatingPoint.ToString() + " or " + EntryType.Integer.ToString() + ", but got entry of type " + entryType);
		reader.SkipEntry();
		return 0m;
	}

	public override void WriteValue(string name, decimal value, IDataWriter writer)
	{
		writer.WriteDecimal(name, value);
	}
}
