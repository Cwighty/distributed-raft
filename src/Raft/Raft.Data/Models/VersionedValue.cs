﻿namespace Raft.Data.Models;

public class VersionedValue<T>
{
    public long Version { get; set; }
    public T Value { get; set; } = default!;
}
