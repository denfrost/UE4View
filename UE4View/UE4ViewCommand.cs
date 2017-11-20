﻿using FarNet;
using System.IO;
using System.Linq;
using UE4View.UE4.Pak;
using UE4View.UE4.UAsset;
using UE4View.UE4.VorbisBank;

namespace UE4View
{
    // Demo tool with an item in the plugin menu.
    [System.Runtime.InteropServices.Guid("dc1d3a1f-22da-4aae-954b-cf07f971cf00")]
    [ModuleCommand(Name = "UE4 Viewer", Prefix = "UE4View")]
    public class UE4ViewCommand : ModuleCommand
    {
        private int GetCookedAssetVersion()
        {
            var dir = Path.Combine(Far.Api.CurrentDirectory, "CookedIniVersion.txt");
            if (File.Exists(dir))
            {
                var FileContent = File.ReadAllLines(dir);
                var verline = FileContent?.Where(line => line.Contains("VersionedIniParams=PackageFileVersions")).SingleOrDefault();
                if (verline != null)
                    return int.Parse(verline.Split(':')[1]);
            }
            return 0;
        }
        public override void Invoke(object sender, ModuleCommandEventArgs e)
        {
            var filename = e.Command;
            if (filename.EndsWith(".pak"))
            {
                new PakExplorer(new FPakFile(filename)).OpenPanel();
            }
            else if (filename.EndsWith(".bnk"))
            {
                new BankExplorer(new BankFile(File.ReadAllBytes(filename))).OpenPanel();
            }
            else if (filename.EndsWith(".uasset"))
            {
            }
        }
    }
}
