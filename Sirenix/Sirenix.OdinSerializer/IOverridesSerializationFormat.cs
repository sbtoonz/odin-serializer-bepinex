namespace Sirenix.OdinSerializer;

public interface IOverridesSerializationFormat
{
	DataFormat GetFormatToSerializeAs(bool isPlayer);
}
