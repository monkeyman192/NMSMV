using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;


namespace NbCore.Utils
{
    public static class StringUtils
    {
        //Parse a string of n and terminate if it is a null terminated string
        public static String read_string(BinaryReader br, int n)
        {
            string s = "";
            bool exit = true;
            long off = br.BaseStream.Position;
            while (exit)
            {
                Char c = br.ReadChar();
                if (c == 0)
                    exit = false;
                else
                    s += c;
            }

            br.BaseStream.Seek(off + 0x80, SeekOrigin.Begin);
            return s;
        }
    }

    public static class FileUtils
    {
        //Check files
        public static bool compareFileSizes(string filepath1, string filepath2)
        {
            if (!File.Exists(filepath1)) return false;
            if (!File.Exists(filepath2)) return false;

            var f1 = new FileStream(filepath1, FileMode.Open);
            var f2 = new FileStream(filepath2, FileMode.Open);
            var l1 = f1.Length;
            var l2 = f2.Length;
            f1.Close();
            f2.Close();

            if (l1 != l2) return false;

            return true;
        }

        public static bool compareFilesHash(string filepath1, string filepath2)
        {
            using (var reader1 = new System.IO.FileStream(filepath1, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                using (var reader2 = new System.IO.FileStream(filepath2, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    byte[] hash1;
                    byte[] hash2;

                    using (var md51 = new System.Security.Cryptography.MD5CryptoServiceProvider())
                    {
                        md51.ComputeHash(reader1);
                        hash1 = md51.Hash;
                    }

                    using (var md52 = new System.Security.Cryptography.MD5CryptoServiceProvider())
                    {
                        md52.ComputeHash(reader2);
                        hash2 = md52.Hash;
                    }

                    int j = 0;
                    for (j = 0; j < hash1.Length; j++)
                    {
                        if (hash1[j] != hash2[j])
                        {
                            break;
                        }
                    }

                    if (j == hash1.Length)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        

        //Endianess Manipulators
        public static uint swapEndianess(uint val)
        {
            uint temp = val & 0xFF;
            return (temp << 8) | ((val >> 8) & 0xFF);
        }

        public static bool IsFileReady(string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        

    }
}
