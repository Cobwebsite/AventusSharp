using System;
using System.Security.Cryptography;
using System.Text;

namespace AventusSharp.Data.Storage.Mysql.Tools
{
    internal static class Utils
    {
        public static string CheckConstraint(string constraint)
        {
            if (constraint.Length > 64) // 128 mssql / 64 mysql
            {
                // replace random by 64 char hash
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    return GetHash(sha256Hash, constraint);
                }

                // string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                // return new string(Enumerable.Repeat(chars, 64).Select(s => s[random.Next(s.Length)]).ToArray());
            }
            return constraint;
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
