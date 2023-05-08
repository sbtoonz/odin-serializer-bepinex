using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Sirenix.OdinSerializer;

public sealed class WeakSerializableFormatter : WeakBaseFormatter
{
	private readonly Func<SerializationInfo, StreamingContext, ISerializable> ISerializableConstructor;

	private readonly WeakReflectionFormatter ReflectionFormatter;

	public WeakSerializableFormatter(Type serializedType)
		: base(serializedType)
	{
		WeakSerializableFormatter weakSerializableFormatter = this;
		Type type = serializedType;
		ConstructorInfo constructor = null;
		do
		{
			constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2]
			{
				typeof(SerializationInfo),
				typeof(StreamingContext)
			}, null);
			type = type.BaseType;
		}
		while (constructor == null && type != typeof(object) && type != null);
		if (constructor != null)
		{
			ISerializableConstructor = delegate(SerializationInfo info, StreamingContext context)
			{
				ISerializable serializable = (ISerializable)FormatterServices.GetUninitializedObject(weakSerializableFormatter.SerializedType);
				constructor.Invoke(serializable, new object[2] { info, context });
				return serializable;
			};
		}
		else
		{
			DefaultLoggers.DefaultLogger.LogWarning("Type " + SerializedType.Name + " implements the interface ISerializable but does not implement the required constructor with signature " + SerializedType.Name + "(SerializationInfo info, StreamingContext context). The interface declaration will be ignored, and the formatter fallbacks to reflection.");
			ReflectionFormatter = new WeakReflectionFormatter(SerializedType);
		}
	}

	protected override object GetUninitializedObject()
	{
		return null;
	}

	protected override void DeserializeImplementation(ref object value, IDataReader reader)
	{
		if (ISerializableConstructor != null)
		{
			SerializationInfo serializationInfo = ReadSerializationInfo(reader);
			if (serializationInfo == null)
			{
				return;
			}
			try
			{
				value = ISerializableConstructor(serializationInfo, reader.Context.StreamingContext);
				InvokeOnDeserializingCallbacks(value, reader.Context);
				if (!IsValueType)
				{
					RegisterReferenceID(value, reader);
				}
				return;
			}
			catch (Exception exception)
			{
				reader.Context.Config.DebugContext.LogException(exception);
				return;
			}
		}
		value = ReflectionFormatter.Deserialize(reader);
		InvokeOnDeserializingCallbacks(value, reader.Context);
		if (!IsValueType)
		{
			RegisterReferenceID(value, reader);
		}
	}

	protected override void SerializeImplementation(ref object value, IDataWriter writer)
	{
		if (ISerializableConstructor != null)
		{
			ISerializable serializable = value as ISerializable;
			SerializationInfo info = new SerializationInfo(value.GetType(), writer.Context.FormatterConverter);
			try
			{
				serializable.GetObjectData(info, writer.Context.StreamingContext);
			}
			catch (Exception exception)
			{
				writer.Context.Config.DebugContext.LogException(exception);
			}
			WriteSerializationInfo(info, writer);
		}
		else
		{
			ReflectionFormatter.Serialize(value, writer);
		}
	}

	private SerializationInfo ReadSerializationInfo(IDataReader reader)
	{
		if (reader.PeekEntry(out var name) == EntryType.StartOfArray)
		{
			try
			{
				reader.EnterArray(out var length);
				SerializationInfo serializationInfo = new SerializationInfo(SerializedType, reader.Context.FormatterConverter);
				for (int i = 0; i < length; i++)
				{
					Type type = null;
					if (reader.PeekEntry(out name) == EntryType.String && name == "type")
					{
						reader.ReadString(out var value);
						type = reader.Context.Binder.BindToType(value, reader.Context.Config.DebugContext);
					}
					if (type == null)
					{
						reader.SkipEntry();
					}
					else
					{
						reader.PeekEntry(out name);
						object value2 = Serializer.Get(type).ReadValueWeak(reader);
						serializationInfo.AddValue(name, value2);
					}
				}
				return serializationInfo;
			}
			finally
			{
				reader.ExitArray();
			}
		}
		return null;
	}

	private void WriteSerializationInfo(SerializationInfo info, IDataWriter writer)
	{
		try
		{
			writer.BeginArrayNode(info.MemberCount);
			SerializationInfoEnumerator enumerator = info.GetEnumerator();
			while (enumerator.MoveNext())
			{
				SerializationEntry current = enumerator.Current;
				try
				{
					writer.WriteString("type", writer.Context.Binder.BindToName(current.ObjectType, writer.Context.Config.DebugContext));
					Serializer.Get(current.ObjectType).WriteValueWeak(current.Name, current.Value, writer);
				}
				catch (Exception exception)
				{
					writer.Context.Config.DebugContext.LogException(exception);
				}
			}
		}
		finally
		{
			writer.EndArrayNode();
		}
	}
}
