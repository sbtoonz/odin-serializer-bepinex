using System;

namespace Sirenix.OdinSerializer;

public sealed class EnumSerializer<T> : Serializer<T> where T : unmanaged, Enum
{
	private unsafe static readonly int SizeOf_T = sizeof(T);

	public unsafe override T ReadValue(IDataReader reader)
	{
		string name;
		EntryType entryType = reader.PeekEntry(out name);
		if (entryType == EntryType.Integer)
		{
			if (!reader.ReadUInt64(out var value))
			{
				reader.Context.Config.DebugContext.LogWarning("Failed to read entry '" + name + "' of type " + entryType);
			}
			return *(T*)(&value);
		}
		reader.Context.Config.DebugContext.LogWarning("Expected entry of type " + EntryType.Integer.ToString() + ", but got entry '" + name + "' of type " + entryType);
		reader.SkipEntry();
		return default(T);
	}

	public unsafe override void WriteValue(string name, T value, IDataWriter writer)
	{
		ulong value2 = default(ulong);
		byte* ptr = (byte*)(&value2);
		byte* ptr2 = (byte*)(&value);
		for (int i = 0; i < SizeOf_T; i++)
		{
			*(ptr++) = *(ptr2++);
		}
		writer.WriteUInt64(name, value2);
	}
}
