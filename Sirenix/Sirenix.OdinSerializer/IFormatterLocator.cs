using System;

namespace Sirenix.OdinSerializer;

public interface IFormatterLocator
{
	bool TryGetFormatter(Type type, FormatterLocationStep step, ISerializationPolicy policy, bool allowWeakFallbackFormatters, out IFormatter formatter);
}
