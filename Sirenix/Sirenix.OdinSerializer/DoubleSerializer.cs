namespace Sirenix.OdinSerializer;

public sealed class DoubleSerializer : Serializer<double>
{
	public override double ReadValue(IDataReader reader)
	{
		string name;
		EntryType entryType = reader.PeekEntry(out name);
		if (entryType == EntryType.FloatingPoint || entryType == EntryType.Integer)
		{
			if (!reader.ReadDouble(out var value))
			{
				reader.Context.Config.DebugContext.LogWarning("Failed to read entry '" + name + "' of type " + entryType);
			}
			return value;
		}
		reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.FloatingPoint.ToString() + " or " + EntryType.Integer.ToString() + ", but got entry '" + name + "' of type " + entryType);
		reader.SkipEntry();
		return 0.0;
	}

	public override void WriteValue(string name, double value, IDataWriter writer)
	{
		writer.WriteDouble(name, value);
	}
}
