﻿namespace UE4View.UE4
{
    public class FEngineVersion : USerializable
    {
        public ushort Major;
        public ushort Minor;
        public ushort Patch;
        public uint Changelist;
        public string Branch;

        public override FArchive Serialize(FArchive reader)
        {
            Major = reader.ToUInt16();
            Minor = reader.ToUInt16();
            Patch = reader.ToUInt16();
            Changelist = reader.ToUInt32();
            Branch = reader.ToFString();
            return reader;
        }

        public void Set(ushort Major, ushort Minor, ushort Patch, uint Changelist, string Branch)
        {
            this.Major = Major;
            this.Minor = Minor;
            this.Patch = Patch;
            this.Changelist = Changelist;
            this.Branch = Branch;
        }
    }
}
