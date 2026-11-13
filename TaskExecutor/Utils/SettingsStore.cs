using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaskExecutor.Cache;

namespace TaskExecutor.Utils
{
    public class SettingsStore
    {
        private static readonly string
            DataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private static readonly string Dir = Path.Combine(DataFolder, "TaskExecutor");

        private static readonly object Lock = new();

        private static string GetFilePath(string fileName) => Path.Combine(Dir, fileName);

        // 读取整个文件根对象，文件不存在或损坏时返回空对象
        private static JObject ReadRoot(string file)
        {
            if (!File.Exists(file)) return new JObject();
            try
            {
                return JObject.Parse(File.ReadAllText(file));
            }
            catch (Exception e)
            {
                Console.WriteLine($"读取配置文件失败：{e.Message}，请检查文件是否损坏。");
                return new JObject();
            }
        }
        
        // 写入整个文件根对象（临时文件 + 原子替换）
        private static void WriteRoot(string file, JObject root)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var temporaryPath = file + ".tmp";
                File.WriteAllText(temporaryPath, root.ToString(Formatting.Indented));
                File.Move(temporaryPath, file, true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"保存配置文件失败：{e.Message}，请检查文件是否可写。");
            }
        }

        // 按分组加载
        public static T Load<T>(string fileName, string section) where T : class, new()
        {
            var node = ReadRoot(GetFilePath(fileName))[section];
            return node == null ? new T() : node.ToObject<T>() ?? new T();
        }

        // 按分组保存：只更新该 section，其他 section 原样保留
        public static void Save<T>(string fileName, string section, T value) where T : class
        {
            var file = GetFilePath(fileName);
            lock (Lock)
            {
                var root = ReadRoot(file);
                root[section] = JToken.FromObject(value);
                try
                {
                    Directory.CreateDirectory(Dir);
                    var temporaryPath = file + ".tmp";
                    File.WriteAllText(temporaryPath, root.ToString(Formatting.Indented));
                    File.Move(temporaryPath, file, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"保存配置文件失败：{e.Message}，请检查文件是否可写。");
                }
            }
        }
        
        /// <summary>
        /// 读取 app-config.json 全部 section。
        /// </summary>
        public static JObject ExportAll()
        {
            lock (Lock)
            {
                return ReadRoot(GetFilePath(ConfigSections.FileName));
            }
        }
        
        /// <summary>
        /// 把备份里的 section 逐个写回，文件中其余 section 保留不动。
        /// </summary>
        public static void ImportAll(JObject data)
        {
            var file = GetFilePath(ConfigSections.FileName);
            lock (Lock)
            {
                var root = ReadRoot(file);
                foreach (var section in data.Properties())
                {
                    root[section.Name] = section.Value.DeepClone();
                }
                WriteRoot(file, root);
            }
        }
    }
}