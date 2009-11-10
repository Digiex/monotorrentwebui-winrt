using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

namespace MonoTorrent.WebUI.Server.Utility
{
    static class PasswordHelper
    {
        /// <summary>
        /// Random (pseudorandom) number generator, used to generate password salts.
        /// </summary>
        private static Random random = new Random();

        public static bool IsPasswordMatch(string candidate, string storedPassword)
        {
            string[] hashParts = storedPassword.Split('$');

            if (hashParts.Length < 3)
                return false;

            string hashAlgName = hashParts[0];
            string salt = hashParts[1];
            string passHex = hashParts[2];

            HashAlgorithm hashAlg = HashAlgorithm.Create(hashAlgName);
            if (hashAlg == null)
                throw new ApplicationException(
                    String.Format("Stored password uses an unknown hash algorithm \"{0}\"", hashAlgName)
                    );

            byte[] saltedBytes = Encoding.Unicode.GetBytes(
                String.Concat(salt, candidate)
                );
            byte[] candHash = hashAlg.ComputeHash(saltedBytes);
            string candHex = BitConverter.ToString(candHash);

            return String.Equals(candHex, passHex, StringComparison.OrdinalIgnoreCase);
        }

        public static object ToStorageFormat(string clearPassword, string HashAlgorithmName)
        {
            HashAlgorithm hashAlg = HashAlgorithm.Create(HashAlgorithmName);

            // salt is a positive integet 10-100 million
            int salt = random.Next(10000000, 99999999);
            byte[] saltedBytes = Encoding.Unicode.GetBytes(
                String.Concat(salt, clearPassword)
                );
            byte[] passwHash = hashAlg.ComputeHash(saltedBytes);

            string hexHash = BitConverter.ToString(passwHash);

            // stored as "hashAlgorithm$salt$saltPlusPassHash"
            return String.Concat(HashAlgorithmName, "$", salt, "$", passwHash);
        }

        public static void HashAlgorithmNameValidator(object value)
        {
            string strValue = Convert.ToString(value);

            if (HashAlgorithm.Create(strValue) == null)
                throw new ArgumentException(
                    String.Format("Unknown hash algorithm \"{0}\".", strValue)
                    );
        }
    }
}
