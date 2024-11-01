using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileHora_server
{
    internal static class Storage
    {
        static Storage()
        {
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "storage/"))
            {
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "storage/");
            }
        }
        public static string GetLoc(string file)
        {
            return AppDomain.CurrentDomain.BaseDirectory + $"storage/{file}";
        }

        public static bool FileExists(string filename)
        {
            return File.Exists(GetLoc(filename));
        }

        public static byte[] GetFileBytes(string filename)
        {
            return OpenFile(GetLoc(filename));
        }

        public static string[] GetFilesInStorage()
        {
            return Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "/storage");
        }

        public static bool AddFile(string filename, byte[] data)
        {
            // if (File.Exists(GetLoc(filename))) return false;

            using var ss = File.Create(GetLoc(filename));
            ss.Write(data);
            return true;
        }

        public static void AppendToFile(string filename, byte[] data, int size)
        {
            using(var fs = new FileStream(GetLoc(filename), FileMode.Append, FileAccess.Write))
            {
                fs.Write(data, 0, size);
            }
        }

        public static void DeleteFile(string filename)
        {
            string path = GetLoc(filename);
            if(File.Exists(path)) { 
                File.Delete(path);
            }
        }

        private static byte[] OpenFile(string filepath)
        {
            using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            long numB = new FileInfo(filepath).Length;

            return br.ReadBytes((int)numB);
        }

    }
}
