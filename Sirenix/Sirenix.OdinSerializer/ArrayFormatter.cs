namespace Sirenix.OdinSerializer;

public sealed class ArrayFormatter<T> : BaseFormatter<T[]>
{
	private static Serializer<T> valueReaderWriter = Serializer.Get<T>();

	protected override T[] GetUninitializedObject()
	{
		return null;
	}

	protected override void DeserializeImplementation(ref T[] value, IDataReader reader)
	{
		if (reader.PeekEntry(out var name) == EntryType.StartOfArray)
		{
			reader.EnterArray(out var length);
			value = new T[length];
			RegisterReferenceID(value, reader);
			for (int i = 0; i < length; i++)
			{
				if (reader.PeekEntry(out name) == EntryType.EndOfArray)
				{
					reader.Context.Config.DebugContext.LogError("Reached end of array after " + i + " elements, when " + length + " elements were expected.");
					break;
				}
				value[i] = valueReaderWriter.ReadValue(reader);
				if (reader.PeekEntry(out name) == EntryType.EndOfStream)
				{
					break;
				}
			}
			reader.ExitArray();
		}
		else
		{
			reader.SkipEntry();
		}
	}

	protected override void SerializeImplementation(ref T[] value, IDataWriter writer)
	{
		try
		{
			writer.BeginArrayNode(value.Length);
			for (int i = 0; i < value.Length; i++)
			{
				valueReaderWriter.WriteValue(value[i], writer);
			}
		}
		finally
		{
			writer.EndArrayNode();
		}
	}
}
