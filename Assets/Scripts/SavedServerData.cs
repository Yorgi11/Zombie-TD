using System;
using System.Collections.Generic;

[Serializable]
public class SavedServerData
{
    public List<SavedServerEntry> Servers = new();
}

[Serializable]
public class SavedServerEntry
{
    public string Name;
    public string IP;
    public ushort Port = 7777;
}

[Serializable]
public class ServerStatusRequest
{
    public string Type = "status";
}

[Serializable]
public class ServerStatusResponse
{
    public string Name;
    public int CurrentPlayers;
    public int MaxPlayers;
}