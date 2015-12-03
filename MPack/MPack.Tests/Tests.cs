using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MPack.Tests;
using MsgPack;

namespace MsgPack.Tests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestDouble()
        {
            var tests = new[]
            {
                (double)0,
                Double.NaN,
                Double.MaxValue,
                Double.MinValue,
                Double.PositiveInfinity,
                Double.NegativeInfinity
            };
            foreach (var value in tests)
            {
                Assert.AreEqual(value, MPack.ParseBytes(MPack.FromDouble(value).EncodeToBytes()).To<double>());
            }
        }

        [TestMethod]
        public void TestNull()
        {
            Assert.AreEqual(null, MPack.ParseBytes(MPack.Null().EncodeToBytes()).To<object>());
        }
        [TestMethod]
        public void TestString()
        {
            var tests = new string[]
            {
                Helpers.GetString(8),
                Helpers.GetString(16),
                Helpers.GetString(32),
                Helpers.GetString(257),
                Helpers.GetString(65537)
            };
            foreach (var value in tests)
            {
                Assert.AreEqual(value, MPack.ParseBytes(MPack.FromString(value).EncodeToBytes()).To<string>());
            }
        }
        [TestMethod]
        public void TestInteger()
        {
            var tests = new[]
            {
                0,
                int.MinValue,
                int.MaxValue,
                long.MaxValue,
                long.MinValue,
            };
            foreach (var value in tests)
            {
                Assert.AreEqual((long)value, MPack.ParseBytes(MPack.FromInteger(value).EncodeToBytes()).To<long>());
            }
        }
        [TestMethod]
        public void TestMap()
        {
            MPackMap dictionary = new MPackMap
            {
                {
                    "array1", MPack.FromArray(new[]
                    {
                        MPack.FromString("array1_value1"),
                        MPack.FromString("array1_value2"),
                        MPack.FromString("array1_value3"),
                    })
                },
                {"bool1", MPack.FromBool(true)},
                {"double1", MPack.FromDouble(50.5)},
                {"double2", MPack.FromDouble(15.2)},
                {"int1", MPack.FromInteger(50505)},
                {"int2", MPack.FromInteger(50)}
            };

            var bytes = dictionary.EncodeToBytes();
            var result = MPack.ParseBytes(bytes) as MPackMap;
            Assert.AreEqual(dictionary, result);
        }
        [TestMethod]
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
            }.Select(f => MPack.FromSingle(f))
            .ToArray();

            var arr = new MPackArray(tests);
            var bytes = arr.EncodeToBytes();
            var round = MPack.ParseBytes(bytes) as MPackArray;

            Assert.IsTrue(round != null);
            Assert.IsTrue(arr.Count == round.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                Assert.AreEqual(arr[i], round[i]);
            }
            Assert.AreEqual(arr, round);
        }
        [TestMethod]
        public void TestUInt64()
        {
            var tests = new[]
            {
                UInt64.MaxValue,
                UInt64.MinValue,
            };
            foreach (var value in tests)
            {
                Assert.AreEqual(value, MPack.ParseBytes(MPack.FromInteger(value).EncodeToBytes()).To<ulong>());
            }
        }
        [TestMethod]
        public void TestBoolean()
        {
            var tru = MPack.ParseBytes(MPack.FromBool(true).EncodeToBytes()).To<bool>();
            var fal = MPack.ParseBytes(MPack.FromBool(false).EncodeToBytes()).To<bool>();
            Assert.IsTrue(tru);
            Assert.IsFalse(fal);
        }
        [TestMethod]
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
                Assert.AreEqual(value, MPack.ParseBytes(MPack.FromSingle(value).EncodeToBytes()).To<float>());
            }
        }
        [TestMethod]
        public void TestDateTime()
        {
            var now = DateTime.Now;
            var result = MPack.ParseBytes(MPack.FromDateTime(now).EncodeToBytes());
            var result_dt = result.To<DateTime>();
            Assert.AreEqual(now, result_dt);

            //the below can not be true i think because the MsgPack spec has no way to 
            //differentiate a datetime from an integer.
            //Assert.IsTrue(result.ValueType == MsgPackType.DateTime);
        }
        [TestMethod]
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
                var result = MPack.ParseBytes(MPack.FromBytes(value).EncodeToBytes()).To<byte[]>();
                Assert.IsTrue(Enumerable.SequenceEqual(value, result));
            }
        }
    }
}
