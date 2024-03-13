using System.Collections.Generic;
using System.Runtime.Serialization;
using Xunit;

namespace MPack.Tests
{
    public class ObjectTests
    {
        [Fact]
        public void TestNoDataContract()
        {
            var person1 = new Person
            {
                FirstName = "John",
                LastName = "Doe",
                Children = new Dictionary<string, Person>
                {
                    {
                        "JANE", new Person
                        {
                            FirstName = "Jane",
                            LastName = "Doe"
                        }
                    },
                    {
                        "JACK", new Person
                        {
                            FirstName = "Jack",
                            LastName = "Doe"
                        }
                    }
                }
            };

            var person2 = new Person
            {
                FirstName = "Jill",
                LastName = "Doe",
                Children = new Dictionary<string, Person>
                {
                    {
                        "LUCY", new Person
                        {
                            FirstName = "Lucy",
                            LastName = "Doe"
                        }
                    },
                    {
                        "NATHAN", new Person
                        {
                            FirstName = "Nathan",
                            LastName = "Doe"
                        }
                    }
                }
            };

            var dto = new Dto1()
            {
                Prop1 = "Prop1",
                Prop2 = 1.1,
                Prop3 = null,
                Prop4 = 999.456,
                Persons = new List<Person>
                {
                    person1,
                    person2
                },
            };
            dto.SetProp5("Prop5");
            dto.SetField3("Field3");
            dto.Field1 = "Field1";

            var arr = new object[] { dto };

            var token = MToken.From(arr) as MArray;
            Assert.Equal(MTokenType.Array, token.ValueType);
            Assert.Single(token);

            var bytes = token.EncodeToBytes();
            var round = MToken.ParseFromBytes(bytes) as MArray;

            Assert.True(round != null);
            Assert.True(token.Count == round.Count);

            var returnArrayObj = round.To<object[]>();
            // in the case where the Type is not known, it will be generic arrays and dictionaries
            Assert.True(returnArrayObj.GetType() == typeof(object[]));
            Assert.True(returnArrayObj[0].GetType() == typeof(Dictionary<object, object>));

            var returnArrayType = round.To<Dto1[]>();
            Assert.True(returnArrayType.GetType() == typeof(Dto1[]));
            var returnDto = returnArrayType[0];

            Assert.Equal(dto.Prop1, returnDto.Prop1);
            Assert.Equal(dto.Prop2, returnDto.Prop2);
            Assert.Equal(dto.Prop3, returnDto.Prop3);
            Assert.Equal(dto.Prop4, returnDto.Prop4);
            Assert.Null(returnDto.GetProp5()); // private property
            Assert.Null(returnDto.GetField3()); // private field
            Assert.Null(returnDto.Field1); // fields are ignored if no data contract
            Assert.Equal(dto.Persons.Count, returnDto.Persons.Count);
            Assert.Equal(dto.Persons[0].FirstName, returnDto.Persons[0].FirstName);
            Assert.Equal(dto.Persons[0].LastName, returnDto.Persons[0].LastName);
            Assert.Equal(dto.Persons[0].Children.Count, returnDto.Persons[0].Children.Count);
            Assert.Equal(dto.Persons[0].Children["JANE"].FirstName, returnDto.Persons[0].Children["JANE"].FirstName);
            Assert.Equal(dto.Persons[0].Children["JANE"].LastName, returnDto.Persons[0].Children["JANE"].LastName);
            Assert.Equal(dto.Persons[0].Children["JACK"].FirstName, returnDto.Persons[0].Children["JACK"].FirstName);
            Assert.Equal(dto.Persons[0].Children["JACK"].LastName, returnDto.Persons[0].Children["JACK"].LastName);
            Assert.Equal(dto.Persons[1].FirstName, returnDto.Persons[1].FirstName);
            Assert.Equal(dto.Persons[1].LastName, returnDto.Persons[1].LastName);
            Assert.Equal(dto.Persons[1].Children.Count, returnDto.Persons[1].Children.Count);
            Assert.Equal(dto.Persons[1].Children["LUCY"].FirstName, returnDto.Persons[1].Children["LUCY"].FirstName);
            Assert.Equal(dto.Persons[1].Children["LUCY"].LastName, returnDto.Persons[1].Children["LUCY"].LastName);
            Assert.Equal(dto.Persons[1].Children["NATHAN"].FirstName, returnDto.Persons[1].Children["NATHAN"].FirstName);
            Assert.Equal(dto.Persons[1].Children["NATHAN"].LastName, returnDto.Persons[1].Children["NATHAN"].LastName);


            var nextDto = round[0].To<Dto1>();
            Assert.Equal(dto.Prop1, nextDto.Prop1);
            Assert.Equal(dto.Prop2, nextDto.Prop2);
        }

