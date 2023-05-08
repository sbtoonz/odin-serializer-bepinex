using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinSerializer.Utilities;

namespace Sirenix.OdinSerializer;

public class WeakReflectionFormatter : WeakBaseFormatter
{
	public WeakReflectionFormatter(Type serializedType)
		: base(serializedType)
	{
	}

	protected override void DeserializeImplementation(ref object value, IDataReader reader)
	{
		Dictionary<string, MemberInfo> serializableMembersMap = FormatterUtilities.GetSerializableMembersMap(SerializedType, reader.Context.Config.SerializationPolicy);
		EntryType entryType;
		string name;
		while ((entryType = reader.PeekEntry(out name)) != EntryType.EndOfNode && entryType != EntryType.EndOfArray && entryType != EntryType.EndOfStream)
		{
			MemberInfo value2;
			if (string.IsNullOrEmpty(name))
			{
				reader.Context.Config.DebugContext.LogError("Entry of type \"" + entryType.ToString() + "\" in node \"" + reader.CurrentNodeName + "\" is missing a name.");
				reader.SkipEntry();
			}
			else if (!serializableMembersMap.TryGetValue(name, out value2))
			{
				reader.Context.Config.DebugContext.LogWarning("Lost serialization data for entry \"" + name + "\" of type \"" + entryType.ToString() + "\" in node \"" + reader.CurrentNodeName + "\" because a serialized member of that name could not be found in type " + SerializedType.GetNiceFullName() + ".");
				reader.SkipEntry();
			}
			else
			{
				Type containedType = FormatterUtilities.GetContainedType(value2);
				try
				{
					object value3 = Serializer.Get(containedType).ReadValueWeak(reader);
					FormatterUtilities.SetMemberValue(value2, value, value3);
				}
				catch (Exception exception)
				{
					reader.Context.Config.DebugContext.LogException(exception);
				}
			}
		}
	}

	protected override void SerializeImplementation(ref object value, IDataWriter writer)
	{
		MemberInfo[] serializableMembers = FormatterUtilities.GetSerializableMembers(SerializedType, writer.Context.Config.SerializationPolicy);
		foreach (MemberInfo memberInfo in serializableMembers)
		{
			object memberValue = FormatterUtilities.GetMemberValue(memberInfo, value);
			Serializer serializer = Serializer.Get(FormatterUtilities.GetContainedType(memberInfo));
			try
			{
				serializer.WriteValueWeak(memberInfo.Name, memberValue, writer);
			}
			catch (Exception exception)
			{
				writer.Context.Config.DebugContext.LogException(exception);
			}
		}
	}
}
