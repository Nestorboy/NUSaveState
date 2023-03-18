
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using Nessie.Udon.SaveState;

[AddComponentMenu("")] // Hide the example script from the component menu.
public class NUSaveState_Example_Sharp : UdonSharpBehaviour
{
    // Simple declarations to reflect the Graph equivalents.
    public NUSaveState NUSaveState;
    
    // Saved/Loaded variables.
    private Vector3 PlayerPosition;
    private Quaternion PlayerRotation;

    public void _Save()
    {
        PlayerPosition = Networking.LocalPlayer.GetPosition();
        PlayerRotation = Networking.LocalPlayer.GetRotation();
            
        NUSaveState._SSSave();
    }

    public void _Load()
    {
        NUSaveState._SSLoad();
    }

    #region Callbacks

    public void _SSPostLoad()
    {
        Networking.LocalPlayer.TeleportTo(PlayerPosition, PlayerRotation);
    }

    #endregion Callbacks
}