        [Fact]
        public void TestWithContract()
        {
            var person1 = new Person2
            {
                FirstName = "John",
                LastName = "Doe",
                Children = new Dictionary<string, Person2>
                {
                    {
                        "JANE", new Person2
                        {
                            FirstName = "Jane",
                            LastName = "Doe"
                        }
                    },
                    {
                        "JACK", new Person2
                        {
                            FirstName = "Jack",
                            LastName = "Doe"
                        }
                    }
                }
            };

            var person2 = new Person2
            {
                FirstName = "Jill",
                LastName = "Doe",
                Children = new Dictionary<string, Person2>
                {
                    {
                        "LUCY", new Person2
                        {
                            FirstName = "Lucy",
                            LastName = "Doe"
                        }
                    },
                    {
                        "NATHAN", new Person2
                        {
                            FirstName = "Nathan",
                            LastName = "Doe"
                        }
                    }
                }
            };

            var dto = new Dto2()
            {
                Prop1 = "Prop1",
                Prop2 = 1.1,
                Prop3 = "hello!",
                Prop4 = 999.456,
                Persons = new List<Person2>
                {
                    person1,
                    person2
                },
            };
            dto.SetProp5("Prop5");
            dto.SetField3("Field3");
            dto.Field1 = "Field1";

            var arr = new object[] { dto };

            var token = MToken.From(arr) as MArray;
            Assert.Equal(MTokenType.Array, token.ValueType);
            Assert.Single(token);

            var bytes = token.EncodeToBytes();
            var round = MToken.ParseFromBytes(bytes) as MArray;

            Assert.True(round != null);
            Assert.True(token.Count == round.Count);

            var returnArrayObj = round.To<object[]>();
            // in the case where the Type is not known, it will be generic arrays and dictionaries
            Assert.True(returnArrayObj.GetType() == typeof(object[]));
            Assert.True(returnArrayObj[0].GetType() == typeof(Dictionary<object, object>));

            var returnArrayType = round.To<Dto2[]>();
            Assert.True(returnArrayType.GetType() == typeof(Dto2[]));
            var returnDto = returnArrayType[0];

            Assert.Equal(dto.Prop1, returnDto.Prop1);
            Assert.Equal(dto.Prop2, returnDto.Prop2);
            Assert.Null(returnDto.Prop3); // no DataMember
            Assert.Null(returnDto.Prop4); // no DataMember
            Assert.Equal(dto.GetProp5(), returnDto.GetProp5());
            Assert.Equal(dto.GetField3(), returnDto.GetField3());
            Assert.Equal(dto.Field1, returnDto.Field1);
            Assert.Equal(dto.Persons.Count, returnDto.Persons.Count);
            Assert.Equal(dto.Persons[0].FirstName, returnDto.Persons[0].FirstName);
            Assert.Null(returnDto.Persons[0].LastName);
            Assert.Equal(dto.Persons[0].Children.Count, returnDto.Persons[0].Children.Count);
            Assert.Equal(dto.Persons[0].Children["JANE"].FirstName, returnDto.Persons[0].Children["JANE"].FirstName);
            Assert.Null(returnDto.Persons[0].Children["JANE"].LastName);
            Assert.Equal(dto.Persons[0].Children["JACK"].FirstName, returnDto.Persons[0].Children["JACK"].FirstName);
            Assert.Null(returnDto.Persons[0].Children["JACK"].LastName);
            Assert.Equal(dto.Persons[1].FirstName, returnDto.Persons[1].FirstName);
            Assert.Null(returnDto.Persons[1].LastName);
            Assert.Equal(dto.Persons[1].Children.Count, returnDto.Persons[1].Children.Count);
            Assert.Equal(dto.Persons[1].Children["LUCY"].FirstName, returnDto.Persons[1].Children["LUCY"].FirstName);
            Assert.Null(returnDto.Persons[1].Children["LUCY"].LastName);
            Assert.Equal(dto.Persons[1].Children["NATHAN"].FirstName, returnDto.Persons[1].Children["NATHAN"].FirstName);
            Assert.Null(returnDto.Persons[1].Children["NATHAN"].LastName);

            var nextDto = round[0].To<Dto2>();
            Assert.Equal(dto.Prop1, nextDto.Prop1);
            Assert.Equal(dto.Prop2, nextDto.Prop2);
        }

        public class Dto1
        {
            public string Prop1 { get; set; }
            public double Prop2 { get; set; }
            public object Prop3 { get; set; }
            public object Prop4 { get; set; }
            private string Prop5 { get; set; }

            public string Field1;
            public readonly string Field2;
            private string Field3;

            public List<Person> Persons { get; set; }

            public string GetField3() => Field3;
            public void SetField3(string value) => Field3 = value;
            public string GetProp5() => Prop5;
            public void SetProp5(string value) => Prop5 = value;
        }

        [DataContract]
        public class Dto2
        {
            [DataMember]
            public string Prop1 { get; set; }
            [DataMember]
            public double Prop2 { get; set; }
            public object Prop3 { get; set; }
            public object Prop4 { get; set; }
            [DataMember]
            private string Prop5 { get; set; }

            [DataMember]
            public string Field1;
            public readonly string Field2;
            [DataMember]
            private string Field3;

            [DataMember]
            public List<Person2> Persons { get; set; }

            public string GetField3() => Field3;
            public void SetField3(string value) => Field3 = value;
            public string GetProp5() => Prop5;
            public void SetProp5(string value) => Prop5 = value;
        }

        public class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public Dictionary<string, Person> Children { get; set; }
        }

        public class Person2
        {
            public string FirstName { get; set; }
            [IgnoreDataMember]
            public string LastName { get; set; }
            public Dictionary<string, Person2> Children { get; set; }
        }
    }
}
