using System.Globalization;
using UnityEngine;

namespace Sirenix.OdinSerializer;

public sealed class Vector2DictionaryKeyPathProvider : BaseDictionaryKeyPathProvider<Vector2>
{
	public override string ProviderID => "v2";

	public override int Compare(Vector2 x, Vector2 y)
	{
		int num = x.x.CompareTo(y.x);
		if (num == 0)
		{
			num = x.y.CompareTo(y.y);
		}
		return num;
	}

	public override Vector2 GetKeyFromPathString(string pathStr)
	{
		int num = pathStr.IndexOf('|');
		string s = pathStr.Substring(1, num - 1).Trim();
		return new Vector2(y: float.Parse(pathStr.Substring(num + 1, pathStr.Length - (num + 2)).Trim()), x: float.Parse(s));
	}

	public override string GetPathStringFromKey(Vector2 key)
	{
		string text = key.x.ToString("R", CultureInfo.InvariantCulture);
		string text2 = key.y.ToString("R", CultureInfo.InvariantCulture);
		return ("(" + text + "|" + text2 + ")").Replace('.', ',');
	}
}
