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
//  Written by Caelan Sayler git@caesay.com                                                  //
//  Original URL: https://github.com/caesay/MPack                                            //
//  Licensed: MIT                                                                            //
//                                                                                           //
///////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MPack
{
    public class MToken : IEquatable<MToken>, IConvertible, IComparable<MToken>
    {
        public virtual object Value { get { return _value; } }
        public virtual MTokenType ValueType { get { return _type; } }

        internal MToken(object value, MTokenType type)
        {
            _value = value;
            _type = type;
        }
        protected MToken()
        {
        }

        private object _value = null;
        private MTokenType _type = MTokenType.Null;

        public virtual MToken this[int index]
        {
            get
            {
                if (this is MDict)
                    return this[(MToken)index];
                throw new NotSupportedException("Array indexor not supported in this context.");
            }
            set
            {
                if (this is MDict)
                    this[(MToken)index] = value;
                else
                    throw new NotSupportedException("Array indexor not supported in this context.");
            }
        }
        public virtual MToken this[MToken key]
        {
            get
            {
                throw new NotSupportedException("Map indexor not supported in this context.");
            }
            set
            {
                throw new NotSupportedException("Map indexor not supported in this context.");
            }
        }

        public static MToken Null()
        {
            return new MToken() { _type = MTokenType.Null };
        }

        public static MToken From(object value)
        {
            return From(value, value.GetType());
        }

        public static MToken From(object value, Type type)
        {
            if (value == null)
                return new MToken(null, MTokenType.Null);

            if (!type.IsInstanceOfType(value))
                throw new ArgumentException("Type does not match provided object.");
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(byte))
                    return new MToken(value, MTokenType.Binary);
                if (elementType == typeof(MToken))
                    return new MArray((MToken[])value);

                var elementTypeCode = (int)Type.GetTypeCode(elementType);
                if (elementTypeCode <= 2 || elementTypeCode == 16)
                    throw new NotSupportedException(String.Format("The specified array type ({0}) is not supported by MsgPack", elementType.Name));

                MArray resultArray = new MArray();
                Array inputArray = (Array)value;
                foreach (var obj in inputArray)
                {
                    resultArray.Add(From(obj));
                }
                return resultArray;
            }

            TypeCode code = Type.GetTypeCode(type);
            switch (code)
            {
                case TypeCode.Boolean:
                    return new MToken(value, MTokenType.Bool);
                case TypeCode.Char:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.SByte:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.Byte:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Int16:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.UInt16:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Int32:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.UInt32:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Int64:
                    return new MToken(value, MTokenType.SInt);
                case TypeCode.UInt64:
                    return new MToken(value, MTokenType.UInt);
                case TypeCode.Single:
                    return new MToken(value, MTokenType.Single);
                case TypeCode.Double:
                    return new MToken(value, MTokenType.Double);
                case TypeCode.Decimal:
                    return new MToken((double)(decimal)value, MTokenType.Double);
                case TypeCode.String:
                    return new MToken(value, MTokenType.String);
            }
            throw new NotSupportedException("Tried to create MPack object from unsupported type: " + type.Name);
        }

        public object To(Type t)
        {
            if (ValueType == MTokenType.Null)
                return null;
            if (t == typeof(object))
                return Value;

            // handle basic array types, ex. string[], int[], etc.
            // will fail if one of the child objects is of the incorrect type.
            if (t.IsArray)
            {
                var elementType = t.GetElementType();
                if (elementType == typeof(byte))
                    return (byte[])Value;

                if (elementType == typeof(object))
                    throw new ArgumentException("Array element type must not equal typeof(object).", nameof(t));

                int elementTypeCode = (int)Type.GetTypeCode(elementType);
                if (elementTypeCode <= 2 || elementTypeCode == 16)
                    throw new NotSupportedException(String.Format("Casting to an array of type {0} is not supported.",
                        elementType.Name));

                var mpackArray = Value as MArray;
                if (mpackArray == null)
                    throw new ArgumentException(String.Format("Cannot conver MPack type {0} into type {1} (it is not an array).",
                        ValueType, t.Name));

                if (elementType == typeof(MToken))
                    return mpackArray.ToArray();

                var count = mpackArray.Count;
                var objArray = Array.CreateInstance(elementType, count);
                for (int i = 0; i < count; i++)
                    objArray.SetValue(mpackArray[i].To(elementType), i);
                return objArray;
            }

            return Convert.ChangeType(Value, t);
        }
        public T To<T>()
        {
            return (T)To(typeof(T));
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

        public static bool operator ==(MToken m1, MToken m2)
        {
            if (ReferenceEquals(m1, m2)) return true;
            if (!ReferenceEquals(m1, null))
                return m1.Equals(m2);
            return false;
        }
        public static bool operator !=(MToken m1, MToken m2)
        {
            if (ReferenceEquals(m1, m2)) return false;
            if (!ReferenceEquals(m1, null))
                return !m1.Equals(m2);
            return true;
        }

        public static implicit operator MToken(bool value) { return From(value); }
        public static implicit operator MToken(float value) { return From(value); }
        public static implicit operator MToken(double value) { return From(value); }
        public static implicit operator MToken(byte value) { return From(value); }
        public static implicit operator MToken(ushort value) { return From(value); }
        public static implicit operator MToken(uint value) { return From(value); }
        public static implicit operator MToken(ulong value) { return From(value); }
        public static implicit operator MToken(sbyte value) { return From(value); }
        public static implicit operator MToken(short value) { return From(value); }
        public static implicit operator MToken(int value) { return From(value); }
        public static implicit operator MToken(long value) { return From(value); }
        public static implicit operator MToken(string value) { return From(value); }
        public static implicit operator MToken(byte[] value) { return From(value); }
        public static implicit operator MToken(MToken[] value) { return From(value); }

        public static explicit operator bool(MToken value) { return value.To<bool>(); }
        public static explicit operator float(MToken value) { return value.To<float>(); }
        public static explicit operator double(MToken value) { return value.To<double>(); }
        public static explicit operator byte(MToken value) { return value.To<byte>(); }
        public static explicit operator ushort(MToken value) { return value.To<ushort>(); }
        public static explicit operator uint(MToken value) { return value.To<uint>(); }
        public static explicit operator ulong(MToken value) { return value.To<ulong>(); }
        public static explicit operator sbyte(MToken value) { return value.To<sbyte>(); }
        public static explicit operator short(MToken value) { return value.To<short>(); }
        public static explicit operator int(MToken value) { return value.To<int>(); }
        public static explicit operator long(MToken value) { return value.To<long>(); }
        public static explicit operator string(MToken value) { return value.To<string>(); }
        public static explicit operator byte[](MToken value) { return value.To<byte[]>(); }
        public static explicit operator MToken[](MToken value) { return value.To<MToken[]>(); }

        public static MToken ParseFromBytes(byte[] array)
        {
            using (MemoryStream ms = new MemoryStream(array))
                return ParseFromStream(ms);
        }
        public static MToken ParseFromStream(Stream stream)
        {
            return Reader.ParseFromStream(stream);
        }
        public static Task<MToken> ParseFromStreamAsync(Stream stream)
        {
            return ParseFromStreamAsync(stream, CancellationToken.None);
        }
        public static Task<MToken> ParseFromStreamAsync(Stream stream, CancellationToken token)
        {
            return Reader.ParseFromStreamAsync(stream, token);
        }
        public void EncodeToStream(Stream stream)
        {
            Writer.EncodeToStream(stream, this);

        }
        public Task EncodeToStreamAsync(Stream stream)
        {
            return EncodeToStreamAsync(stream, CancellationToken.None);
        }
        public async Task EncodeToStreamAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Writer.EncodeToStream(ms, this);

                ms.Position = 0;
                await ms.CopyToAsync(stream, 65535, token);
            }
        }
        public byte[] EncodeToBytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Writer.EncodeToStream(ms, this);
                return ms.ToArray();
            }
        }

        public bool Equals(MToken other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (this is MArray && other is MArray)
            {
                var ob1 = (MArray)this;
                var ob2 = (MArray)other;
                if (ob1.Count == ob2.Count)
                {
                    return ob1.SequenceEqual(ob2);
                }
            }
            else if (this is MDict && other is MDict)
            {
                var ob1 = (MDict)this;
                var ob2 = (MDict)other;
                if (ob1.Count == ob2.Count)
                {
                    return ob1.OrderBy(r => r.Key).SequenceEqual(ob2.OrderBy(r => r.Key));
                }
            }
            else if ((this.ValueType == MTokenType.SInt || this.ValueType == MTokenType.UInt) &&
                     (other.ValueType == MTokenType.SInt || other.ValueType == MTokenType.UInt))
            {
                decimal xd = Convert.ToDecimal(Value);
                decimal yd = Convert.ToDecimal(other.Value);
                return xd == yd;
            }
            else return Value.Equals(other.Value);

            return false;
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is MToken)
                return Equals((MToken)obj);
            return false;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        public override string ToString()
        {
            if (Value == null)
                return "null";
            return Value.ToString();
        }

        TypeCode IConvertible.GetTypeCode()
        {
            if (ValueType == MTokenType.Null)
                return TypeCode.Object;
            return Type.GetTypeCode(Value.GetType());
        }
        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            return To<bool>();
        }
        char IConvertible.ToChar(IFormatProvider provider)
        {
            return To<char>();
        }
        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            return To<sbyte>();
        }
        byte IConvertible.ToByte(IFormatProvider provider)
        {
            return To<byte>();
        }
        short IConvertible.ToInt16(IFormatProvider provider)
        {
            return To<short>();
        }
        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            return To<ushort>();
        }
        int IConvertible.ToInt32(IFormatProvider provider)
        {
            return To<int>();
        }
        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            return To<uint>();
        }
        long IConvertible.ToInt64(IFormatProvider provider)
        {
            return To<long>();
        }
        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            return To<ulong>();
        }
        float IConvertible.ToSingle(IFormatProvider provider)
        {
            return To<float>();
        }
        double IConvertible.ToDouble(IFormatProvider provider)
        {
            return To<double>();
        }
        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            return To<decimal>();
        }
        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            return To<DateTime>();
        }
        string IConvertible.ToString(IFormatProvider provider)
        {
            return To<string>();
        }
        object IConvertible.ToType(Type conversionType, IFormatProvider provider)
        {
            return To(conversionType);
        }

        public int CompareTo(MToken other)
        {
            // This interface is meant to be used when we need to order a MPackMap by its keys.
            // Only makes sense for numeric and string types.
            // Since we can mix numeric and string keys, we choose that numerics come before strings.
            bool isNum(MTokenType t) => t == MTokenType.SInt || t == MTokenType.UInt || t == MTokenType.Single || t == MTokenType.Double;

            if (isNum(ValueType))
            {
                if (isNum(other.ValueType))
                {
                    var thisVal = To<double>();
                    var otherVal = other.To<double>();
                    return thisVal.CompareTo(otherVal);
                }
                if (other.ValueType == MTokenType.String)
                    return -1;
                throw new NotSupportedException("Only numeric or string types can be compared.");
            }
            if (ValueType == MTokenType.String)
            {
                if (isNum(other.ValueType))
                    return 1;
                if (other.ValueType == MTokenType.String)
                    return To<string>().CompareTo(other.To<string>());
            }
            throw new NotSupportedException("Only numeric or string types can be compared.");
        }
    }
}