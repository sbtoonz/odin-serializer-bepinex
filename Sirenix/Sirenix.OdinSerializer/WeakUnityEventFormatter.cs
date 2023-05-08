using System;

namespace Sirenix.OdinSerializer;

public class WeakUnityEventFormatter : WeakReflectionFormatter
{
	public WeakUnityEventFormatter(Type serializedType)
		: base(serializedType)
	{
	}

	protected override object GetUninitializedObject()
	{
		return Activator.CreateInstance(SerializedType);
	}
}
