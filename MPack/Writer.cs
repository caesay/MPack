using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS
{
    internal class Writer
    {
        private static Encoding _encoding = Encoding.UTF8;
        private static IBitConverter _convert = EndianBitConverter.Big;

        public static void EncodeToStream(Stream ms, MPack m)
        {
            switch (m.ValueType)
            {
                case MPackType.Null:
                    WriteNull(ms);
                    break;
                case MPackType.String:
                    WriteString(ms, Convert.ToString(m.Value));
                    break;
                case MPackType.SInt:
                    WriteInteger(ms, Convert.ToInt64(m.Value));
                    break;
                case MPackType.UInt:
                    WriteInteger(ms, Convert.ToUInt64(m.Value));
                    break;
                case MPackType.Bool:
                    WriteBoolean(ms, Convert.ToBoolean(m.Value));
                    break;
                case MPackType.Single:
                    WriteSingle(ms, Convert.ToSingle(m.Value));
                    break;
                case MPackType.Double:
                    WriteDouble(ms, Convert.ToDouble(m.Value));
                    break;
                case MPackType.Binary:
                    WriteBinary(ms, (byte[])m.Value);
                    break;
                case MPackType.Map:
                    WriteMap(ms, m);
                    break;
                case MPackType.Array:
                    WirteArray(ms, m);
                    break;
                default:
                    WriteNull(ms);
                    break;
            }
        }

        private static void WriteMap(Stream stream, MPack mpack)
        {
            MPackMap map = mpack as MPackMap;
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

                lenBytes = _convert.GetBytes((UInt16)len);
                stream.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDF;
                stream.WriteByte(b);
                lenBytes = _convert.GetBytes((UInt32)len);
                stream.Write(lenBytes, 0, lenBytes.Length);
            }

            foreach (KeyValuePair<MPack, MPack> child in map)
            {
                EncodeToStream(stream, child.Key);
                EncodeToStream(stream, child.Value);
            }
        }
        private static void WirteArray(Stream ms, MPack mpack)
        {
            MPackArray list = mpack as MPackArray;
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
                lenBytes = _convert.GetBytes((UInt16)len);
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDD;
                ms.WriteByte(b);
                lenBytes = _convert.GetBytes((UInt32)len);
                ms.Write(lenBytes, 0, lenBytes.Length);
            }


            for (int i = 0; i < len; i++)
            {
                EncodeToStream(ms, list[i]);
            }
        }
        private static void WriteNull(Stream ms)
        {
            ms.WriteByte(0xC0);
        }
        private static void WriteString(Stream ms, string strVal)
        {
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

            byte[] rawBytes = _encoding.GetBytes(strVal);
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
                lenBytes = _convert.GetBytes(Convert.ToUInt16(len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xDB;
                ms.WriteByte(b);
                lenBytes = _convert.GetBytes(Convert.ToUInt32(len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            ms.Write(rawBytes, 0, rawBytes.Length);
        }
        private static void WriteBinary(Stream ms, byte[] rawBytes)
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
                lenBytes = _convert.GetBytes(Convert.ToUInt16(len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            else
            {
                b = 0xC6;
                ms.WriteByte(b);
                lenBytes = _convert.GetBytes(Convert.ToUInt32(len));
                ms.Write(lenBytes, 0, lenBytes.Length);
            }
            ms.Write(rawBytes, 0, rawBytes.Length);
        }
        private static void WriteDouble(Stream ms, double val)
        {
            ms.WriteByte(0xCB);
            ms.Write(_convert.GetBytes(val), 0, 8);
        }
        private static void WriteSingle(Stream ms, float val)
        {
            ms.WriteByte(0xCA);
            ms.Write(_convert.GetBytes(val), 0, 4);
        }
        private static void WriteBoolean(Stream ms, bool val)
        {
            if (val)
                ms.WriteByte(0xC3);
            else
                ms.WriteByte(0xC2);
        }
        private static void WriteInteger(Stream ms, ulong val)
        {
            ms.WriteByte(0xCF);
            byte[] dataBytes = _convert.GetBytes(val);
            ms.Write(dataBytes, 0, 8);
        }
        private static void WriteInteger(Stream ms, long iVal)
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
                else if (iVal <= 0xFFFF)
                {  //UInt16
                    ms.WriteByte(0xCD);
                    ms.Write(_convert.GetBytes((Int16)iVal), 0, 2);
                }
                else if (iVal <= 0xFFFFFFFF)
                {  //UInt32
                    ms.WriteByte(0xCE);
                    ms.Write(_convert.GetBytes((Int32)iVal), 0, 4);
                }
                else
                {  //UInt64
                    ms.WriteByte(0xD3);
                    ms.Write(_convert.GetBytes(iVal), 0, 8);
                }
            }
            else
            {  // <0
                if (iVal <= Int32.MinValue)  //-2147483648  // 64 bit
                {
                    ms.WriteByte(0xD3);
                    ms.Write(_convert.GetBytes(iVal), 0, 8);
                }
                else if (iVal <= Int16.MinValue)   // -32768    // 32 bit
                {
                    ms.WriteByte(0xD2);
                    ms.Write(_convert.GetBytes((Int32)iVal), 0, 4);
                }
                else if (iVal <= -128)   // -32768    // 16 bit
                {
                    ms.WriteByte(0xD1);
                    ms.Write(_convert.GetBytes((Int16)iVal), 0, 2);
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
