using System;
using System.Security.Cryptography;
using System.Text;

namespace AventusSharp.Data.Storage.Relational
{
    internal static class Utils
    {
        public static string CheckConstraint(string constraint)
        {
            string prefix = "";
            string suffix = "";
            string name = constraint;
            if (constraint.Length >= 2)
            {
                if (constraint[0] == '[' && constraint[^1] == ']')
                {
                    prefix = "[";
                    suffix = "]";
                    name = constraint[1..^1];
                }
                else if ((constraint[0] == '`' && constraint[^1] == '`')
                    || (constraint[0] == '"' && constraint[^1] == '"'))
                {
                    prefix = constraint[0].ToString();
                    suffix = constraint[^1].ToString();
                    name = constraint[1..^1];
                }
            }

            if (name.Length > 64) // 128 mssql / 64 mysql
            {
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    name = GetHash(sha256Hash, name);
                }
            }
            return prefix + name + suffix;
        }

        private static string GetHash(HashAlgorithm hashAlgorithm, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
    }
}
