using System;

namespace Sirenix.OdinSerializer.Utilities;

public interface ICache : IDisposable
{
	object Value { get; }
}
