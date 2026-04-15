using System;
using System.Collections.Generic;
[Serializable]
public class SavedServerData
{
    public List<RecentJoinCodeEntry> RecentJoinCodes = new();
}

[Serializable]
public class RecentJoinCodeEntry
{
    public string Name;
    public string Code;
}