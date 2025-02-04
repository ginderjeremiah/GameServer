﻿namespace Game.Api
{
    public enum EChangeType
    {
        Add = 0,
        Edit = 1,
        Delete = 2
    }

    public enum ESocketCloseReason
    {
        Finished = 0,
        Inactivity = 1,
        SocketReplaced = 2,
        MessageTooBig = 3
    }
}
