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

namespace CS
{
    public class MPack : IEquatable<MPack>, IConvertible
    {
        public virtual object Value { get { return _value; } }
        public virtual MPackType ValueType { get { return _type; } }

        internal MPack(object value, MPackType type)
        {
            _value = value;
            _type = type;
        }
        protected MPack()
        {
        }

        private object _value = null;
        private MPackType _type = MPackType.Null;

        public virtual MPack this[int index]
        {
            get
            {
                if (this is MPackMap)
                    return this[(MPack)index];
                throw new NotSupportedException("Array indexor not supported in this context.");
            }
            set
            {
                if (this is MPackMap)
                    this[(MPack)index] = value;
                else
                    throw new NotSupportedException("Array indexor not supported in this context.");
            }
        }
        public virtual MPack this[MPack key]
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

        public static MPack Null()
        {
            return new MPack() { _type = MPackType.Null };
        }
        public static MPack From(object value)
        {
            if (value == null)
                return new MPack(null, MPackType.Null);

            var type = value.GetType();
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == typeof(byte))
                    return new MPack(value, MPackType.Binary);
                if (elementType == typeof(MPack))
                    return new MPackArray((MPack[])value);

                var elementTypeCode = (int)Type.GetTypeCode(elementType);
                if (elementTypeCode <= 2 || elementTypeCode == 16)
                    throw new NotSupportedException(String.Format("The specified array type ({0}) is not supported by MsgPack", elementType.Name));

                MPackArray resultArray = new MPackArray();
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
                    return new MPack(value, MPackType.Bool);
                case TypeCode.Char:
                    return new MPack(value, MPackType.UInt);
                case TypeCode.SByte:
                    return new MPack(value, MPackType.SInt);
                case TypeCode.Byte:
                    return new MPack(value, MPackType.UInt);
                case TypeCode.Int16:
                    return new MPack(value, MPackType.SInt);
                case TypeCode.UInt16:
                    return new MPack(value, MPackType.UInt);
                case TypeCode.Int32:
                    return new MPack(value, MPackType.SInt);
                case TypeCode.UInt32:
                    return new MPack(value, MPackType.UInt);
                case TypeCode.Int64:
                    return new MPack(value, MPackType.SInt);
                case TypeCode.UInt64:
                    return new MPack(value, MPackType.UInt);
                case TypeCode.Single:
                    return new MPack(value, MPackType.Single);
                case TypeCode.Double:
                    return new MPack(value, MPackType.Double);
                case TypeCode.Decimal:
                    return new MPack((double)(decimal)value, MPackType.Double);
                case TypeCode.String:
                    return new MPack(value, MPackType.String);
            }
            throw new NotSupportedException("Tried to create MPack object from unsupported type: " + type.Name);
        }

        public object To(Type t)
        {
            if (ValueType == MPackType.Null)
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

                var mpackArray = Value as MPackArray;
                if (mpackArray == null)
                    throw new ArgumentException(String.Format("Cannot conver MPack type {0} into type {1} (it is not an array).",
                        ValueType, t.Name));

                if (elementType == typeof(MPack))
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

        public static bool operator ==(MPack m1, MPack m2)
        {
            if (ReferenceEquals(m1, m2)) return true;
            if (!ReferenceEquals(m1, null))
                return m1.Equals(m2);
            return false;
        }
        public static bool operator !=(MPack m1, MPack m2)
        {
            if (ReferenceEquals(m1, m2)) return false;
            if (!ReferenceEquals(m1, null))
                return !m1.Equals(m2);
            return true;
        }

        public static implicit operator MPack(bool value) { return From(value); }
        public static implicit operator MPack(float value) { return From(value); }
        public static implicit operator MPack(double value) { return From(value); }
        public static implicit operator MPack(byte value) { return From(value); }
        public static implicit operator MPack(ushort value) { return From(value); }
        public static implicit operator MPack(uint value) { return From(value); }
        public static implicit operator MPack(ulong value) { return From(value); }
        public static implicit operator MPack(sbyte value) { return From(value); }
        public static implicit operator MPack(short value) { return From(value); }
        public static implicit operator MPack(int value) { return From(value); }
        public static implicit operator MPack(string value) { return From(value); }
        public static implicit operator MPack(byte[] value) { return From(value); }
        public static implicit operator MPack(MPack[] value) { return From(value); }

        public static explicit operator bool(MPack value) { return value.To<bool>(); }
        public static explicit operator float(MPack value) { return value.To<float>(); }
        public static explicit operator double(MPack value) { return value.To<double>(); }
        public static explicit operator byte(MPack value) { return value.To<byte>(); }
        public static explicit operator ushort(MPack value) { return value.To<ushort>(); }
        public static explicit operator uint(MPack value) { return value.To<uint>(); }
        public static explicit operator ulong(MPack value) { return value.To<ulong>(); }
        public static explicit operator sbyte(MPack value) { return value.To<sbyte>(); }
        public static explicit operator short(MPack value) { return value.To<short>(); }
        public static explicit operator int(MPack value) { return value.To<int>(); }
        public static explicit operator string(MPack value) { return value.To<string>(); }
        public static explicit operator byte[] (MPack value) { return value.To<byte[]>(); }
        public static explicit operator MPack[] (MPack value) { return value.To<MPack[]>(); }

        public static MPack ParseFromBytes(byte[] array)
        {
            using (MemoryStream ms = new MemoryStream(array))
                return ParseFromStream(ms);
        }
        public static MPack ParseFromStream(Stream stream)
        {
            return Reader.ParseFromStream(stream);
        }
        public static Task<MPack> ParseFromStreamAsync(Stream stream)
        {
            return ParseFromStreamAsync(stream, CancellationToken.None);
        }
        public static Task<MPack> ParseFromStreamAsync(Stream stream, CancellationToken token)
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

        public bool Equals(MPack other)
        {
            if (ReferenceEquals(other, null))
                return false;

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
            else if ((this.ValueType == MPackType.SInt || this.ValueType == MPackType.UInt) &&
                     (other.ValueType == MPackType.SInt || other.ValueType == MPackType.UInt))
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
            if (Value == null)
                return "null";
            return Value.ToString();
        }

        TypeCode IConvertible.GetTypeCode()
        {
            if (ValueType == MPackType.Null)
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
    }
}