// LICENSE INFORMATION ////////////////////////////////////////////////////////////////////////
//                                                                                           //
//                                          MPack                                            //
//                                                                                           //
//  MPack is an implementation of the MessagePack binary serialization format.               //
//  It is a direct mapping to JSON, and the official specification can be found here:        //
//  https://github.com/msgpack/msgpack/blob/master/spec.md                                   //
//                                                                                           //
//  This MPack implementation is inspired by the work of ymofen (ymofen@diocp.org):          //
//  https://github.com/ymofen/SimpleMsgPack.Net                                              //
//                                                                                           //
//  this implementation has been completely reworked from the ground up including many       //
//  bux fixes and an API that remains lightweight compared to the official one,              //
//  while remaining robust and easy to use.                                                  //
//                                                                                           //                 
//  Written by Caelan Sayler [caelantsayler]at[gmail]dot[com]                                //
//  Original URL: https://github.com/caesay/MPack                                            //
//  Licensed: Attribution 4.0 (CC BY 4.0) http://creativecommons.org/licenses/by/4.0/        //
//                                                                                           //
///////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsgPack
{
    public enum MsgPackType
    {
        Unknown = 0,
        Null = 1,
        Map = 2,
        Array = 3,
        String = 4,
        Integer = 5,
        UInt64 = 6,
        Boolean = 7,
        Double = 8,
        Single = 9,
        DateTime = 10,
        Binary = 11
    }
    public class MPack : IEquatable<MPack>
    {
        public virtual object Value => _value;
        public virtual MsgPackType ValueType => _type;

        protected MPack(object value, MsgPackType type)
        {
            _value = value;
            _type = type;
        }
        protected MPack()
        {
        }

        private object _value;
        private MsgPackType _type = MsgPackType.Unknown;

        public static MPack ParseBytes(byte[] array)
        {
            using (MemoryStream ms = new MemoryStream(array))
                return ParseStream(ms);
        }
        public static MPack ParseStream(Stream stream)
        {
            byte lvByte = (byte)stream.ReadByte();
            if (lvByte <= 0x7F)
            {
                //positive fixint	0xxxxxxx	0x00 - 0x7f
                return FromInteger(lvByte);
            }
            if ((lvByte >= 0x80) && (lvByte <= 0x8F))
            {
                //fixmap	1000xxxx	0x80 - 0x8f
                MPackMap map = new MPackMap();
                int len = lvByte - 0x80;
                for (int i = 0; i < len; i++)
                {
                    map.Add(ReadTools.ReadString(stream), ParseStream(stream));
                }
                return map;
            }
            if ((lvByte >= 0x90) && (lvByte <= 0x9F))
            {
                //fixarray	1001xxxx	0x90 - 0x9f
                MPackArray array = new MPackArray();
                int len = lvByte - 0x90;
                for (int i = 0; i < len; i++)
                {
                    array.Add(ParseStream(stream));
                }
                return array;
            }
            if ((lvByte >= 0xA0) && (lvByte <= 0xBF))
            {
                // fixstr	101xxxxx	0xa0 - 0xbf
                int len = lvByte - 0xA0;
                return FromString(ReadTools.ReadString(stream, len));
            }
            if ((lvByte >= 0xE0) && (lvByte <= 0xFF))
            {
                // -1..-32
                //  negative fixnum stores 5-bit negative integer
                //  +--------+
                //  |111YYYYY|
                //  +--------+                
                return FromInteger((sbyte)lvByte);
            }
            if (lvByte == 0xC0)
            {
                return Null();
            }
            if (lvByte == 0xC1)
            {
                throw new ArgumentException("(never used) type $c1");
            }
            if (lvByte == 0xC2)
            {
                return FromBool(false);
            }
            if (lvByte == 0xC3)
            {
                return FromBool(true);
            }
            if (lvByte == 0xC4)
            {
                // max 255
                int len = stream.ReadByte();
                byte[] raw = new byte[len];
                stream.Read(raw, 0, len);
                return FromBytes(raw);
            }
            if (lvByte == 0xC5)
            {
                // max 65535                
                byte[] raw = new byte[2];
                stream.Read(raw, 0, 2);
                raw = BytesTools.SwapBytes(raw);
                int len = BitConverter.ToInt16(raw, 0);

                // read binary
                raw = new byte[len];
                stream.Read(raw, 0, len);
                return FromBytes(raw);
            }
            if (lvByte == 0xC6)
            {
                // binary max: 2^32-1                
                byte[] raw = new byte[4];
                stream.Read(raw, 0, 4);
                raw = BytesTools.SwapBytes(raw);
                int len = BitConverter.ToInt32(raw, 0);

                // read binary
                raw = new byte[len];
                stream.Read(raw, 0, len);
                return FromBytes(raw);
            }
            if ((lvByte == 0xC7) || (lvByte == 0xC8) || (lvByte == 0xC9))
            {
                throw new Exception("(ext8,ext16,ex32) type $c7,$c8,$c9");
            }
            if (lvByte == 0xCA)
            {
                // float 32 stores a floating point number in IEEE 754 single precision floating point number     
                var raw = new byte[4];
                stream.Read(raw, 0, 4);
                raw = BytesTools.SwapBytes(raw);
                return FromSingle(BitConverter.ToSingle(raw, 0));
            }
            if (lvByte == 0xCB)
            {
                // float 64 stores a floating point number in IEEE 754 double precision floating point number        
                var rawByte = new byte[8];
                stream.Read(rawByte, 0, 8);
                rawByte = BytesTools.SwapBytes(rawByte);
                return FromDouble(BitConverter.ToDouble(rawByte, 0));
            }
            if (lvByte == 0xCC)
            {
                // uint8   
                //      uint 8 stores a 8-bit unsigned integer
                //      +--------+--------+
                //      |  0xcc  |ZZZZZZZZ|
                //      +--------+--------+
                lvByte = (byte)stream.ReadByte();
                return FromInteger(lvByte);
            }
            if (lvByte == 0xCD)
            {
                // uint16      
                //    uint 16 stores a 16-bit big-endian unsigned integer
                //    +--------+--------+--------+
                //    |  0xcd  |ZZZZZZZZ|ZZZZZZZZ|
                //    +--------+--------+--------+
                var rawByte = new byte[2];
                stream.Read(rawByte, 0, 2);
                rawByte = BytesTools.SwapBytes(rawByte);
                return FromInteger(BitConverter.ToUInt16(rawByte, 0));
            }
            if (lvByte == 0xCE)
            {
                //  uint 32 stores a 32-bit big-endian unsigned integer
                //  +--------+--------+--------+--------+--------+
                //  |  0xce  |ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ
                //  +--------+--------+--------+--------+--------+
                var rawByte = new byte[4];
                stream.Read(rawByte, 0, 4);
                rawByte = BytesTools.SwapBytes(rawByte);
                return FromInteger(BitConverter.ToUInt32(rawByte, 0));
            }
            if (lvByte == 0xCF)
            {
                //  uint 64 stores a 64-bit big-endian unsigned integer
                //  +--------+--------+--------+--------+--------+--------+--------+--------+--------+
                //  |  0xcf  |ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|
                //  +--------+--------+--------+--------+--------+--------+--------+--------+--------+
                var rawByte = new byte[8];
                stream.Read(rawByte, 0, 8);
                rawByte = BytesTools.SwapBytes(rawByte);
                return FromInteger(BitConverter.ToUInt64(rawByte, 0));
            }
            if (lvByte == 0xDC)
            {
                //      +--------+--------+--------+~~~~~~~~~~~~~~~~~+
                //      |  0xdc  |YYYYYYYY|YYYYYYYY|    N objects    |
                //      +--------+--------+--------+~~~~~~~~~~~~~~~~~+
                var rawByte = new byte[2];
                stream.Read(rawByte, 0, 2);
                rawByte = BytesTools.SwapBytes(rawByte);
                var len = BitConverter.ToInt16(rawByte, 0);

                MPackArray array = new MPackArray();
                for (int i = 0; i < len; i++)
                {
                    array.Add(ParseStream(stream));
                }
                return array;
            }
            if (lvByte == 0xDD)
            {
                //  +--------+--------+--------+--------+--------+~~~~~~~~~~~~~~~~~+
                //  |  0xdd  |ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|    N objects    |
                //  +--------+--------+--------+--------+--------+~~~~~~~~~~~~~~~~~+
                var rawByte = new byte[4];
                stream.Read(rawByte, 0, 4);
                rawByte = BytesTools.SwapBytes(rawByte);
                var len = BitConverter.ToInt32(rawByte, 0);

                MPackArray array = new MPackArray();
                for (int i = 0; i < len; i++)
                {
                    array.Add(ParseStream(stream));
                }
                return array;
            }
            if (lvByte == 0xD9)
            {
                //  str 8 stores a byte array whose length is upto (2^8)-1 bytes:
                //  +--------+--------+========+
                //  |  0xd9  |YYYYYYYY|  data  |
                //  +--------+--------+========+
                return FromString(ReadTools.ReadString(lvByte, stream));
            }
            if (lvByte == 0xDE)
            {
                //    +--------+--------+--------+~~~~~~~~~~~~~~~~~+
                //    |  0xde  |YYYYYYYY|YYYYYYYY|   N*2 objects   |
                //    +--------+--------+--------+~~~~~~~~~~~~~~~~~+
                var rawByte = new byte[2];
                stream.Read(rawByte, 0, 2);
                rawByte = BytesTools.SwapBytes(rawByte);
                var len = BitConverter.ToInt16(rawByte, 0);

                MPackMap map = new MPackMap();
                for (int i = 0; i < len; i++)
                {
                    var key = ReadTools.ReadString(stream);
                    var val = ParseStream(stream);
                    map.Add(key, val);
                }
                return map;
            }
            if (lvByte == 0xDF)
            {
                //    +--------+--------+--------+--------+--------+~~~~~~~~~~~~~~~~~+
                //    |  0xdf  |ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|   N*2 objects   |
                //    +--------+--------+--------+--------+--------+~~~~~~~~~~~~~~~~~+
                var rawByte = new byte[4];
                stream.Read(rawByte, 0, 4);
                rawByte = BytesTools.SwapBytes(rawByte);
                var len = BitConverter.ToInt32(rawByte, 0);

                MPackMap map = new MPackMap();
                for (int i = 0; i < len; i++)
                {
                    var key = ReadTools.ReadString(stream);
                    var val = ParseStream(stream);
                    map.Add(key, val);
                }
                return map;
            }
            if (lvByte == 0xDA)
            {
                //      str 16 stores a byte array whose length is upto (2^16)-1 bytes:
                //      +--------+--------+--------+========+
                //      |  0xda  |ZZZZZZZZ|ZZZZZZZZ|  data  |
                //      +--------+--------+--------+========+
                return FromString(ReadTools.ReadString(lvByte, stream));
            }
            if (lvByte == 0xDB)
            {
                //  str 32 stores a byte array whose length is upto (2^32)-1 bytes:
                //  +--------+--------+--------+--------+--------+========+
                //  |  0xdb  |AAAAAAAA|AAAAAAAA|AAAAAAAA|AAAAAAAA|  data  |
                //  +--------+--------+--------+--------+--------+========+
                return FromString(ReadTools.ReadString(lvByte, stream));
            }
            if (lvByte == 0xD0)
            {
                //      int 8 stores a 8-bit signed integer
                //      +--------+--------+
                //      |  0xd0  |ZZZZZZZZ|
                //      +--------+--------+
                return FromInteger((sbyte)stream.ReadByte());
            }
            if (lvByte == 0xD1)
            {
                //    int 16 stores a 16-bit big-endian signed integer
                //    +--------+--------+--------+
                //    |  0xd1  |ZZZZZZZZ|ZZZZZZZZ|
                //    +--------+--------+--------+
                var rawByte = new byte[2];
                stream.Read(rawByte, 0, 2);
                rawByte = BytesTools.SwapBytes(rawByte);
                return FromInteger(BitConverter.ToInt16(rawByte, 0));
            }
            if (lvByte == 0xD2)
            {
                //  int 32 stores a 32-bit big-endian signed integer
                //  +--------+--------+--------+--------+--------+
                //  |  0xd2  |ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|
                //  +--------+--------+--------+--------+--------+
                var rawByte = new byte[4];
                stream.Read(rawByte, 0, 4);
                rawByte = BytesTools.SwapBytes(rawByte);
                return FromInteger(BitConverter.ToInt32(rawByte, 0));
            }
            if (lvByte == 0xD3)
            {
                //  int 64 stores a 64-bit big-endian signed integer
                //  +--------+--------+--------+--------+--------+--------+--------+--------+--------+
                //  |  0xd3  |ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|ZZZZZZZZ|
                //  +--------+--------+--------+--------+--------+--------+--------+--------+--------+
                var rawByte = new byte[8];
                stream.Read(rawByte, 0, 8);
                rawByte = BytesTools.SwapBytes(rawByte);
                return FromInteger(BitConverter.ToInt64(rawByte, 0));
            }

            throw new ArgumentException("This stream does not contain MsgPack data.");
        }

        public static MPack Null()
        {
            return new MPack() { _type = MsgPackType.Null };
        }
        public static MPack FromDateTime(DateTime value)
        {
            return new MPack() { _type = MsgPackType.DateTime, _value = value };
        }
        public static MPack FromBytes(byte[] value)
        {
            return new MPack() { _type = MsgPackType.Binary, _value = value };
        }
        public static MPack FromBool(bool value)
        {
            return new MPack() { _type = MsgPackType.Boolean, _value = value };
        }
        public static MPack FromDouble(double value)
        {
            return new MPack() { _type = MsgPackType.Double, _value = value };
        }
        public static MPack FromSingle(float value)
        {
            return new MPack() { _type = MsgPackType.Single, _value = value };
        }
        public static MPack FromString(string value)
        {
            return new MPack() { _type = MsgPackType.String, _value = value };
        }
        public static MPack FromInteger(int number)
        {
            return new MPack() { _type = MsgPackType.Integer, _value = number };
        }
        public static MPack FromInteger(long number)
        {
            return new MPack() { _type = MsgPackType.Integer, _value = number };
        }
        public static MPack FromInteger(ulong number)
        {
            return new MPack() { _type = MsgPackType.UInt64, _value = number };
        }
        public static MPackArray FromArray(IEnumerable<MPack> value)
        {
            return new MPackArray(value);
        }
        public static MPackMap FromDictionary(IDictionary<string, MPack> value)
        {
            return new MPackMap(value);
        }
        public static MPackMap FromDictionary(IEnumerable<KeyValuePair<string, MPack>> value)
        {
            return new MPackMap(value);
        }

        public T To<T>()
        {
            if (typeof(T) == typeof(object))
                return (T)Value;
            if (typeof(T) == typeof(byte[]) && ValueType == MsgPackType.Binary)
                return (T)Value;

            TypeCode code = Type.GetTypeCode(typeof(T));
            //boolean
            if (code == TypeCode.Boolean && ValueType == MsgPackType.Boolean)
                return (T)(object)Convert.ToBoolean(Value);

            //integers
            if (code == TypeCode.Int16 && ValueType == MsgPackType.Integer)
                return (T)(object)Convert.ToInt16(Value);
            if (code == TypeCode.UInt16 && ValueType == MsgPackType.Integer)
                return (T)(object)Convert.ToUInt16(Value);
            if (code == TypeCode.Int32 && ValueType == MsgPackType.Integer)
                return (T)(object)Convert.ToInt32(Value);
            if (code == TypeCode.UInt32 && ValueType == MsgPackType.Integer)
                return (T)(object)Convert.ToUInt32(Value);
            if (code == TypeCode.Int64 && ValueType == MsgPackType.Integer)
                return (T)(object)Convert.ToInt64(Value);
            if (code == TypeCode.UInt64 && ValueType == MsgPackType.UInt64)
                return (T)(object)Convert.ToUInt64(Value);
            if (code == TypeCode.String && ValueType == MsgPackType.String)
                return (T)(object)Convert.ToString(Value);

            //floating
            if (code == TypeCode.Single &&
                (ValueType == MsgPackType.Single || ValueType == MsgPackType.Double || ValueType == MsgPackType.Integer))
                return (T)(object)Convert.ToSingle(Value);
            if (code == TypeCode.Double &&
                (ValueType == MsgPackType.Single || ValueType == MsgPackType.Double || ValueType == MsgPackType.Integer))
                return (T)(object)Convert.ToDouble(Value);
            if (code == TypeCode.Decimal &&
                (ValueType == MsgPackType.Single || ValueType == MsgPackType.Double || ValueType == MsgPackType.Integer))
                return (T)(object)Convert.ToDecimal(Value);

            //special
            if (code == TypeCode.DateTime && ValueType == MsgPackType.DateTime || ValueType == MsgPackType.Integer)
                return (T)(object)new DateTime(Convert.ToInt64(Value));

            if (code == TypeCode.Byte && ValueType == MsgPackType.Binary)
            {
                byte[] tmp = (byte[])Value;
                if (tmp.Length == 1)
                    return (T)(object)tmp[0];
            }

            if (code == TypeCode.Char && ValueType == MsgPackType.String)
            {
                string tmp = (string)Value;
                if (tmp.Length == 1)
                    return (T)(object)tmp[0];
            }
            throw new InvalidCastException($"This MPack object is not of type {typeof(T).Name}, and no valid cast was found.");
        }
        public T ToOrDefault<T>()
        {
            try
            {
                return To<T>();
            }
            catch
            {
                return default(T);
            }
        }

        public void EncodeToStream(Stream ms)
        {
            switch (ValueType)
            {
                case MsgPackType.Unknown:
                case MsgPackType.Null:
                    WriteTools.WriteNull(ms);
                    break;
                case MsgPackType.String:
                    WriteTools.WriteString(ms, Convert.ToString(Value));
                    break;
                case MsgPackType.Integer:
                    WriteTools.WriteInteger(ms, Convert.ToInt64(Value));
                    break;
                case MsgPackType.UInt64:
                    WriteTools.WriteUInt64(ms, Convert.ToUInt64(Value));
                    break;
                case MsgPackType.Boolean:
                    WriteTools.WriteBoolean(ms, Convert.ToBoolean(Value));
                    break;
                case MsgPackType.Double:
                    WriteTools.WriteFloat(ms, Convert.ToDouble(Value));
                    break;
                case MsgPackType.Single:
                    WriteTools.WriteSingle(ms, Convert.ToSingle(Value));
                    break;
                case MsgPackType.DateTime:
                    WriteTools.WriteInteger(ms, Convert.ToInt64(((DateTime)Value).Ticks));
                    break;
                case MsgPackType.Binary:
                    WriteTools.WriteBinary(ms, (byte[])Value);
                    break;
                case MsgPackType.Map:
                    WriteMap(ms);
                    break;
                case MsgPackType.Array:
                    WirteArray(ms);
                    break;
                default:
                    WriteTools.WriteNull(ms);
                    break;
            }
        }
        public byte[] EncodeToBytes()
        {
            MemoryStream ms = new MemoryStream();
            EncodeToStream(ms);
            byte[] r = new byte[ms.Length];
            ms.Position = 0;
            ms.Read(r, 0, (int)ms.Length);
            return r;
        }

        private void WriteMap(Stream stream)
        {
            MPackMap map = this as MPackMap;
            if (map == null)
                throw new InvalidOperationException("A call to WriteMap can not occur unless type is of MsgPackMap");

            byte b;
            byte[] lenBytes;
            int len = map.Count;
            if (len <= 15)
            {
                b = (byte)(0x80 + (byte)len);
                stream.WriteByte(b);
            }
            else if (len <= 65535)
            {
                b = 0xDE;
                stream.WriteByte(b);

                lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int16)len));
                stream.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDF;
                stream.WriteByte(b);
                lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int32)len));
                stream.Write(lenBytes, 0, lenBytes.Length);
            }

            foreach (var child in map)
            {
                WriteTools.WriteString(stream, child.Key);
                child.Value.EncodeToStream(stream);
            }
        }
        private void WirteArray(Stream ms)
        {
            MPackArray list = this as MPackArray;
            if (list == null)
                throw new InvalidOperationException("A call to WirteArray can not occur unless type is of MsgPackArray");

            byte b;
            byte[] lenBytes;
            int len = list.Count;
            if (len <= 15)
            {
                b = (byte)(0x90 + (byte)len);
                ms.WriteByte(b);
            }
            else if (len <= 65535)
            {
                b = 0xDC;
                ms.WriteByte(b);

                lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int16)len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDD;
                ms.WriteByte(b);
                lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int32)len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }


            for (int i = 0; i < len; i++)
            {
                list[i].EncodeToStream(ms);
            }
        }

        public bool Equals(MPack other)
        {
            if (this is MPackArray && other is MPackArray)
            {
                var ob1 = (MPackArray)this;
                var ob2 = (MPackArray)other;
                if (ob1.Count == ob2.Count)
                {
                    return ob1.SequenceEqual(ob2);
                }
            }
            else if (this is MPackMap && other is MPackMap)
            {
                var ob1 = (MPackMap)this;
                var ob2 = (MPackMap)other;
                if (ob1.Count == ob2.Count)
                {
                    return ob1.OrderBy(r => r.Key).SequenceEqual(ob2.OrderBy(r => r.Key));
                }
            }
            else return Value.Equals(other.Value);

            return false;
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is MPack)
                return Equals((MPack)obj);
            return false;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        public override string ToString()
        {
            return Value.ToString();
        }

        private static class BytesTools
        {
            private static readonly UTF8Encoding utf8Encode = new UTF8Encoding();
            public static byte[] GetUtf8Bytes(string s)
            {

                return utf8Encode.GetBytes(s);
            }
            public static string GetString(byte[] utf8Bytes)
            {
                return utf8Encode.GetString(utf8Bytes);
            }
            public static byte[] SwapBytes(byte[] v)
            {
                byte[] r = new byte[v.Length];
                int j = v.Length - 1;
                for (int i = 0; i < r.Length; i++)
                {
                    r[i] = v[j];
                    j--;
                }
                return r;
            }
            public static byte[] SwapInt64(long v)
            {
                //byte[] r = new byte[8];
                //r[7] = (byte)v;
                //r[6] = (byte)(v >> 8);
                //r[5] = (byte)(v >> 16);
                //r[4] = (byte)(v >> 24);
                //r[3] = (byte)(v >> 32);
                //r[2] = (byte)(v >> 40);
                //r[1] = (byte)(v >> 48);
                //r[0] = (byte)(v >> 56);            
                return SwapBytes(BitConverter.GetBytes(v));
            }
            public static byte[] SwapInt32(int v)
            {
                byte[] r = new byte[4];
                r[3] = (byte)v;
                r[2] = (byte)(v >> 8);
                r[1] = (byte)(v >> 16);
                r[0] = (byte)(v >> 24);
                return r;
            }
            public static byte[] SwapInt16(Int16 v)
            {
                byte[] r = new byte[2];
                r[1] = (byte)v;
                r[0] = (byte)(v >> 8);
                return r;
            }
            public static byte[] SwapDouble(double v)
            {
                return SwapBytes(BitConverter.GetBytes(v));
            }
        }
        private static class ReadTools
        {
            public static string ReadString(Stream ms, int len)
            {
                byte[] rawBytes = new byte[len];
                ms.Read(rawBytes, 0, len);
                return BytesTools.GetString(rawBytes);
            }
            public static string ReadString(Stream ms)
            {
                byte strFlag = (byte)ms.ReadByte();
                return ReadString(strFlag, ms);
            }
            public static string ReadString(byte strFlag, Stream ms)
            {
                //
                //fixstr stores a byte array whose length is upto 31 bytes:
                //+--------+========+
                //|101XXXXX|  data  |
                //+--------+========+
                //
                //str 8 stores a byte array whose length is upto (2^8)-1 bytes:
                //+--------+--------+========+
                //|  0xd9  |YYYYYYYY|  data  |
                //+--------+--------+========+
                //
                //str 16 stores a byte array whose length is upto (2^16)-1 bytes:
                //+--------+--------+--------+========+
                //|  0xda  |ZZZZZZZZ|ZZZZZZZZ|  data  |
                //+--------+--------+--------+========+
                //
                //str 32 stores a byte array whose length is upto (2^32)-1 bytes:
                //+--------+--------+--------+--------+--------+========+
                //|  0xdb  |AAAAAAAA|AAAAAAAA|AAAAAAAA|AAAAAAAA|  data  |
                //+--------+--------+--------+--------+--------+========+
                //
                //where
                //* XXXXX is a 5-bit unsigned integer which represents N
                //* YYYYYYYY is a 8-bit unsigned integer which represents N
                //* ZZZZZZZZ_ZZZZZZZZ is a 16-bit big-endian unsigned integer which represents N
                //* AAAAAAAA_AAAAAAAA_AAAAAAAA_AAAAAAAA is a 32-bit big-endian unsigned integer which represents N
                //* N is the length of data   

                byte[] rawBytes = null;
                int len = 0;
                if ((strFlag >= 0xA0) && (strFlag <= 0xBF))
                {
                    len = strFlag - 0xA0;
                }
                else if (strFlag == 0xD9)
                {
                    len = ms.ReadByte();
                }
                else if (strFlag == 0xDA)
                {
                    rawBytes = new byte[2];
                    ms.Read(rawBytes, 0, 2);
                    rawBytes = BytesTools.SwapBytes(rawBytes);
                    len = BitConverter.ToInt16(rawBytes, 0);
                }
                else if (strFlag == 0xDB)
                {
                    rawBytes = new byte[4];
                    ms.Read(rawBytes, 0, 4);
                    rawBytes = BytesTools.SwapBytes(rawBytes);
                    len = BitConverter.ToInt32(rawBytes, 0);
                }
                rawBytes = new byte[len];
                ms.Read(rawBytes, 0, len);
                return BytesTools.GetString(rawBytes);
            }
        }
        private static class WriteTools
        {
            public static void WriteNull(Stream ms)
            {
                ms.WriteByte(0xC0);
            }
            public static void WriteString(Stream ms, String strVal)
            {
                //
                //fixstr stores a byte array whose length is upto 31 bytes:
                //+--------+========+
                //|101XXXXX|  data  |
                //+--------+========+
                //
                //str 8 stores a byte array whose length is upto (2^8)-1 bytes:
                //+--------+--------+========+
                //|  0xd9  |YYYYYYYY|  data  |
                //+--------+--------+========+
                //
                //str 16 stores a byte array whose length is upto (2^16)-1 bytes:
                //+--------+--------+--------+========+
                //|  0xda  |ZZZZZZZZ|ZZZZZZZZ|  data  |
                //+--------+--------+--------+========+
                //
                //str 32 stores a byte array whose length is upto (2^32)-1 bytes:
                //+--------+--------+--------+--------+--------+========+
                //|  0xdb  |AAAAAAAA|AAAAAAAA|AAAAAAAA|AAAAAAAA|  data  |
                //+--------+--------+--------+--------+--------+========+
                //
                //where
                //* XXXXX is a 5-bit unsigned integer which represents N
                //* YYYYYYYY is a 8-bit unsigned integer which represents N
                //* ZZZZZZZZ_ZZZZZZZZ is a 16-bit big-endian unsigned integer which represents N
                //* AAAAAAAA_AAAAAAAA_AAAAAAAA_AAAAAAAA is a 32-bit big-endian unsigned integer which represents N
                //* N is the length of data

                byte[] rawBytes = BytesTools.GetUtf8Bytes(strVal);
                byte[] lenBytes = null;
                int len = rawBytes.Length;
                byte b = 0;
                if (len <= 31)
                {
                    b = (byte)(0xA0 + (byte)len);
                    ms.WriteByte(b);
                }
                else if (len <= 255)
                {
                    b = 0xD9;
                    ms.WriteByte(b);
                    b = (byte)len;
                    ms.WriteByte(b);
                }
                else if (len <= 65535)
                {
                    b = 0xDA;
                    ms.WriteByte(b);

                    lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int16)len));
                    ms.Write(lenBytes, 0, lenBytes.Length);
                }
                else
                {
                    b = 0xDB;
                    ms.WriteByte(b);

                    lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int32)len));
                    ms.Write(lenBytes, 0, lenBytes.Length);
                }
                ms.Write(rawBytes, 0, rawBytes.Length);
            }
            public static void WriteBinary(Stream ms, byte[] rawBytes)
            {

                byte[] lenBytes = null;
                int len = rawBytes.Length;
                byte b = 0;
                if (len <= 255)
                {
                    b = 0xC4;
                    ms.WriteByte(b);
                    b = (byte)len;
                    ms.WriteByte(b);
                }
                else if (len <= 65535)
                {
                    b = 0xC5;
                    ms.WriteByte(b);

                    lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int16)len));
                    ms.Write(lenBytes, 0, lenBytes.Length);
                }
                else
                {
                    b = 0xC6;
                    ms.WriteByte(b);

                    lenBytes = BytesTools.SwapBytes(BitConverter.GetBytes((Int32)len));
                    ms.Write(lenBytes, 0, lenBytes.Length);
                }
                ms.Write(rawBytes, 0, rawBytes.Length);
            }
            public static void WriteFloat(Stream ms, Double fVal)
            {
                ms.WriteByte(0xCB);
                ms.Write(BytesTools.SwapDouble(fVal), 0, 8);
            }
            public static void WriteSingle(Stream ms, Single fVal)
            {
                ms.WriteByte(0xCA);
                ms.Write(BytesTools.SwapBytes(BitConverter.GetBytes(fVal)), 0, 4);
            }
            public static void WriteBoolean(Stream ms, Boolean bVal)
            {
                if (bVal)
                {
                    ms.WriteByte(0xC3);
                }
                else
                {
                    ms.WriteByte(0xC2);
                }
            }
            public static void WriteUInt64(Stream ms, UInt64 iVal)
            {
                ms.WriteByte(0xCF);
                byte[] dataBytes = BitConverter.GetBytes(iVal);
                ms.Write(BytesTools.SwapBytes(dataBytes), 0, 8);
            }
            public static void WriteInteger(Stream ms, Int64 iVal)
            {
                if (iVal >= 0)
                {   // fixedval
                    if (iVal <= 127)
                    {
                        ms.WriteByte((byte)iVal);
                    }
                    else if (iVal <= 255)
                    {  //UInt8
                        ms.WriteByte(0xCC);
                        ms.WriteByte((byte)iVal);
                    }
                    else if (iVal <= (UInt32)0xFFFF)
                    {  //UInt16
                        ms.WriteByte(0xCD);
                        ms.Write(BytesTools.SwapInt16((Int16)iVal), 0, 2);
                    }
                    else if (iVal <= (UInt32)0xFFFFFFFF)
                    {  //UInt32
                        ms.WriteByte(0xCE);
                        ms.Write(BytesTools.SwapInt32((Int32)iVal), 0, 4);
                    }
                    else
                    {  //Int64
                        ms.WriteByte(0xD3);
                        ms.Write(BytesTools.SwapInt64(iVal), 0, 8);
                    }
                }
                else
                {  // <0
                    if (iVal <= Int32.MinValue)  //-2147483648  // 64 bit
                    {
                        ms.WriteByte(0xD3);
                        ms.Write(BytesTools.SwapInt64(iVal), 0, 8);
                    }
                    else if (iVal <= Int16.MinValue)   // -32768    // 32 bit
                    {
                        ms.WriteByte(0xD2);
                        ms.Write(BytesTools.SwapInt32((Int32)iVal), 0, 4);
                    }
                    else if (iVal <= -128)   // -32768    // 32 bit
                    {
                        ms.WriteByte(0xD1);
                        ms.Write(BytesTools.SwapInt16((Int16)iVal), 0, 2);
                    }
                    else if (iVal <= -32)
                    {
                        ms.WriteByte(0xD0);
                        ms.WriteByte((byte)iVal);
                    }
                    else
                    {
                        ms.WriteByte((byte)iVal);
                    }
                }  // end <0
            }
        }
    }

    public sealed class MPackArray : MPack, IList<MPack>
    {
        public override object Value
        {
            get { return _collection; }
        }
        public override MsgPackType ValueType => MsgPackType.Array;
        private IList<MPack> _collection = new List<MPack>();

        public MPackArray()
        {
        }
        public MPackArray(IEnumerable<MPack> seed)
        {
            foreach (var v in seed)
                _collection.Add(v);
        }

        public MPack this[int index]
        {
            get { return _collection[index]; }
            set { _collection[index] = value; }
        }
        MPack IList<MPack>.this[int index]
        {
            get { return _collection[index]; }
            set { _collection[index] = value; }
        }
        public int Count
        {
            get { return _collection.Count; }
        }
        public bool IsReadOnly
        {
            get { return false; }
        }
        public void Add(MPack item)
        {
            _collection.Add(item);
        }
        public void Clear()
        {
            _collection.Clear();
        }
        public bool Contains(MPack item)
        {
            return _collection.Contains(item);
        }
        public void CopyTo(MPack[] array, int arrayIndex)
        {
            _collection.CopyTo(array, arrayIndex);
        }
        public IEnumerator<MPack> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
        public int IndexOf(MPack item)
        {
            return _collection.IndexOf(item);
        }
        public void Insert(int index, MPack item)
        {
            _collection.Insert(index, item);
        }
        public bool Remove(MPack item)
        {
            return _collection.Remove(item);
        }
        public void RemoveAt(int index)
        {
            _collection.RemoveAt(index);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
    }

    public class MPackMap : MPack, IDictionary<string, MPack>
    {
        public int Count => _collection.Count;
        public bool IsReadOnly => _collection.IsReadOnly;
        public override object Value
        {
            get { return _collection; }
        }
        public override MsgPackType ValueType => MsgPackType.Map;

        private IDictionary<string, MPack> _collection;

        public MPackMap()
        {
            _collection = new Dictionary<string, MPack>();
        }
        public MPackMap(IDictionary<string, MPack> seed)
        {
            _collection = seed;
        }
        public MPackMap(IEnumerable<KeyValuePair<string, MPack>> seed)
        {
            _collection = new Dictionary<string, MPack>();
            foreach (var v in seed)
                _collection.Add(v);
        }

        public IEnumerator<KeyValuePair<string, MPack>> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Add(KeyValuePair<string, MPack> item)
        {
            _collection.Add(item);
        }
        public void Clear()
        {
            _collection.Clear();
        }
        public bool Contains(KeyValuePair<string, MPack> item)
        {
            return _collection.Contains(item);
        }
        public void CopyTo(KeyValuePair<string, MPack>[] array, int arrayIndex)
        {
            _collection.CopyTo(array, arrayIndex);
        }
        public bool Remove(KeyValuePair<string, MPack> item)
        {
            return _collection.Remove(item);
        }
        public bool ContainsKey(string key)
        {
            return _collection.ContainsKey(key);
        }
        public void Add(string key, MPack value)
        {
            _collection.Add(key, value);
        }
        public bool Remove(string key)
        {
            return _collection.Remove(key);
        }
        public bool TryGetValue(string key, out MPack value)
        {
            return _collection.TryGetValue(key, out value);
        }
        public MPack this[string key]
        {
            get { return _collection[key]; }
            set { _collection[key] = value; }
        }
        public ICollection<string> Keys => _collection.Keys;
        public ICollection<MPack> Values => _collection.Values;
    }
}
