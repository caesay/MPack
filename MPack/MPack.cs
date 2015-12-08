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
using System.Threading;
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
        public virtual object Value { get { return _value; } }
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
        private static readonly ReaderLookup _lookup = new ReaderLookup();

        static MPack()
        {
            //positive fixint	0xxxxxxx	0x00 - 0x7f
            _lookup.AddRange(0, 0x7F,
                (b, s) => FromInteger(b),
                (b, s, c) => Task.FromResult(FromInteger(b)));

            //positive fixint	0xxxxxxx	0x00 - 0x7f
            _lookup.AddRange(0x80, 0x8F, (b, s) =>
            {
                MPackMap map = new MPackMap();
                int len = b - 0x80;
                for (int i = 0; i < len; i++)
                {
                    map.Add(MPackExtensions.ReadString(s), ParseFromStream(s));
                }
                return map;
            }, async (b, s, c) =>
            {
                MPackMap map = new MPackMap();
                int len = b - 0x80;
                for (int i = 0; i < len; i++)
                {
                    map.Add(await MPackExtensions.ReadStringAsync(s, c), await ParseFromStreamAsync(s, c));
                }
                return map;
            });

            //fixarray	1001xxxx	0x90 - 0x9f
            _lookup.AddRange(0x90, 0x9f, (b, s) =>
            {
                MPackArray array = new MPackArray();
                int len = b - 0x90;
                for (int i = 0; i < len; i++)
                {
                    array.Add(ParseFromStream(s));
                }
                return array;
            }, async (b, s, c) =>
            {
                MPackArray array = new MPackArray();
                int len = b - 0x90;
                for (int i = 0; i < len; i++)
                {
                    array.Add(await ParseFromStreamAsync(s, c));
                }
                return array;
            });

            // fixstr	101xxxxx	0xa0 - 0xbf
            _lookup.AddRange(0xA0, 0xBF, (b, s) =>
            {
                int len = b - 0xA0;
                return FromString(MPackExtensions.ReadString(s, len));
            }, async (b, s, c) =>
            {
                int len = b - 0xA0;
                return FromString(await MPackExtensions.ReadStringAsync(s, len, c));
            });

            // negative fixnum stores 5-bit negative integer 111xxxxx
            _lookup.AddRange(0xE0, 0xFF,
                (b, s) => FromInteger((sbyte)b),
                (b, s, c) => Task.FromResult(FromInteger((sbyte)b)));

            // null
            _lookup.Add(0xC0,
                (b, s) => Null(),
                (b, s, c) => Task.FromResult(Null()));

            // note, no 0xC1, it's not used.

            // bool: false
            _lookup.Add(0xC2,
                (b, s) => FromBool(false),
                (b, s, c) => Task.FromResult(FromBool(false)));

            // bool: true
            _lookup.Add(0xC3,
                (b, s) => FromBool(true),
                (b, s, c) => Task.FromResult(FromBool(true)));

            // binary array, max 255
            _lookup.Add(0xC4, (b, s) =>
            {
                int len = s.ReadByte();
                return FromBytes(s.FillBuffer(len));
            }, async (b, s, c) =>
            {
                int len = await s.ReadByteAsync(c);
                return FromBytes(await s.FillBufferAsync(len, c));
            });

            // binary array, max 65535
            _lookup.Add(0xC5, (b, s) =>
            {
                int len = BitConverter.ToInt16(s.FillBuffer(2).SwapBytes(), 0);
                return FromBytes(s.FillBuffer(len));
            }, async (b, s, c) =>
            {
                int len = BitConverter.ToInt16((await s.FillBufferAsync(2, c))
                    .SwapBytes(), 0);
                return FromBytes(await s.FillBufferAsync(len, c));
            });

            // binary max: 2^32-1                
            _lookup.Add(0xC6, (b, s) =>
            {
                int len = BitConverter.ToInt32(s.FillBuffer(4).SwapBytes(), 0);
                return FromBytes(s.FillBuffer(len));
            }, async (b, s, c) =>
            {
                int len = BitConverter.ToInt32((await s.FillBufferAsync(4, c))
                    .SwapBytes(), 0);
                return FromBytes(await s.FillBufferAsync(len, c));
            });

            // note 0xC7, 0xC8, 0xC9, not used.

            // float 32 stores a floating point number in IEEE 754 single precision floating point number     
            _lookup.Add(0xCA, (b, s) =>
            {
                var raw = s.FillBuffer(4).SwapBytes();
                return FromSingle(BitConverter.ToSingle(raw, 0));
            }, async (b, s, c) =>
            {
                var raw = (await s.FillBufferAsync(4, c)).SwapBytes();
                return FromSingle(BitConverter.ToSingle(raw, 0));
            });

            // float 64 stores a floating point number in IEEE 754 double precision floating point number        
            _lookup.Add(0xCB, (b, s) =>
            {
                var raw = s.FillBuffer(8).SwapBytes();
                return FromDouble(BitConverter.ToDouble(raw, 0));
            }, async (b, s, c) =>
            {
                var raw = (await s.FillBufferAsync(8, c)).SwapBytes();
                return FromDouble(BitConverter.ToDouble(raw, 0));
            });

            // uint8   0xcc   xxxxxxxx
            _lookup.Add(0xCC,
                (b, s) => FromInteger((byte)s.ReadByte()),
                async (b, s, c) => FromInteger((byte)await s.ReadByteAsync(c)));

            // uint16   0xcd xxxxxxxx xxxxxxxx   
            _lookup.Add(0xCD, (b, s) =>
            {
                var v = BitConverter.ToUInt16(s.FillBuffer(2).SwapBytes(), 0);
                return FromInteger(v);
            }, async (b, s, c) =>
            {
                var v = BitConverter.ToUInt16((await s.FillBufferAsync(2, c))
                    .SwapBytes(), 0);
                return FromInteger(v);
            });

            // uint32   0xce xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx  
            _lookup.Add(0xCE, (b, s) =>
            {
                var v = BitConverter.ToUInt32(s.FillBuffer(4).SwapBytes(), 0);
                return FromInteger(v);
            }, async (b, s, c) =>
            {
                var v = BitConverter.ToUInt32((await s.FillBufferAsync(4, c))
                    .SwapBytes(), 0);
                return FromInteger(v);
            });

            // uint64   0xcF   xxxxxxxx (x4)
            _lookup.Add(0xCF, (b, s) =>
            {
                var v = BitConverter.ToUInt64(s.FillBuffer(8).SwapBytes(), 0);
                return FromInteger(v);
            }, async (b, s, c) =>
            {
                var v = BitConverter.ToUInt64((await s.FillBufferAsync(8, c))
                    .SwapBytes(), 0);
                return FromInteger(v);
            });

            // array (int16 length)
            _lookup.Add(0xDC, (b, s) =>
            {
                int len = BitConverter.ToInt16(s.FillBuffer(2).SwapBytes(), 0);
                MPackArray array = new MPackArray();
                for (int i = 0; i < len; i++)
                {
                    array.Add(ParseFromStream(s));
                }
                return array;
            }, async (b, s, c) =>
            {
                int len = BitConverter.ToInt16((await s.FillBufferAsync(2, c))
                    .SwapBytes(), 0);
                MPackArray array = new MPackArray();
                for (int i = 0; i < len; i++)
                {
                    array.Add(await ParseFromStreamAsync(s, c));
                }
                return array;
            });

            // array (int32 length)
            _lookup.Add(0xDD, (b, s) =>
            {
                int len = BitConverter.ToInt32(s.FillBuffer(4).SwapBytes(), 0);
                MPackArray array = new MPackArray();
                for (int i = 0; i < len; i++)
                {
                    array.Add(ParseFromStream(s));
                }
                return array;
            }, async (b, s, c) =>
            {
                int len = BitConverter.ToInt32((await s.FillBufferAsync(4, c))
                    .SwapBytes(), 0);
                MPackArray array = new MPackArray();
                for (int i = 0; i < len; i++)
                {
                    array.Add(await ParseFromStreamAsync(s, c));
                }
                return array;
            });

            // map (int16 length)
            _lookup.Add(0xDE, (b, s) =>
            {
                int len = BitConverter.ToInt16(s.FillBuffer(2).SwapBytes(), 0);
                MPackMap map = new MPackMap();
                for (int i = 0; i < len; i++)
                {
                    var key = MPackExtensions.ReadString(s);
                    var val = ParseFromStream(s);
                    map.Add(key, val);
                }
                return map;
            }, async (b, s, c) =>
            {
                int len = BitConverter.ToInt16((await s.FillBufferAsync(2, c))
                    .SwapBytes(), 0);
                MPackMap map = new MPackMap();
                for (int i = 0; i < len; i++)
                {
                    var key = await MPackExtensions.ReadStringAsync(s, c);
                    var val = await ParseFromStreamAsync(s, c);
                    map.Add(key, val);
                }
                return map;
            });

            // map (int32 length)
            _lookup.Add(0xDF, (b, s) =>
            {
                int len = BitConverter.ToInt32(s.FillBuffer(4).SwapBytes(), 0);
                MPackMap map = new MPackMap();
                for (int i = 0; i < len; i++)
                {
                    var key = MPackExtensions.ReadString(s);
                    var val = ParseFromStream(s);
                    map.Add(key, val);
                }
                return map;
            }, async (b, s, c) =>
            {
                int len = BitConverter.ToInt32((await s.FillBufferAsync(4, c))
                    .SwapBytes(), 0);
                MPackMap map = new MPackMap();
                for (int i = 0; i < len; i++)
                {
                    var key = await MPackExtensions.ReadStringAsync(s, c);
                    var val = await ParseFromStreamAsync(s, c);
                    map.Add(key, val);
                }
                return map;
            });

            //  str family
            _lookup.AddRange(0xD9, 0xDB,
                (b, s) => FromString(MPackExtensions.ReadString(b, s)),
                async (b, s, c) => FromString(await MPackExtensions.ReadStringAsync(b, s, c)));

            // int8   0xD0   xxxxxxxx
            _lookup.Add(0xD0,
                (b, s) => FromInteger((sbyte)s.ReadByte()),
                async (b, s, c) => FromInteger((sbyte)await s.ReadByteAsync(c)));

            // int16   0xd1 xxxxxxxx xxxxxxxx   
            _lookup.Add(0xD1, (b, s) =>
            {
                var v = BitConverter.ToInt16(s.FillBuffer(2).SwapBytes(), 0);
                return FromInteger(v);
            }, async (b, s, c) =>
            {
                var v = BitConverter.ToInt16((await s.FillBufferAsync(2, c))
                    .SwapBytes(), 0);
                return FromInteger(v);
            });

            // int32    xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx  
            _lookup.Add(0xD2, (b, s) =>
            {
                var v = BitConverter.ToInt32(s.FillBuffer(4).SwapBytes(), 0);
                return FromInteger(v);
            }, async (b, s, c) =>
            {
                var v = BitConverter.ToInt32((await s.FillBufferAsync(4, c))
                    .SwapBytes(), 0);
                return FromInteger(v);
            });

            // int64      xxxxxxxx (x4)
            _lookup.Add(0xD3, (b, s) =>
            {
                var v = BitConverter.ToInt64(s.FillBuffer(8).SwapBytes(), 0);
                return FromInteger(v);
            }, async (b, s, c) =>
            {
                var v = BitConverter.ToInt64((await s.FillBufferAsync(8, c))
                    .SwapBytes(), 0);
                return FromInteger(v);
            });
        }

        public MPack this[int index]
        {
            get
            {
                var array = this as MPackArray;
                if (array == null)
                    throw new InvalidOperationException("Can not use array indexor on mpack type: " + ValueType);
                return array[index];
            }
            set
            {
                var array = this as MPackArray;
                if (array == null)
                    throw new InvalidOperationException("Can not use array indexor on mpack type: " + ValueType);
                array[index] = value;
            }
        }
        public MPack this[string key]
        {
            get
            {
                var map = this as MPackMap;
                if (map == null)
                    throw new InvalidOperationException("Can not use map indexor on mpack type: " + ValueType);
                return map[key];
            }
            set
            {
                var map = this as MPackMap;
                if (map == null)
                    throw new InvalidOperationException("Can not use map indexor on mpack type: " + ValueType);
                map[key] = value;

            }
        }

        public static MPack ParseFromBytes(byte[] array)
        {
            using (MemoryStream ms = new MemoryStream(array))
                return ParseFromStream(ms);
        }
        public static MPack ParseFromStream(Stream stream)
        {
            byte selector = (byte)stream.ReadByte();
            return _lookup[selector].Read(selector, stream);
        }
        public static Task<MPack> ParseFromStreamAsync(Stream stream)
        {
            return ParseFromStreamAsync(stream, CancellationToken.None);
        }
        public static async Task<MPack> ParseFromStreamAsync(Stream stream, CancellationToken token)
        {
            byte selector = await stream.ReadByteAsync(token);
            return await _lookup[selector].ReadAsync(selector, stream, token);
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
            if (code == TypeCode.Byte && ValueType == MsgPackType.Integer)
                return (T)(object)Convert.ToByte(Value);
            if (code == TypeCode.SByte && ValueType == MsgPackType.Integer)
                return (T)(object)Convert.ToSByte(Value);
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

            //string
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

            if (code == TypeCode.Char && ValueType == MsgPackType.Integer)
            {
                return (T)(object)(int)Value;
            }
            throw new InvalidCastException("This MPack object is not of type " + typeof(T).Name + ", and no valid cast was found.");
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

        public void EncodeToStream(Stream stream)
        {
            EncodeToStreamInternal(stream);
        }
        public Task EncodeToStreamAsync(Stream stream)
        {
            return EncodeToStreamAsync(stream, CancellationToken.None);
        }
        public async Task EncodeToStreamAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                EncodeToStreamInternal(ms);
                ms.Position = 0;
                await ms.CopyToAsync(stream, 65535, token);
            }
        }
        public byte[] EncodeToBytes()
        {
            MemoryStream ms = new MemoryStream();
            EncodeToStreamInternal(ms);
            return ms.ToArray();
        }

        private void EncodeToStreamInternal(Stream ms)
        {
            switch (ValueType)
            {
                case MsgPackType.Unknown:
                case MsgPackType.Null:
                    MPackExtensions.WriteNull(ms);
                    break;
                case MsgPackType.String:
                    MPackExtensions.WriteString(ms, Convert.ToString(Value));
                    break;
                case MsgPackType.Integer:
                    MPackExtensions.WriteInteger(ms, Convert.ToInt64(Value));
                    break;
                case MsgPackType.UInt64:
                    MPackExtensions.WriteUInt64(ms, Convert.ToUInt64(Value));
                    break;
                case MsgPackType.Boolean:
                    MPackExtensions.WriteBoolean(ms, Convert.ToBoolean(Value));
                    break;
                case MsgPackType.Double:
                    MPackExtensions.WriteFloat(ms, Convert.ToDouble(Value));
                    break;
                case MsgPackType.Single:
                    MPackExtensions.WriteSingle(ms, Convert.ToSingle(Value));
                    break;
                case MsgPackType.DateTime:
                    MPackExtensions.WriteInteger(ms, Convert.ToInt64(((DateTime)Value).Ticks));
                    break;
                case MsgPackType.Binary:
                    MPackExtensions.WriteBinary(ms, (byte[])Value);
                    break;
                case MsgPackType.Map:
                    WriteMap(ms);
                    break;
                case MsgPackType.Array:
                    WirteArray(ms);
                    break;
                default:
                    MPackExtensions.WriteNull(ms);
                    break;
            }
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

                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int16)len));
                stream.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDF;
                stream.WriteByte(b);
                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int32)len));
                stream.Write(lenBytes, 0, lenBytes.Length);
            }

            foreach (var child in map)
            {
                MPackExtensions.WriteString(stream, child.Key);
                child.Value.EncodeToStreamInternal(stream);
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

                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int16)len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDD;
                ms.WriteByte(b);
                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int32)len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }


            for (int i = 0; i < len; i++)
            {
                list[i].EncodeToStreamInternal(ms);
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

        private class Reader
        {
            private readonly Func<byte, Stream, MPack> _sync;
            private readonly Func<byte, Stream, CancellationToken, Task<MPack>> _async;

            public Reader(Func<byte, Stream, MPack> sync, Func<byte, Stream, CancellationToken, Task<MPack>> async)
            {
                _sync = sync;
                _async = async;
            }

            public MPack Read(byte selector, Stream stream)
            {
                return _sync(selector, stream);
            }
            public Task<MPack> ReadAsync(byte selector, Stream stream, CancellationToken token)
            {
                return _async(selector, stream, token);
            }
        }

        private class ReaderLookup : Dictionary<byte, Reader>
        {
            public void AddRange(byte rangeStart, byte rangeEnd, Reader reader)
            {
                var test = Enumerable.Range(rangeStart, rangeEnd - rangeStart + 1).ToArray();
                foreach (var b in test.Select(i => (byte)i))
                {
                    this.Add(b, reader);
                }
            }
            public void AddRange(byte rangeStart, byte rangeEnd, Func<byte, Stream, MPack> sync,
                Func<byte, Stream, CancellationToken, Task<MPack>> async)
            {
                AddRange(rangeStart, rangeEnd, new Reader(sync, async));
            }
            public void Add(byte key, Func<byte, Stream, MPack> sync,
                Func<byte, Stream, CancellationToken, Task<MPack>> async)
            {
                Add(key, new Reader(sync, async));
            }
        }
    }

    internal static class MPackExtensions
    {
        private const string EX_STREAMEND = "Stream ended but expecting more data. Data may be incorrupt or stream ended prematurely.";

        public static string ReadString(Stream ms, int len)
        {
            byte[] rawBytes = new byte[len];
            ms.Read(rawBytes, 0, len);
            return Encoding.UTF8.GetString(rawBytes);
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
                rawBytes = SwapBytes(rawBytes);
                len = BitConverter.ToInt16(rawBytes, 0);
            }
            else if (strFlag == 0xDB)
            {
                rawBytes = new byte[4];
                ms.Read(rawBytes, 0, 4);
                rawBytes = SwapBytes(rawBytes);
                len = BitConverter.ToInt32(rawBytes, 0);
            }
            rawBytes = new byte[len];
            ms.Read(rawBytes, 0, len);
            return Encoding.UTF8.GetString(rawBytes);
        }
        public static async Task<string> ReadStringAsync(byte strFlag, Stream ms, CancellationToken token)
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
            byte[] rawBytes;
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
                rawBytes = await FillBufferAsync(ms, 2, token);
                rawBytes = SwapBytes(rawBytes);
                len = BitConverter.ToInt16(rawBytes, 0);
            }
            else if (strFlag == 0xDB)
            {
                rawBytes = await FillBufferAsync(ms, 4, token);
                rawBytes = SwapBytes(rawBytes);
                len = BitConverter.ToInt32(rawBytes, 0);
            }
            rawBytes = await FillBufferAsync(ms, len, token);
            return Encoding.UTF8.GetString(rawBytes);
        }
        public static async Task<string> ReadStringAsync(Stream ms, CancellationToken token)
        {
            return await ReadStringAsync((await FillBufferAsync(ms, 1, token))[0], ms, token);
        }
        public static async Task<string> ReadStringAsync(Stream ms, int len, CancellationToken token)
        {
            return Encoding.UTF8.GetString(await FillBufferAsync(ms, len, token));
        }
        public static async Task<byte> ReadByteAsync(this Stream ms, CancellationToken token)
        {
            var buffer = await FillBufferAsync(ms, 1, token);
            return buffer[0];
        }

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

            byte[] rawBytes = Encoding.UTF8.GetBytes(strVal);
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

                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int16)len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDB;
                ms.WriteByte(b);

                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int32)len));
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

                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int16)len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xC6;
                ms.WriteByte(b);

                lenBytes = MPackExtensions.SwapBytes(BitConverter.GetBytes((Int32)len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            ms.Write(rawBytes, 0, rawBytes.Length);
        }
        public static void WriteFloat(Stream ms, Double fVal)
        {
            ms.WriteByte(0xCB);

            ms.Write(BitConverter.GetBytes(fVal).SwapBytes(), 0, 8);
        }
        public static void WriteSingle(Stream ms, Single fVal)
        {
            ms.WriteByte(0xCA);
            ms.Write(MPackExtensions.SwapBytes(BitConverter.GetBytes(fVal)), 0, 4);
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
            ms.Write(MPackExtensions.SwapBytes(dataBytes), 0, 8);
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
                    ms.Write(BitConverter.GetBytes((Int16)iVal).SwapBytes(), 0, 2);
                }
                else if (iVal <= (UInt32)0xFFFFFFFF)
                {  //UInt32
                    ms.WriteByte(0xCE);
                    ms.Write(BitConverter.GetBytes((Int32)iVal).SwapBytes(), 0, 4);
                }
                else
                {  //Int64
                    ms.WriteByte(0xD3);
                    ms.Write(BitConverter.GetBytes(iVal).SwapBytes(), 0, 8);
                }
            }
            else
            {  // <0
                if (iVal <= Int32.MinValue)  //-2147483648  // 64 bit
                {
                    ms.WriteByte(0xD3);
                    ms.Write(BitConverter.GetBytes(iVal).SwapBytes(), 0, 8);
                }
                else if (iVal <= Int16.MinValue)   // -32768    // 32 bit
                {
                    ms.WriteByte(0xD2);
                    ms.Write(BitConverter.GetBytes((Int32)iVal).SwapBytes(), 0, 4);
                }
                else if (iVal <= -128)   // -32768    // 32 bit
                {
                    ms.WriteByte(0xD1);
                    ms.Write(BitConverter.GetBytes((Int16)iVal).SwapBytes(), 0, 2);
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

        public static byte[] SwapBytes(this byte[] v)
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

        public static byte[] FillBuffer(this Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int read = FillBuffer_internal(stream, buffer, 0, count);
            if (read != count)
                throw new InvalidDataException(EX_STREAMEND);
            return buffer;
        }
        private static int FillBuffer_internal(Stream stream, byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            while (length > 0)
            {
                var read = stream.Read(buffer, offset, length);
                if (read == 0)
                    return totalRead;
                offset += read;
                length -= read;
                totalRead += read;
            }
            return totalRead;
        }

        public static async Task<byte[]> FillBufferAsync(this Stream stream, int count, CancellationToken token)
        {
            byte[] buffer = new byte[count];
            int read = await FillBufferAsync_internal(stream, buffer, 0, count, token);
            if (read != count)
                throw new InvalidDataException(EX_STREAMEND);
            return buffer;
        }
        private static async Task<int> FillBufferAsync_internal(Stream stream, byte[] buffer, int offset, int length, CancellationToken token)
        {
            int totalRead = 0;
            while (length > 0 && !token.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, offset, length, token);
                if (read == 0)
                    return totalRead;
                offset += read;
                length -= read;
                totalRead += read;
            }
            return totalRead;
        }
    }

    public sealed class MPackArray : MPack, IList<MPack>
    {
        public override object Value
        {
            get { return _collection; }
        }
        public override MsgPackType ValueType { get { return MsgPackType.Array; } }
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
        public int Count { get { return _collection.Count; } }

        public bool IsReadOnly { get { return _collection.IsReadOnly; } }

        public override object Value
        {
            get { return _collection; }
        }
        public override MsgPackType ValueType { get { return MsgPackType.Map; } }

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
