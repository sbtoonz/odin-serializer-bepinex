namespace Sirenix.OdinSerializer.Utilities;

public interface ICacheNotificationReceiver
{
	void OnFreed();

	void OnClaimed();
}
