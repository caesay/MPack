using System;
using Xunit;

namespace MPack.Tests
{
    public class TupleTests
    {
        [Fact]
        public void ValueTupleInDictionarySerializesAsArray()
        {
            var dictionary = new MDict
            {
                { "tuple", MToken.From((42, "answer")) }
            };

            var roundTrip = Assert.IsType<MDict>(MToken.ParseFromBytes(dictionary.EncodeToBytes()));
            var tuple = Assert.IsType<MArray>(roundTrip["tuple"]);

            Assert.Equal(2, tuple.Count);
            Assert.Equal(42, tuple[0].To<int>());
            Assert.Equal("answer", tuple[1].To<string>());
        }

        [Fact]
        public void TupleInArraySerializesAsArray()
        {
            var array = Assert.IsType<MArray>(MToken.From(new object[]
            {
                Tuple.Create("answer", 42)
            }));

            var roundTrip = Assert.IsType<MArray>(MToken.ParseFromBytes(array.EncodeToBytes()));
            var tuple = Assert.IsType<MArray>(roundTrip[0]);

            Assert.Equal(2, tuple.Count);
            Assert.Equal("answer", tuple[0].To<string>());
            Assert.Equal(42, tuple[1].To<int>());
        }

        [Fact]
        public void ValueTupleRoundTripsToClrType()
        {
            var expected = (Number: 42, Text: "answer");
            var token = MToken.ParseFromBytes(MToken.From(expected).EncodeToBytes());

            var actual = token.To<(int Number, string Text)>();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TupleRoundTripsToClrType()
        {
            var expected = Tuple.Create("answer", 42);
            var token = MToken.ParseFromBytes(MToken.From(expected).EncodeToBytes());

            var actual = token.To<Tuple<string, int>>();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LongValueTupleSerializesAsFlatArrayAndRoundTrips()
        {
            var expected = (1, 2, 3, 4, 5, 6, 7, 8);
            var tuple = Assert.IsType<MArray>(MToken.From(expected));

            Assert.Equal(8, tuple.Count);
            for (var index = 0; index < tuple.Count; index++)
            {
                Assert.Equal(index + 1, tuple[index].To<int>());
            }

            var actual = MToken.ParseFromBytes(tuple.EncodeToBytes()).To<(int, int, int, int, int, int, int, int)>();
            Assert.Equal(expected, actual);
        }
    }
}
