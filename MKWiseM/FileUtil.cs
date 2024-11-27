using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKWiseM
{
    internal static class FileUtil
    {
        public static event EventHandler<int> FileProgressChanged;
        public static string CreateVersion(DataRowView view)
        {
            var todayPrefix = DateTime.Now.ToString("yyyy-MM-dd");
            var version = $"{todayPrefix}-1";
            var selectedVersion = view["version"]?.ToString();


            if (string.IsNullOrEmpty(selectedVersion)) return version;
            if (selectedVersion.StartsWith(todayPrefix))
            {
                var vCode = selectedVersion.Split('-').Last();
                if (int.TryParse(vCode, out int newVCode))
                {
                    return $"{todayPrefix}-{newVCode + 1}";
                }
            }

            return version;
        }

        public static byte[] ToRawData(string filepath)
        {
            byte[] buffer;
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"File not found: \n\nPath: {filepath}");

            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {
                var fLength = (int) fs.Length;
                using (var br = new BinaryReader(fs))
                {
                    buffer = br.ReadBytes(fLength);
                }
            }

            return buffer;
        }

        public static async Task<byte[]> ToRawdataAsync(string filepath)
        {
            byte[] buffer;
            if (!File.Exists(filepath))
                throw new FileNotFoundException($"FILE NOT FOUND\n\nPath: {filepath}");
            FileProgressChanged?.Invoke(null, 50);

            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                var fLength = (int) fs.Length;
                buffer = new byte[fLength];
                
                var rResult = await fs.ReadAsync(buffer, 0, fLength);
                if (rResult != fLength)
                    throw new IOException("Failed to read.");
            }

            FileProgressChanged?.Invoke(null, 100);
            return buffer;
        }

        public static async Task<bool> DownloadRawData(string filepath, string query)
        {
            try
            {
                byte[] rawData = null;
                FileProgressChanged?.Invoke(null, 50);
                await Task.Run(() =>
                {
                    rawData = DBUtil.ExecuteScalar(query) as byte[];
                });
                if (rawData == null) return false;
                    

                using (var fs = new FileStream(
                           filepath, FileMode.Create, 
                           FileAccess.Write, FileShare.None, 
                           4096, true))
                {
                    await fs.WriteAsync(rawData, 0, rawData.Length);
                }

                return true;
            }
            finally
            {
                FileProgressChanged?.Invoke(null, 100);
            }
        }

        public static int ByteToMB(byte[] bytes)
        {
            return bytes.Length / (1024 * 1024);
        }
    }
}
