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
    public string IP;
    public ushort Port = 7777;
}