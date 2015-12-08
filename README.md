MPack   ![Build status](https://ci.appveyor.com/api/projects/status/84jv0lllniqsicpb?svg=true)](https://ci.appveyor.com/project/caesay/mpack)
=====
This library is a lightweight implementation of the [MessagePack](http://msgpack.org/) binary serialization format. MessagePack is a 1-to-1 binary representation of JSON, and the official specification can be found here: [https://github.com/msgpack...](https://github.com/msgpack/msgpack/blob/master/spec.md).


Notes
-----
This library is contained in one file, with the designed purpose of being a drop-in file to your solution instead of being an added dependancy.

Its easiest to understand how this library works if you think in terms of json. The type `MPackMap` represents a dictionary, and the type `MPackArray` represents an array. Everything else can be created with the `MPack` type static methods (such as `MPack.FromString(string)`).

NuGet
-----
MPack is now available as a NuGet package!
```
PM> Install-Package MPack
```

Usage
-----
Create a object model that can be represented as MsgPack. Here we are creating a dictionary, but really it can be anything:
```csharp
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
```
Serialize the data to a byte array or to a stream to be saved, transmitted, etc:
```csharp
byte[] encodedBytes = dictionary.EncodeToBytes();
// -- or --
dictionary.EncodeToStream(stream);
```
Parse the binary data back into a MPack object model:
```csharp
var reconstructed = MPack.ParseFromBytes(encodedBytes) as MPackMap;
// -- or --
var reconstructed = MPack.ParseFromStream(stream) as MPackMap;
```
Turn MPack objects back into types that we understand with the generic `To<>()` method. Since we know the types of everything here we can just call `To<bool>()` to reconstruct our bool, but if you don't know you can access the instance enum `MPack.ValueType` to know what kind of value it is:
```csharp
bool bool1 = reconstructed["bool1"].To<bool>();
var array1 = reconstructed["array1"] as MPackArray;
var array1_value1 = array1[0];
double double1 = reconstructed["double1"].To<double>();
//etc...
```

Credits
-------
The following people/projects have made this possible:

0. Me, obviously :) (caelantsayler]at[gmail]dot[com)
0. ymofen (ymofen]at[diocp]dot[org) for his work on https://github.com/ymofen/SimpleMsgPack.Net
0. All of the people that make MessagePack happen https://github.com/msgpack