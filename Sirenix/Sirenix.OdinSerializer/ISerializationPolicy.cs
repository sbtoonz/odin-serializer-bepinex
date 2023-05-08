using System.Reflection;

namespace Sirenix.OdinSerializer;

public interface ISerializationPolicy
{
	string ID { get; }

	bool AllowNonSerializableTypes { get; }

	bool ShouldSerializeMember(MemberInfo member);
}
