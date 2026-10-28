using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pidataexport
{
    public static class AppSettings
    {
        public static UserOptions Options { get; private set; }

        public static void Load()
        {
            try
            {
                string folder = GetConfigFolder();
                string file = Path.Combine(folder, "user_options.json");

                if (!File.Exists(file))
                {
                    Options = new UserOptions
                    {
                        DefaultExportFolder = "",
                        AutoOpenExcelAfterExport = false,
                        MinimizeEFByDefault = true,
                        DefaultWidth = 800,
                        DefaultHeight = 800
                    };

                    return;
                }

                var json = File.ReadAllText(file);
                Options = JsonConvert.DeserializeObject<UserOptions>(json);
            }
            catch
            {
                Options = new UserOptions();
            }
        }

        public static void Save()
        {
            try
            {
                string folder = GetConfigFolder();
                string file = Path.Combine(folder, "user_options.json");

                var json = JsonConvert.SerializeObject(Options, Formatting.Indented);

                File.WriteAllText(file, json);
            }
            catch
            {
            }
        }

        private static string GetConfigFolder()
        {
            string exeFolder = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);

            string configFolder = Path.Combine(exeFolder, "config");
            Directory.CreateDirectory(configFolder);

            return configFolder;
        }
    }
}
