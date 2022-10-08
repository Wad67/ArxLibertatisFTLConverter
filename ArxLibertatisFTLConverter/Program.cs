using System;
using System.IO;

namespace ArxLibertatisFTLConverter
{
    internal class Program
    {
        private static void ConvertFile(string file)
        {
            string fileLower = file.ToLowerInvariant();

            if (fileLower.EndsWith(".ftl"))
            {
                //  ConvertFTLToOBJ.Convert(file);
                ConvertFTLtoGLTF2.Convert(file);
            }
            else if (fileLower.EndsWith(".obj"))
            {
                ConvertOBJToFTL.Convert(file);
            }

        }

        private static void Main(string[] args)
        {

            foreach (string path in args)
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine("Can't find file " + path);
                    continue;
                }
                ConvertFile(path);
            }
        }
    }
}
