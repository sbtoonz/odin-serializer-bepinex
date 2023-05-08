using System;

namespace Sirenix.OdinSerializer;

public class WeakDelegateFormatter : DelegateFormatter<Delegate>
{
	public WeakDelegateFormatter(Type delegateType)
		: base(delegateType)
	{
	}
}
