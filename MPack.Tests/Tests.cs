using System;
using System.Linq;
using Xunit;

namespace MPack.Tests
{
    public class Tests
    {
        [Fact]
        public void ReadmeExample()
        {
            var dictionary = new MDict
            {
                {
                    "array1", MToken.From(new[]
                    {
                        "array1_value1",  // implicitly converted string
                        MToken.From("array1_value2"),
                    })
                },
                {"bool1", MToken.From(true)}, //boolean
                {"double1", MToken.From(50.5)}, //single-precision float
                {"double2", MToken.From(15.2)},
                {"int1", 50505}, // implicitly converted integer
                {"int2", MToken.From(50)} // integer
            };

            byte[] encodedBytes = dictionary.EncodeToBytes();
            var reconstructed = MToken.ParseFromBytes(encodedBytes);

            bool bool1 = reconstructed["bool1"].To<bool>();
            var array1 = reconstructed["array1"] as MArray;
            var array1_value1 = array1[0];
            double double1 = reconstructed["double1"].To<double>();

            Assert.True(bool1);
            Assert.Equal("array1_value1", array1_value1.To<string>());
            Assert.Equal(50.5, double1);
        }

        [Fact]
        public void TestDouble()
        {
            var tests = new[]
            {
                0d,
                1d,
                -1d,
                224d,
                256d,
                65530d,
                65540d,
                Double.NaN,
                Double.MaxValue,
                Double.MinValue,
                Double.PositiveInfinity,
                Double.NegativeInfinity
            };
            foreach (var value in tests)
            {
                Assert.Equal(value, MToken.ParseFromBytes(MToken.From(value).EncodeToBytes()).To<double>());
            }
        }

        [Fact]
        public void TestNumRepr()
        {
            Assert.True(Enumerable.SequenceEqual(MToken.From(127).EncodeToBytes(), new byte[] { 0b01111111 }), "127");
            Assert.True(Enumerable.SequenceEqual(MToken.From(0xFF).EncodeToBytes(), new byte[] { 0xCC, 0xFF }), "255");
            Assert.True(Enumerable.SequenceEqual(MToken.From(0x7FFF).EncodeToBytes(), new byte[] { 0xD1, 0x7F, 0xFF }), "0x7FFF");
            Assert.True(Enumerable.SequenceEqual(MToken.From((UInt16)0x7FFF).EncodeToBytes(), new byte[] { 0xCD, 0x7F, 0xFF }), "(UInt16)0x7FFF");
            Assert.True(Enumerable.SequenceEqual(MToken.From(0xFFFF).EncodeToBytes(), new byte[] { 0xCD, 0xFF, 0xFF }), "0xFFFF");
            Assert.True(Enumerable.SequenceEqual(MToken.From(0xFFFFFFFF).EncodeToBytes(), new byte[] { 0xCE, 0xFF, 0xFF, 0xFF, 0xFF }), "0xFFFFFFFF");
            Assert.True(Enumerable.SequenceEqual(((MToken)0xF_FFFF_FFFF).EncodeToBytes(), new byte[] { 0xD3, 0x00, 0x00, 0x00, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF }), "0xF_FFFF_FFFF");
            Assert.True(Enumerable.SequenceEqual(((MToken)(ulong)0xF_FFFF_FFFF).EncodeToBytes(), new byte[] { 0xCF, 0x00, 0x00, 0x00, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF }), "(ulong)0xF_FFFF_FFFF");
            Assert.True(Enumerable.SequenceEqual(MToken.From(0x8000_0000_FFFF_FFFF).EncodeToBytes(), new byte[] { 0xCF, 0x80, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }), "0x8000_0000_FFFF_FFFF");
            Assert.True(Enumerable.SequenceEqual(MToken.From(-32).EncodeToBytes(), new byte[] { 0b11100000 }), "-32");
            Assert.True(Enumerable.SequenceEqual(MToken.From(-33).EncodeToBytes(), new byte[] { 0xD0, 0xDF }), "-33");
            Assert.True(Enumerable.SequenceEqual(MToken.From(-127).EncodeToBytes(), new byte[] { 0xD0, 0x81 }), "-127");
            Assert.True(Enumerable.SequenceEqual(MToken.From(-128).EncodeToBytes(), new byte[] { 0xD1, 0xFF, 0x80 }), "-128");
        }

