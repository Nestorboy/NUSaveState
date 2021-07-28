
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CallThis : UdonSharpBehaviour
{
    public Texture2D[] textureArray;
    public Texture2D textureVariable;

    private void Start()
    {
        UdonBehaviour udon = (UdonBehaviour)(Component)this;

        Debug.Log(string.Format("<b>U#</b> Before: {0}\nType: {1}", udon.GetProgramVariable(nameof(textureVariable)), udon.GetProgramVariableType(nameof(textureVariable))));
        textureVariable = textureArray[0];
        Debug.Log(string.Format("<b>U#</b> After: {0}\nType: {1}", udon.GetProgramVariable(nameof(textureVariable)), udon.GetProgramVariableType(nameof(textureVariable))));
    }

    public void EventName()
    {
        Debug.Log("Event recieved.");
    }
}
