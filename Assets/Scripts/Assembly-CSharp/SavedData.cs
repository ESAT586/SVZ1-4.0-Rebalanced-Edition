using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using Debug = UnityEngine.Debug;

public class SavedData
{
	private SDFTreeNode mSavedData;

	private string mSaveFilePath;

	public string saveFilePath
	{
		get
		{
			return mSaveFilePath;
		}
		set
		{
			mSaveFilePath = value;
		}
	}

	public SDFTreeNode saveData
	{
		get
		{
			return mSavedData;
		}
	}

	public SavedData(string savePath)
	{
		saveFilePath = savePath;
	}

	public void Save()
	{
		try
		{
			SDFTree.Save(mSavedData, saveFilePath);
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[SavedData] Save failed: " + ex.Message);
		}
	}

	public bool Load()
	{
		mSavedData = SDFTree.LoadFromFile(saveFilePath);
		if (mSavedData == null)
		{
			mSavedData = new SDFTreeNode();
			return false;
		}
		return true;
	}

	public void SetSDFTreeNode(SDFTreeNode data)
	{
		mSavedData = data;
	}

	public void Clear()
	{
		mSavedData = new SDFTreeNode();
	}

	public void SetValue(string attrib, string val)
	{
		mSavedData[attrib] = val;
	}

	public void SetValue(string attrib, string val, string subSection)
	{
		GetSubSection(subSection)[attrib] = val;
	}

	public void SetValueInt(string attrib, int val)
	{
		SetValue(attrib, val.ToString());
	}

	public void SetValueInt(string attrib, int val, string subSection)
	{
		SetValue(attrib, val.ToString(), subSection);
	}

	public void SetValueFloat(string attrib, float val)
	{
		SetValue(attrib, val.ToString());
	}

	public void SetValueFloat(string attrib, float val, string subSection)
	{
		SetValue(attrib, val.ToString(), subSection);
	}

	public void SetValueBool(string attrib, bool val)
	{
		SetValue(attrib, val.ToString());
	}

	public void SetValueBool(string attrib, bool val, string subSection)
	{
		SetValue(attrib, val.ToString(), subSection);
	}

	public string GetValue(string attrib)
	{
		return GetValue(attrib, null);
	}

	public string GetValue(string attrib, string subSection)
	{
		if (!GetSubSection(subSection).hasAttribute(attrib))
		{
			return string.Empty;
		}
		return GetSubSection(subSection)[attrib];
	}

	public int GetValueInt(string attrib)
	{
		return GetValueInt(attrib, null);
	}

	public int GetValueInt(string attrib, string subSection)
	{
		string value = GetValue(attrib, subSection);
		if (value.Length == 0)
		{
			return 0;
		}
		int result;
		if (int.TryParse(value, out result))
		{
			return result;
		}
		return 0;
	}

	public float GetValueFloat(string attrib)
	{
		return GetValueFloat(attrib, null);
	}

	public float GetValueFloat(string attrib, string subSection)
	{
		string value = GetValue(attrib, subSection);
		if (value.Length == 0)
		{
			return 0f;
		}
		float result;
		if (float.TryParse(value, out result))
		{
			return result;
		}
		return 0f;
	}

	public bool GetValueBool(string attrib)
	{
		return GetValueBool(attrib, null);
	}

	public bool GetValueBool(string attrib, string subSection)
	{
		string value = GetValue(attrib, subSection);
		if (value.Length == 0)
		{
			return false;
		}
		bool result;
		if (bool.TryParse(value, out result))
		{
			return result;
		}
		return false;
	}

	public void SetDictionaryValue<T>(string subNode, string attrib, T val)
	{
		SetDictionaryValue(subNode, attrib, val, null);
	}

	public void SetDictionaryValue<T>(string subNode, string attrib, T val, string subSection)
	{
		SDFTreeNode sDFTreeNode = GetSubSection(subSection).to(subNode);
		if (sDFTreeNode == null)
		{
			sDFTreeNode = new SDFTreeNode();
			GetSubSection(subSection).SetChild(subNode, sDFTreeNode);
		}
		string value = ConvertToString(val);
		sDFTreeNode[attrib] = value;
	}