        [Fact]
        public void TestNull()
        {
            Assert.Equal(null, MToken.ParseFromBytes(MToken.Null().EncodeToBytes()).To<object>());
        }

        [Fact]
        public void TestString()
        {
            var tests = new string[]
            {
                Helpers.GetString(2),
                Helpers.GetString(8),
                Helpers.GetString(16),
                Helpers.GetString(32),
                Helpers.GetString(257),
                Helpers.GetString(65537)
            };
            foreach (var value in tests)
            {
                Assert.Equal(value, MToken.ParseFromBytes(MToken.From(value).EncodeToBytes()).To<string>());
            }
        }

        [Fact]
        public void TestInteger()
        {
            var tests = new[]
            {
                0,
                1,
                -1,
                sbyte.MinValue,
                sbyte.MaxValue,
                byte.MaxValue,
                short.MinValue,
                short.MaxValue,
                int.MinValue,
                int.MaxValue,
                long.MaxValue,
                long.MinValue,
            };
            foreach (var value in tests)
            {
                Assert.Equal(value, MToken.ParseFromBytes(MToken.From(value).EncodeToBytes()).To<long>());
            }
        }

        [Fact]
        public void TestMap()
        {
            MDict dictionary = new MDict
            {
                {
                    "array1", MToken.From(new[]
                    {
                        MToken.From("array1_value1"),
                        MToken.From("array1_value2"),
                        MToken.From("array1_value3"),
                    })
                },
                {"bool1", MToken.From(true)},
                {"double1", MToken.From(50.5)},
                {"double2", MToken.From(15.2)},
                {"int1", MToken.From(50505)},
                {"int2", MToken.From(50)},
                {3.14, MToken.From(3.14)},
                {42, MToken.From(42)}
            };

            var bytes = dictionary.EncodeToBytes();
            var result = MToken.ParseFromBytes(bytes) as MDict;
            Assert.Equal(dictionary, result);
        }

        [Fact]
        public void TestArray()
        {
            var tests = new[]
            {
                (float) 0,
                (float) 50505,
                Single.NaN,
                Single.MaxValue,
                Single.MinValue,
                Single.PositiveInfinity,
                Single.NegativeInfinity,
                Single.Epsilon,
            }.Select(f => MToken.From(f))
            .ToArray();

            var arr = new MArray(tests);
            var bytes = arr.EncodeToBytes();
            var round = MToken.ParseFromBytes(bytes) as MArray;

            Assert.True(round != null);
            Assert.True(arr.Count == round.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                Assert.Equal(arr[i], round[i]);
            }
            Assert.Equal(arr, round);
        }

        [Fact]
        public void TestUInt64()
        {
            var tests = new[]
            {
                UInt64.MaxValue,
                UInt64.MinValue,
            };
            foreach (var value in tests)
            {
                Assert.Equal(value, MToken.ParseFromBytes(MToken.From(value).EncodeToBytes()).To<ulong>());
            }
        }

        [Fact]
        public void TestBoolean()
        {
            var tru = MToken.ParseFromBytes(MToken.From(true).EncodeToBytes()).To<bool>();
            var fal = MToken.ParseFromBytes(MToken.From(false).EncodeToBytes()).To<bool>();
            Assert.True(tru);
            Assert.False(fal);
        }

        [Fact]
        public void TestSingle()
        {
            var tests = new[]
            {
                (float)0,
                (float)50505,
                Single.NaN,
                Single.MaxValue,
                Single.MinValue,
                Single.PositiveInfinity,
                Single.NegativeInfinity,
                Single.Epsilon,
            };
            foreach (var value in tests)
            {
                Assert.Equal(value, MToken.ParseFromBytes(MToken.From(value).EncodeToBytes()).To<float>());
            }
        }

        [Fact]
        public void TestBinary()
        {
            var tests = new[]
            {
                Helpers.GetBytes(8),
                Helpers.GetBytes(16),
                Helpers.GetBytes(32),
                Helpers.GetBytes(257),
                Helpers.GetBytes(65537)
            };
            foreach (var value in tests)
            {
                var result = MToken.ParseFromBytes(MToken.From(value).EncodeToBytes()).To<byte[]>();
                Assert.True(Enumerable.SequenceEqual(value, result));
            }
        }
    }
}
