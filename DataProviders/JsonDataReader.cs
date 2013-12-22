﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace fyiReporting.Data
{
	public class JsonDataReader : IDataReader
	{

		JsonConnection _jconn;
		JsonCommand _jcmd;
		System.Data.CommandBehavior _behavior;
		static Type[] BASE_TYPES = new Type[] {System.String.Empty.GetType(),
									  System.Double.MinValue.GetType(),
									  System.Single.MinValue.GetType(),
									  System.Decimal.MinValue.GetType(),
									  System.DateTime.MinValue.GetType(),
									  System.Char.MinValue.GetType(),
									  new bool().GetType(),
									  System.Int32.MinValue.GetType(),
									  System.Int16.MinValue.GetType(),
									  System.Int64.MinValue.GetType(),
									  System.Byte.MinValue.GetType(),
									  System.UInt16.MinValue.GetType(),
									  System.UInt32.MinValue.GetType(),
									  System.UInt64.MinValue.GetType()};

		// column information
		ArrayList _RowData;			// array of Data rows; 
		IEnumerator _ie;			//  enumerator thru rows
		object[] _Data;				// data values of the columns
		ArrayList _Names;			// names of the columns
		ArrayList _Types;			// types of the columns



		public JsonDataReader(System.Data.CommandBehavior behavior, JsonConnection conn, JsonCommand cmd)
		{
			_jconn = conn;
			_jcmd = cmd;
			_behavior = behavior;

			string json_str="";
			if (conn.ConnectionString.StartsWith("http"))
			{
				var client = new System.Net.WebClient();
				client.Headers.Add("user-agent", "MajorSilence Reporting/4.5 (compatible; (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36)");
				json_str = client.DownloadString(conn.ConnectionString);
			}
			else
			{
				json_str = System.IO.File.ReadAllText(conn.ConnectionString);
			}

			IList json = Newtonsoft.Json.JsonConvert.DeserializeObject<IList>(json_str);

			// build the structure of the result
			BuildMetaData(json);

			if (_behavior == CommandBehavior.SchemaOnly)
				return;

			// build the array that will hold the data
			BuildData(json);
			return;
		}

		void BuildData(IList json)
		{
			_RowData = new ArrayList();


			int ci = 0;
			object[] row = null;
			GetDataProperties(null, json.GetType(), json, ref ci, ref row);
			_ie = _RowData.GetEnumerator();
		}

		void GetDataProperties(string name, Type t, object data, ref int ci, ref object[] row)
		{
			// Handle arrays
			if (t.IsArray)
			{
				Type at = t.GetElementType();
				if (data == null)		// even with null we need to go down the tree
					GetDataProperties(name, at, null, ref ci, ref row);
				else
				{
					int saveci = ci;
					foreach (object d in data as Array)
					{
						ci = saveci;			// seems funny, but we need to restore before each call
						GetDataProperties(name, at, d, ref ci, ref row);
						row = null;				// we'll want another row
					}
				}
				return;
			}

			// Base types go no further
			if (IsBaseType(t))
			{
				if (name == null)
					name = "result";

				if (row == null)
				{
					row = new object[_Names.Count];
					_RowData.Add(row);
				}
				row[ci++] = data;
				return;
			}

			// Handle complex type; get all its fields
			FieldInfo[] fis = t.GetFields();
			foreach (FieldInfo fi in fis)
			{
				string column_name = name == null ? fi.Name : name + "." + fi.Name;
				if (fi.FieldType.IsArray)
				{
					Array da = data == null ? null : fi.GetValue(data) as Array;
					if (da == null)	// still need to go down path even with null
						GetDataProperties(column_name, fi.FieldType.GetElementType(), null, ref ci, ref row);
					else
					{	// loop thru the object
						object[] save_row = row;
						int saveci = ci;
						foreach (object d in da)
						{
							ci = saveci;			// seems funny, but we need to restore before each call
							GetDataProperties(column_name, fi.FieldType.GetElementType(), d, ref ci, ref row);
							row = null;				// we'll want another row after this one
						}
						row = save_row;
					}
				}
				else
					GetDataProperties(column_name, fi.FieldType, data == null ? null : fi.GetValue(data), ref ci, ref row);
			}
		}

		void BuildMetaData(IList wsw)
		{
			_Names = new ArrayList();
			_Types = new ArrayList();

			GetProperties(null, wsw.GetType());
		}

		void GetProperties(string name, Type t)
		{
			// Handle arrays
			if (t.IsArray)
			{
				GetProperties(name, t.GetElementType());
				return;
			}

			// Base types go no further
			if (IsBaseType(t))
			{
				if (name == null)
				{
					name = "result";
				}

				_Names.Add(name);
				_Types.Add(t);
				return;
			}

			// Handle complex type; get all its fields
			FieldInfo[] fis = t.GetFields();
			foreach (FieldInfo fi in fis)
			{
				string column_name = name == null ? fi.Name : name + "." + fi.Name;
				if (fi.FieldType.IsArray)
				{
					GetProperties(column_name, fi.FieldType.GetElementType());
				}
				else
				{
					GetProperties(column_name, fi.FieldType);
				}
			}
		}

		// Determines if underlying type is a primitive
		bool IsBaseType(Type t)
		{
			foreach (Type bt in BASE_TYPES)
			{
				if (bt == t)
				{
					return true;
				}
			}

			return false;
		}


		public int RecordsAffected
		{
			get
			{
				return 0;
			}
		}

		public bool IsClosed
		{
			get
			{
				return _RowData != null;
			}
		}

		public bool NextResult()
		{
			return false;
		}

		public void Close()
		{
			_RowData = null;		// get rid of the data & metadata
			_ie = null;
			_Data = null;
			_Names = null;
			_Types = null;
		}

		public bool Read()
		{
			if (_ie == null || !_ie.MoveNext())
				return false;

			_Data = _ie.Current as object[];

			return true;
		}

		public int Depth
		{
			get
			{
				return 0;
			}
		}

		public DataTable GetSchemaTable()
		{
			// TODO:  Add XmlDataReader.GetSchemaTable implementation
			return null;
		}

		public void Dispose()
		{
			this.Close();
		}

		public int GetInt32(int i)
		{
			return Convert.ToInt32(_Data[i]);
		}

		public object this[string name]
		{
			get
			{
				int ci = this.GetOrdinal(name);
				return _Data[ci];
			}
		}

		object System.Data.IDataRecord.this[int i]
		{
			get
			{
				return _Data[i];
			}
		}

		public object GetValue(int i)
		{
			return _Data[i];
		}

		public bool IsDBNull(int i)
		{
			return _Data[i] == null;
		}

		public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
		{
			throw new NotImplementedException("GetBytes not implemented.");
		}

		public byte GetByte(int i)
		{
			return Convert.ToByte(_Data[i]);
		}

		public Type GetFieldType(int i)
		{
			return this._Types[i] as Type;
		}

		public decimal GetDecimal(int i)
		{
			return Convert.ToDecimal(_Data[i]);
		}

		public int GetValues(object[] values)
		{
			int i;
			for (i = 0; i < values.Length; i++)
			{
				values[i] = i >= _Data.Length ? System.DBNull.Value : _Data[i];
			}

			return Math.Min(values.Length, _Data.Length);
		}

		public string GetName(int i)
		{
			return _Names[i] as string;
		}

		public int FieldCount
		{
			get
			{
				return _Names == null ? 0 : _Names.Count;
			}
		}

		public long GetInt64(int i)
		{
			return Convert.ToInt64(_Data[i]);
		}

		public double GetDouble(int i)
		{
			return Convert.ToDouble(_Data[i]);
		}

		public bool GetBoolean(int i)
		{
			return Convert.ToBoolean(_Data[i]);
		}

		public Guid GetGuid(int i)
		{
			throw new NotImplementedException("GetGuid not implemented.");
		}

		public DateTime GetDateTime(int i)
		{
			return Convert.ToDateTime(_Data[i]);
		}

		public int GetOrdinal(string name)
		{
			int ci = 0;
			// do case sensitive lookup
			foreach (string cname in _Names)
			{
				if (cname == name)
					return ci;
				ci++;
			}

			// do case insensitive lookup
			ci = 0;
			name = name.ToLower();
			foreach (string cname in _Names)
			{
				if (cname.ToLower() == name)
					return ci;
				ci++;
			}

			throw new ArgumentException(string.Format("Column '{0}' not known.", name));
		}

		public string GetDataTypeName(int i)
		{
			Type t = _Types[i] as Type;
			return t.ToString();
		}

		public float GetFloat(int i)
		{
			return Convert.ToSingle(_Data[i]);
		}

		public IDataReader GetData(int i)
		{
			throw new NotImplementedException("GetData not implemented.");
		}

		public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
		{
			throw new NotImplementedException("GetChars not implemented.");
		}

		public string GetString(int i)
		{
			return Convert.ToString(_Data[i]);
		}

		public char GetChar(int i)
		{
			return Convert.ToChar(_Data[i]);
		}

		public short GetInt16(int i)
		{
			return Convert.ToInt16(_Data[i]);
		}

	}

}