using System;

namespace Sirenix.OdinSerializer;

public interface IExternalGuidReferenceResolver
{
	IExternalGuidReferenceResolver NextResolver { get; set; }

	bool TryResolveReference(Guid guid, out object value);

	bool CanReference(object value, out Guid guid);
}
