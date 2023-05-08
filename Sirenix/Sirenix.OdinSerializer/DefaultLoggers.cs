using UnityEngine;

namespace Sirenix.OdinSerializer;

public static class DefaultLoggers
{
	private static readonly object LOCK = new object();

	private static volatile ILogger unityLogger;

	public static ILogger DefaultLogger => UnityLogger;

	public static ILogger UnityLogger
	{
		get
		{
			if (unityLogger == null)
			{
				lock (LOCK)
				{
					if (unityLogger == null)
					{
						unityLogger = new CustomLogger(Debug.LogWarning, Debug.LogError, Debug.LogException);
					}
				}
			}
			return unityLogger;
		}
	}
}
