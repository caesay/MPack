# MPack
This library is a lightweight implementation of the [MessagePack](http://msgpack.org/) binary serialization format. MessagePack is a 1-to-1 binary representation of JSON, and the official specification can be found here: [https://github.com/msgpack...](https://github.com/msgpack/msgpack/blob/master/spec.md).

## Implementation Notes
* This library is designed to be super light weight.
* Its easiest to understand how this library works if you think in terms of json. The type `MDict` represents a dictionary, and the type `MArray` represents an array. 
* Create MPack values with the static method `MToken.From(object);`. You can pass any simple type (such as string, integer, etc), or any Array composed of a simple type. MPack also has implicit conversions from most of the basic types built in.
* Transform an MPack object back into a CLR type with the static method `MToken.To<T>();` or `MToken.To(type);`. MPack also has **explicit** converions going back to most basic types, you can do `string str = (string)mpack;` for instance.
* MPack now supports native asynchrounous reading and cancellation tokens. It will *not* block a thread to wait on a stream.

## NuGet
MPack is available as a [NuGet package](https://www.nuget.org/packages/MPack)!
```
PM> Install-Package MPack
```

## Usage
-----
Create a object model that can be represented as MsgPack. Here we are creating a dictionary, but really it can be anything:
```csharp
using MPack;

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
```
Serialize the data to a byte array or to a stream to be saved, transmitted, etc:
```csharp
byte[] encodedBytes = dictionary.EncodeToBytes();
// -- or --
dictionary.EncodeToStream(stream);
```
Parse the binary data back into a MPack object model (you can also cast back to an MPackMap or MPackArray after reading if you want dictionary/array methods):
```csharp
var reconstructed = MToken.ParseFromBytes(encodedBytes);
// -- or --
var reconstructed = MToken.ParseFromStream(stream);
```
Turn MPack objects back into types that we understand with the generic `To<>()` method. Since we know the types of everything here we can just call `To<bool>()` to reconstruct our bool, but if you don't know you can access the instance enum `MToken.ValueType` to know what kind of value it is:
```csharp
bool bool1 = reconstructed["bool1"].To<bool>();
var array1 = reconstructed["array1"] as MArray;
var array1_value1 = array1[0];
double double1 = reconstructed["double1"].To<double>();
//etc...
```

## Complex Types

Beyond converting basic/primitive types, there is a very basic object serializer built in.

If your object has the `[DataContract]` attribute, all properties/fields with the `[DataMember]` attribute will be serialized.

```csharp
[DataContract]
class MyObject
{
	[DataMember]
	public string Name { get; set; }

	[DataMember]
	public int Age { get; set; }

    // following will NOT be serialized, because missing [DataMember] attribute
	public List<string> Friends { get; set; } 
}

// ...
var token = MToken.From(new MyObject { Name = "John", Age = 25 });
var bytes = token.EncodeToBytes();
var reconstructed = MToken.ParseFromBytes(bytes);
var reconstructedObj = reconstructed.To<MyObject>();
```

If your object does not have the `[DataContract]` attribute, all public properties will be serialized, 
and you can use the `[IgnoreDataMember]` attribute to exclude properties from serialization.

```csharp
class MyObject
{
	public string Name { get; set; }

	public int Age { get; set; }

	[IgnoreDataMember] // will not be serialized
	public List<string> Friends { get; set; }
}
```

## Disclaimer
This is a very basic/simple implementation of the MessagePack format.
This library is not optimized for performance, and the object serialization is very basic. It is not recommended for use in high-performance, 
high-throughput scenarios, or for serializing complex object models. For those scenarios, consider using a more robust library such as
[MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp).