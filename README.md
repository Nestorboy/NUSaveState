# NUSaveState

### Description
NUSaveState uses an alternate (and Quest compatible!) method to load and save data for and with Udon, and it's built with both Udon Graph and U# users in mind.
To make the implementation as straight forward as possible there's also a custom inspector for the NUSaveState script, allowing for a quicker workflow, and then there are also several methods or events you can use to easily integrate it into your own worlds.

This system uses a combination between setting player velocities and 'Parameter Drivers' to write to a data avatar's parameters, and the data is then output through the finger bone rotations. You can chain together multiple avatars if you'd like to save and load different sets of data shared between different worlds, like one avatar for global settings and one for player stats.

As it stands right now, the upper limit of an avatar is 65536 bits or 8192 bytes, however, saving this much data might bloat avatar or world sizes. Each avatar parameter stores two bytes and an avatar can output 32 bytes per frame. The writing process is able to push 3 bytes each frame, which then take an additional 9 frames to get copied to the intermediate buffers, and if validation is succesful, the data gets pushed to the final buffers after 2 more frames.

### Requirements
- [Udon](https://vrchat.com/home/download)
- [UdonSharp](https://github.com/vrchat-community/UdonSharp)
- [VRCSDK3A.dll](https://vrchat.com/home/download)

**It is incredibly important that you import the `VRCSDK3A.dll` along with the `VRCSDK3A.dll.meta` file which can be found in the 3.0 avatar SDK.
Begin importing any of the VRCSDK3-AVATAR SDKs, but make sure to uncheck everything except for the `VRCSDK/Plugins/VRCSDK3A.dll` and `VRCSDK/Plugins/VRCSDK3A.dll.meta` files.**
