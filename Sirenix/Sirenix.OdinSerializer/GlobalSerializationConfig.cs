namespace Sirenix.OdinSerializer;

public class GlobalSerializationConfig
{
	private static readonly GlobalSerializationConfig instance = new GlobalSerializationConfig();

	public static GlobalSerializationConfig Instance => instance;

	public ILogger Logger => DefaultLoggers.UnityLogger;

	public DataFormat EditorSerializationFormat => DataFormat.Nodes;

	public DataFormat BuildSerializationFormat => DataFormat.Binary;

	public LoggingPolicy LoggingPolicy => LoggingPolicy.LogErrors;

	public ErrorHandlingPolicy ErrorHandlingPolicy => ErrorHandlingPolicy.Resilient;

	internal static bool HasInstanceLoaded => true;

	internal static void LoadInstanceIfAssetExists()
	{
	}
}
