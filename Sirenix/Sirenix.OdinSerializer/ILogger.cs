using System;

namespace Sirenix.OdinSerializer;

public interface ILogger
{
	void LogWarning(string warning);

	void LogError(string error);

	void LogException(Exception exception);
}
