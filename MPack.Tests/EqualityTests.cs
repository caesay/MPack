using System;
using System.Linq;
using Xunit;

namespace MPack.Tests
{
    public class EqualityTests
    {
        [Fact]
        public void TestDictionaryEquality()
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

            var dictionary2 = new MDict
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

            var dictionary3 = new MDict
            {
                {
                    "array1", MToken.From(new[]
                    {
                        "array1_ value1",  // implicitly converted string
                        MToken.From("array1_value2"),
                    })
                },
                {"bool1", MToken.From(true)}, //boolean
                {"double1", MToken.From(50.5)}, //single-precision float
                {"double2", MToken.From(15.2)},
                {"int1", 50505}, // implicitly converted integer
                {"int2", MToken.From(50)} // integer
            };

            Assert.Equal(dictionary, dictionary2);
            Assert.NotEqual(dictionary, dictionary3);
            Assert.NotEqual(dictionary, MToken.From(50.5));
        }

        [Fact]
        public void TestNumberEquality()
        {
            Assert.Equal(MToken.From(10), MToken.From(10));
            Assert.NotEqual(MToken.From(10), MToken.From(-10));
            Assert.NotEqual(MToken.From(10), -10);
            Assert.Equal(MToken.From(10), 10);
            Assert.Equal(10, MToken.From(10));
            Assert.Equal((long)10, MToken.From(10));
            Assert.Equal(10, MToken.From((long)10));
            Assert.Equal(MToken.From((int)10), MToken.From((double)10));
            Assert.Equal(MToken.From((decimal)10.12345), MToken.From((double)10.12345));
            Assert.NotEqual(MToken.From((decimal)10.12345), MToken.From((double)10.1245));
            Assert.Equal(MToken.From((object)10.12345d), MToken.From((object)10.12345m));
        }

        [Fact]
        public void TestStringEquality()
        {
            Assert.Equal(MToken.From("hello!"), MToken.From("hello!"));
            Assert.NotEqual(MToken.From(10), MToken.From("hello!"));
        }

        [Fact]
        public void TestStringCompare()
        {
            var arr = new MToken[]
            {
                "a",
                9,
                "z",
                "!",
                1,
                "b",
            };

            Array.Sort(arr);
            Assert.Equal(1, arr[0]);
            Assert.Equal(9, arr[1]);
            Assert.Equal("!", arr[2]);
            Assert.Equal("a", arr[3]);
            Assert.Equal("b", arr[4]);
            Assert.Equal("z", arr[5]);
        }

        [Fact]
        public void TestBoolNullEquality()
        {
            Assert.Equal(MToken.From(true), MToken.From(true));
            Assert.NotEqual(MToken.From(true), MToken.From(false));
            Assert.NotEqual(MToken.From(true), MToken.From(null));
            Assert.Equal(MToken.From(false), MToken.From(false));
            Assert.NotEqual(MToken.From(false), MToken.From(null));
            Assert.Equal(MToken.From(null), MToken.From(null));
            Assert.NotEqual(MToken.From(null), MToken.From(10));
            Assert.NotEqual(MToken.From(null), MToken.From("asd"));
        }

        [Fact]
        public void TestArrayEquality()
        {
            var tests = new object[]
            {
                (float) 0,
                (float) 50505,
                1234.92d,
                800,
                (object)99656,
                "hello!",
                new[] {1, 2, 3, 4, 5},
            };

            var m1 = tests.Select(f => MToken.From(f)).ToArray();
            var m2 = tests.Select(f => MToken.From(f)).ToArray();
            tests[1] = 0;
            var m3 = tests.Select(f => MToken.From(f)).ToArray();

            Assert.Equal(m1, m2);
            Assert.NotEqual(m1, m3);
        }
    }
}
