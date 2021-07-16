using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TableCloth.Helpers
{
    // https://www.codeproject.com/Articles/20053/A-Complete-Win-INI-File-Utility-Class
    sealed class IniFileParser
	{
		// Win32 API supports maximum 32KiB size of file.
		public static readonly int MaxSectionSize = 32767;

		public IniFileParser(string iniFilePath)
		{
			IniFilePath = Path.GetFullPath(iniFilePath);
		}

		public string IniFilePath { get; init; }

		public string GetString(string sectionName, string keyName, string defaultValue)
		{
			if (sectionName == null)
				throw new ArgumentNullException(nameof(sectionName));

			if (keyName == null)
				throw new ArgumentNullException(nameof(keyName));

			var retval = new StringBuilder(IniFileParser.MaxSectionSize);
            _ = NativeMethods.GetPrivateProfileStringW(sectionName, keyName, defaultValue, retval, IniFileParser.MaxSectionSize, IniFilePath);
			return retval.ToString();
		}

		public int GetInt32(string sectionName, string keyName, int defaultValue)
		{
			if (sectionName == null)
				throw new ArgumentNullException(nameof(sectionName));

			if (keyName == null)
				throw new ArgumentNullException(nameof(keyName));

			return NativeMethods.GetPrivateProfileIntW(sectionName, keyName, defaultValue, IniFilePath);
		}

		public List<KeyValuePair<string, string>> GetSectionValues(string sectionName)
		{
			List<KeyValuePair<string, string>> retval;
			string[] keyValuePairs;
			string key, value;
			int equalSignPos;

			if (sectionName == null)
				throw new ArgumentNullException(nameof(sectionName));

			//Allocate a buffer for the returned section names.
			var ptr = Marshal.AllocCoTaskMem(IniFileParser.MaxSectionSize);

			try
			{
				//Get the section key/value pairs into the buffer.
				int len = NativeMethods.GetPrivateProfileSectionW(sectionName, ptr, IniFileParser.MaxSectionSize, IniFilePath);
				keyValuePairs = ConvertNullSeperatedStringToStringArray(ptr, len);
			}
			finally
			{
				//Free the buffer
				Marshal.FreeCoTaskMem(ptr);
			}

			//Parse keyValue pairs and add them to the list.
			retval = new List<KeyValuePair<string, string>>(keyValuePairs.Length);

			for (int i = 0; i < keyValuePairs.Length; ++i)
			{
				//Parse the "key=value" string into its constituent parts
				equalSignPos = keyValuePairs[i].IndexOf('=');
				key = keyValuePairs[i].Substring(0, equalSignPos);
				value = keyValuePairs[i].Substring(equalSignPos + 1, keyValuePairs[i].Length - equalSignPos - 1);
				retval.Add(new KeyValuePair<string, string>(key, value));
			}

			return retval;
		}

		public string[] GetKeyNames(string sectionName)
		{
			int len;
			string[] retval;

			if (sectionName == null)
				throw new ArgumentNullException(nameof(sectionName));

			//Allocate a buffer for the returned section names.
			var ptr = Marshal.AllocCoTaskMem(IniFileParser.MaxSectionSize);

			try
			{
				//Get the section names into the buffer.
				len = NativeMethods.GetPrivateProfileStringW(sectionName, null, null, ptr, IniFileParser.MaxSectionSize, IniFilePath);
				retval = ConvertNullSeperatedStringToStringArray(ptr, len);
			}
			finally
			{
				//Free the buffer
				Marshal.FreeCoTaskMem(ptr);
			}

			return retval;
		}

		public string[] GetSectionNames()
		{
			string[] retval;
			int len;

			//Allocate a buffer for the returned section names.
			var ptr = Marshal.AllocCoTaskMem(IniFileParser.MaxSectionSize);

			try
			{
				//Get the section names into the buffer.
				len = NativeMethods.GetPrivateProfileSectionNamesW(ptr, IniFileParser.MaxSectionSize, IniFilePath);
				retval = ConvertNullSeperatedStringToStringArray(ptr, len);
			}
			finally
			{
				//Free the buffer
				Marshal.FreeCoTaskMem(ptr);
			}

			return retval;
		}

		private static string[] ConvertNullSeperatedStringToStringArray(IntPtr ptr, int valLength)
		{
			string[] retval;

			if (valLength == 0)
			{
				//Return an empty array.
				retval = Array.Empty<string>();
			}
			else
			{
				//Convert the buffer into a string.  Decrease the length 
				//by 1 so that we remove the second null off the end.
				var buff = Marshal.PtrToStringAuto(ptr, valLength - 1);

				//Parse the buffer into an array of strings by searching for nulls.
				retval = buff.Split('\0');
			}

			return retval;
		}

		public override string ToString() => $"{IniFilePath}";
	}
}
