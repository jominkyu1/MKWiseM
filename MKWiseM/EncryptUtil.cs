using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace MKWiseM
{
    public class EncryptUtil
    {
        public static string Encrypt(string rawData)
        {
            byte[] data = Encoding.UTF8.GetBytes(rawData);
            byte[] encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

            // 암호화된 Binary를 UTF-8로 변환 후 리턴시 데이터 손실 가능성
            // Binary -> Text 표현시 Base64 사용

            // raw string -> UTF8 -> binary -> Encrypt -> Base64 -> Base64 string
            return Convert.ToBase64String(encryptedData);
        }

        public static string Decrypt(string encryptedData)
        {
            try
            {
                byte[] data = Convert.FromBase64String(encryptedData);
                byte[] decryptedData = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);

                // 암호화의 역순
                // Encrypted Base64 string -> Base64 -> binary -> Decrypt -> UTF-8 -> raw string
                return Encoding.UTF8.GetString(decryptedData);
            }
            catch (FormatException)
            {
                //이미 평문일경우 FormatException 발생
                return encryptedData;
            }
        }
    }
}
