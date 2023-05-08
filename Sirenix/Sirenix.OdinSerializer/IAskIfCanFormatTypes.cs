using System;

namespace Sirenix.OdinSerializer;

public interface IAskIfCanFormatTypes
{
	bool CanFormatType(Type type);
}
