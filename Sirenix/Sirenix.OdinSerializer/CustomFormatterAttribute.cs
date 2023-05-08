using System;
using System.ComponentModel;

namespace Sirenix.OdinSerializer;

[AttributeUsage(AttributeTargets.Class)]
[Obsolete("Use a RegisterFormatterAttribute applied to the containing assembly instead.", true)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class CustomFormatterAttribute : Attribute
{
	public readonly int Priority;

	public CustomFormatterAttribute()
	{
		Priority = 0;
	}

	public CustomFormatterAttribute(int priority = 0)
	{
		Priority = priority;
	}
}
