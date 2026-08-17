using System;
using System.IO;

namespace AlbionDataAvalonia
{
    public static class AppData
    {
        public static string LocalPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrimsSilver");

        public static string DataDirectoryPath => Path.Combine(LocalPath, "data");

        public static string DatabasePath => Path.Combine(DataDirectoryPath, "trimssilver.db");

        public static string BackupDirectoryPath => Path.Combine(LocalPath, "backups");
    }
}
