namespace Sirenix.OdinSerializer;

public abstract class ReflectionOrEmittedBaseFormatter<T> : ReflectionFormatter<T>
{
	protected override void DeserializeImplementation(ref T value, IDataReader reader)
	{
		if (!(FormatterEmitter.GetEmittedFormatter(typeof(T), reader.Context.Config.SerializationPolicy) is FormatterEmitter.RuntimeEmittedFormatter<T> runtimeEmittedFormatter))
		{
			return;
		}
		int num = 0;
		EntryType entryType;
		string name;
		while ((entryType = reader.PeekEntry(out name)) != EntryType.EndOfNode && entryType != EntryType.EndOfArray && entryType != EntryType.EndOfStream)
		{
			runtimeEmittedFormatter.Read(ref value, name, entryType, reader);
			num++;
			if (num > 1000)
			{
				reader.Context.Config.DebugContext.LogError("Breaking out of infinite reading loop!");
				break;
			}
		}
	}

	protected override void SerializeImplementation(ref T value, IDataWriter writer)
	{
		if (FormatterEmitter.GetEmittedFormatter(typeof(T), writer.Context.Config.SerializationPolicy) is FormatterEmitter.RuntimeEmittedFormatter<T> runtimeEmittedFormatter)
		{
			runtimeEmittedFormatter.Write(ref value, writer);
		}
	}
}
