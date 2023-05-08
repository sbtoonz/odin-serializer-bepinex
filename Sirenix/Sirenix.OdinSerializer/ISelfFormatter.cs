namespace Sirenix.OdinSerializer;

public interface ISelfFormatter
{
	void Serialize(IDataWriter writer);

	void Deserialize(IDataReader reader);
}