	public T GetDictionaryValue<T>(string subNode, string attrib)
	{
		return GetDictionaryValue<T>(subNode, attrib, null);
	}

	public T GetDictionaryValue<T>(string subNode, string attrib, string subSection)
	{
		SDFTreeNode sDFTreeNode = GetSubSection(subSection).to(subNode);
		if (sDFTreeNode == null)
		{
			return default(T);
		}
		if (!sDFTreeNode.hasAttribute(attrib))
		{
			return default(T);
		}
		return ConvertFromString<T>(sDFTreeNode[attrib]);
	}

	public List<string> GetSubNodeValueList(string subNode)
	{
		return GetSubNodeValueList(subNode, null);
	}

	public List<string> GetSubNodeValueList(string subNode, string subSection)
	{
		List<string> list = new List<string>();
		if (GetSubSection(subSection).hasChild(subNode))
		{
			SDFTreeNode sDFTreeNode = GetSubSection(subSection).to(subNode);
			for (int i = 0; sDFTreeNode.hasAttribute(i); i++)
			{
				list.Add(sDFTreeNode[i]);
			}
		}
		return list;
	}

	public void SetSubNodeValueList(string subNode, List<string> vals)
	{
		SetSubNodeValueList(subNode, vals, null);
	}

	public void SetSubNodeValueList(string subNode, List<string> vals, string subSection)
	{
		SDFTreeNode sDFTreeNode = null;
		if (GetSubSection(subSection).hasChild(subNode))
		{
			sDFTreeNode = GetSubSection(subSection).to(subNode);
		}
		else
		{
			sDFTreeNode = new SDFTreeNode();
			GetSubSection(subSection).SetChild(subNode, sDFTreeNode);
		}
		sDFTreeNode.ClearAttributes();
		int num = 0;
		foreach (string val in vals)
		{
			sDFTreeNode[num] = val;
			num++;
		}
	}

	public void SetSimpleList(string subNode, List<string> values)
	{
		SetSimpleList(subNode, values, null);
	}

	public void SetSimpleList(string subNode, List<string> values, string subSection)
	{
		SDFTreeNode sDFTreeNode = null;
		if (GetSubSection(subSection).hasChild(subNode))
		{
			sDFTreeNode = GetSubSection(subSection).to(subNode);
		}
		else
		{
			sDFTreeNode = new SDFTreeNode();
			GetSubSection(subSection).SetChild(subNode, sDFTreeNode);
		}
		sDFTreeNode.ClearAttributes();
		if (values == null)
		{
			return;
		}
		int num = 0;
		foreach (string value in values)
		{
			sDFTreeNode[num] = value;
			num++;
		}
	}

	public List<string> GetSimpleList(string subNode)
	{
		return GetSimpleList(subNode, null);
	}

	public List<string> GetSimpleList(string subNode, string subSection)
	{
		List<string> list = new List<string>();
		SDFTreeNode sDFTreeNode = GetSubSection(subSection).to(subNode);
		if (sDFTreeNode != null)
		{
			for (int i = 0; sDFTreeNode.hasAttribute(i); i++)
			{
				list.Add(sDFTreeNode[i]);
			}
		}
		return list;
	}

