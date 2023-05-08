namespace Sirenix.OdinSerializer;

public interface IOverridesSerializationPolicy
{
	ISerializationPolicy SerializationPolicy { get; }

	bool OdinSerializesUnityFields { get; }
}
