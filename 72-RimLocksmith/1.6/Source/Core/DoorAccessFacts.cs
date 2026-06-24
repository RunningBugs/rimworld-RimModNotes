namespace RunningBugs.RimLocksmith.Core;

public readonly struct DoorAccessFacts
{
    public DoorAccessFacts(bool isColonyDoor, bool isOpenOrFreePassage, bool roamerCanOpen = false)
    {
        IsColonyDoor = isColonyDoor;
        IsOpenOrFreePassage = isOpenOrFreePassage;
        RoamerCanOpen = roamerCanOpen;
    }

    public bool IsColonyDoor { get; }
    public bool IsOpenOrFreePassage { get; }
    public bool RoamerCanOpen { get; }
}
