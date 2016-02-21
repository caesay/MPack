using System;
using System.Linq;
using NUnit.Framework;

namespace CS.MPackTests
{
    [TestFixture()]
    public class Tests
    {
        [Test()]
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
                Assert.AreEqual(value, MPack.ParseFromBytes(MPack.From(value).EncodeToBytes()).To<double>());
            }
        }

        [Test()]
        public void TestNull()
        {
            Assert.AreEqual(null, MPack.ParseFromBytes(MPack.Null().EncodeToBytes()).To<object>());
        }
        [Test()]
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
                Assert.AreEqual(value, MPack.ParseFromBytes(MPack.From(value).EncodeToBytes()).To<string>());
            }
        }
        [Test()]
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
                Assert.AreEqual(value, MPack.ParseFromBytes(MPack.From(value).EncodeToBytes()).To<long>());
            }
        }
        [Test()]
        public void TestMap()
        {
            MPackMap dictionary = new MPackMap
            {
                {
                    "array1", MPack.From(new[]
                    {
                        MPack.From("array1_value1"),
                        MPack.From("array1_value2"),
                        MPack.From("array1_value3"),
                    })
                },
                {"bool1", MPack.From(true)},
                {"double1", MPack.From(50.5)},
                {"double2", MPack.From(15.2)},
                {"int1", MPack.From(50505)},
                {"int2", MPack.From(50)},
                {3.14, MPack.From(3.14)},
                {42, MPack.From(42)}
            };
            
            var bytes = dictionary.EncodeToBytes();
            var result = MPack.ParseFromBytes(bytes) as MPackMap;
            Assert.AreEqual(dictionary, result);
        }
        [Test()]
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
            }.Select(f => MPack.From(f))
            .ToArray();

            var arr = new MPackArray(tests);
            var bytes = arr.EncodeToBytes();
            var round = MPack.ParseFromBytes(bytes) as MPackArray;

            Assert.IsTrue(round != null);
            Assert.IsTrue(arr.Count == round.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                Assert.AreEqual(arr[i], round[i]);
            }
            Assert.AreEqual(arr, round);
        }
        [Test()]
        public void TestUInt64()
        {
            var tests = new[]
            {
                UInt64.MaxValue,
                UInt64.MinValue,
            };
            foreach (var value in tests)
            {
                Assert.AreEqual(value, MPack.ParseFromBytes(MPack.From(value).EncodeToBytes()).To<ulong>());
            }
        }
        [Test()]
        public void TestBoolean()
        {
            var tru = MPack.ParseFromBytes(MPack.From(true).EncodeToBytes()).To<bool>();
            var fal = MPack.ParseFromBytes(MPack.From(false).EncodeToBytes()).To<bool>();
            Assert.IsTrue(tru);
            Assert.IsFalse(fal);
        }
        [Test()]
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
                Assert.AreEqual(value, MPack.ParseFromBytes(MPack.From(value).EncodeToBytes()).To<float>());
            }
        }
        [Test()]
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
                var result = MPack.ParseFromBytes(MPack.From(value).EncodeToBytes()).To<byte[]>();
                Assert.IsTrue(Enumerable.SequenceEqual(value, result));
            }
        }
    }
}
