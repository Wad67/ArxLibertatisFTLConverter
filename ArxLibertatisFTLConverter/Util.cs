﻿using System.IO;

namespace ArxLibertatisFTLConverter
{
    public static class Util
    {
        public static string GetParentWithName(string dirPath, string name)
        {
            DirectoryInfo di = new DirectoryInfo(dirPath);
            while (true)
            {
                if (di.Name == name)
                {
                    return di.FullName;
                }
                di = di.Parent;
                if (di == null)
                {
                    return null;
                }
            }
        }
    }
}