	public T ConvertFromString<T>(string val)
	{
		System.Type targetType = typeof(T);
		if (targetType == typeof(string))
		{
			return (T)(object)(val ?? string.Empty);
		}
		string trimmed = (val == null) ? string.Empty : val.Trim();
		if (targetType == typeof(int))
		{
			int intVal;
			if (trimmed.Length > 0 && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out intVal))
			{
				return (T)(object)intVal;
			}
			return default(T);
		}
		if (targetType == typeof(bool))
		{
			bool boolVal;
			if (trimmed.Length > 0 && bool.TryParse(trimmed, out boolVal))
			{
				return (T)(object)boolVal;
			}
			if (trimmed == "1")
			{
				return (T)(object)true;
			}
			return default(T);
		}
		if (targetType == typeof(float))
		{
			float floatVal;
			if (trimmed.Length > 0 && float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out floatVal))
			{
				return (T)(object)floatVal;
			}
			return default(T);
		}
		if (targetType == typeof(double))
		{
			double doubleVal;
			if (trimmed.Length > 0 && double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleVal))
			{
				return (T)(object)doubleVal;
			}
			return default(T);
		}
		if (targetType == typeof(long))
		{
			long longVal;
			if (trimmed.Length > 0 && long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out longVal))
			{
				return (T)(object)longVal;
			}
			return default(T);
		}
		if (targetType.IsEnum)
		{
			if (trimmed.Length > 0)
			{
				try
				{
					return (T)System.Enum.Parse(targetType, trimmed, true);
				}
				catch (System.Exception)
				{
				}
			}
			return default(T);
		}
		try
		{
			TypeConverter converter = TypeDescriptor.GetConverter(targetType);
			if (converter != null)
			{
				return (T)converter.ConvertFromString(val);
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[SavedData] TypeDescriptor ConvertFromString fallback failed for " + targetType.FullName + ": " + ex.Message);
		}
		return default(T);
	}

	public string ConvertToString<T>(T val)
	{
		if (val == null)
		{
			return string.Empty;
		}
		System.Type targetType = typeof(T);
		if (targetType == typeof(string))
		{
			return (string)(object)val;
		}
		if (targetType == typeof(bool))
		{
			return ((bool)(object)val).ToString();
		}
		if (targetType == typeof(int))
		{
			return ((int)(object)val).ToString(CultureInfo.InvariantCulture);
		}
		if (targetType == typeof(float))
		{
			return ((float)(object)val).ToString(CultureInfo.InvariantCulture);
		}
		if (targetType == typeof(double))
		{
			return ((double)(object)val).ToString(CultureInfo.InvariantCulture);
		}
		if (targetType == typeof(long))
		{
			return ((long)(object)val).ToString(CultureInfo.InvariantCulture);
		}
		if (targetType.IsEnum)
		{
			return val.ToString();
		}
		try
		{
			TypeConverter converter = TypeDescriptor.GetConverter(targetType);
			if (converter != null)
			{
				return converter.ConvertToString(val);
			}
		}
		catch (System.Exception)
		{
		}
		return val.ToString();
	}

	public byte[] GetAsByteArray()
	{
		Encoding encoding = new ASCIIEncoding();
		string text = string.Empty;
		string error = string.Empty;
		if (FileEncryptAndDecrypt.TryDecryptFile(mSaveFilePath, out text, out error))
		{
			return encoding.GetBytes(text);
		}
		Debug.LogWarning("[SavedData] Export decrypt failed: " + error);
		return new byte[0];
	}

	public void SetWithByteArray(byte[] data)
	{
		TrySetWithByteArray(data);
	}

	public bool TrySetWithByteArray(byte[] data)
	{
		Encoding aSCII = Encoding.ASCII;
		char[] array = new char[aSCII.GetCharCount(data, 0, data.Length)];
		aSCII.GetChars(data, 0, data.Length, array, 0);
		string text = new string(array);
		try
		{
			SDFTreeNode sDFTreeNode = SDFTree.LoadFromSingleString(text);
			if (sDFTreeNode == null || (sDFTreeNode.attributeCount == 0 && sDFTreeNode.childCount == 0))
			{
				Debug.LogWarning("[SavedData] Import failed: parsed save is empty");
				return false;
			}
			SetSDFTreeNode(sDFTreeNode);
			return true;
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[SavedData] Import failed: " + ex.Message);
			return false;
		}
	}

	private SDFTreeNode GetSubSection(string subSection)
	{
		if (subSection == null || subSection == string.Empty)
		{
			return mSavedData;
		}
		if (!mSavedData.hasChild(subSection))
		{
			mSavedData.SetChild(subSection, new SDFTreeNode());
		}
		return mSavedData.to(subSection);
	}
}
