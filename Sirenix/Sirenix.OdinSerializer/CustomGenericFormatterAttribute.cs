using System;
using System.ComponentModel;

namespace Sirenix.OdinSerializer;

[AttributeUsage(AttributeTargets.Class)]
[Obsolete("Use a RegisterFormatterAttribute applied to the containing assembly instead.", true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CustomGenericFormatterAttribute : CustomFormatterAttribute
{
	public readonly Type SerializedGenericTypeDefinition;

	public CustomGenericFormatterAttribute(Type serializedGenericTypeDefinition, int priority = 0)
		: base(priority)
	{
		if (serializedGenericTypeDefinition == null)
		{
			throw new ArgumentNullException();
		}
		if (!serializedGenericTypeDefinition.IsGenericTypeDefinition)
		{
			throw new ArgumentException("The type " + serializedGenericTypeDefinition.Name + " is not a generic type definition.");
		}
		SerializedGenericTypeDefinition = serializedGenericTypeDefinition;
	}
}
