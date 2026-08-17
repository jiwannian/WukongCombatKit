using System;
using System.IO;

namespace WukongCombatKit
{
    public static class ModLog
    {
        private static readonly object LockObj = new object();
        private static readonly string LogPath = Path.Combine("CSharpLoader", "Mods", "WukongCombatKit", "WukongCombatKit.log");

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Debug(string message)
        {
            if (ConfigStore.Current.DebugLog)
            {
                Write("DEBUG", message);
            }
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (LockObj)
                {
                    string line = string.Format("[{0:HH:mm:ss}] [{1}] {2}", DateTime.Now, level, message);
                    Console.WriteLine(line);
                    try
                    {
                        string directory = Path.GetDirectoryName(LogPath);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.AppendAllText(LogPath, line + Environment.NewLine);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
