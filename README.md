# NUSaveState

### Description
NUSaveState uses an alternate (and Quest compatible!) method to load and save data for and with Udon, and it's built with both Udon Graph and U# users in mind.
To make the implementation as straight forward as possible there's also a custom inspector for the NUSaveState script, allowing for a quicker workflow, and then there are also several methods or events you can use to easily integrate it into your own worlds.

This system uses a combination between setting player velocities and 'Parameter Drivers' to write to a data avatar's parameters, and the data is then output through the finger bone rotations.
Due to this systems reliance on avatar parameters and floats, there's a limit as to how precise the data can be, and as it stands right now, each avatar is used to store 256 bits, but there's no limit to how many data avatars you can chain together.

### Requirements
- [Udon](https://vrchat.com/home/download)
- [UdonSharp](https://github.com/MerlinVR/UdonSharp)
- [VRCSDK3A.dll](https://vrchat.com/home/download)

**It is incredibly important that you import the `VRCSDK3A.dll` along with the `VRCSDK3A.dll.meta` file which can be found in the 3.0 avatar SDK.
Begin importing any of the VRCSDK3-AVATAR SDKs, but make sure to uncheck everything except for the `VRCSDK/Plugins/VRCSDK3A.dll` and `VRCSDK/Plugins/VRCSDK3A.dll.meta` files.**
