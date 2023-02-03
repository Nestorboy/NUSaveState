namespace Nessie.Udon.SaveState
{
    public enum ModeEnum
    {
        Saving,
        Loading,
    }
    
    public enum StatusEnum
    {
        Idle,
        Processing,
        Failed,
        Finished,
    }
    
    public enum ProgressState
    {
        WaitingForAvatar,
        Reading,
        Writing,
        Verifying,
        Complete,
    }
}
