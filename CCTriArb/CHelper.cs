using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace CCTriArb
{
    public static class CHelper
    {
        public static String GetTimestamp(this DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssfff");
        }

        public static String GetTimestamp()
        {
            return GetTimestamp(DateTime.Now);
        }

        public static DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp);
        }

        public static double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalMilliseconds);
        }

        public static double ConvertToUnixTimestamp()
        {
            return ConvertToUnixTimestamp(DateTime.Now);
        }


        public static String Base64Encode(String plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static String Base64Decode(String base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static String hmacsha256(String key, String message)
        {
            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            byte[] keyByte = encoding.GetBytes(key);
            byte[] messageBytes = encoding.GetBytes(message);
            HMACSHA256 hmacsha256 = new HMACSHA256(keyByte);
            byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
            String hashMessageString = encoding.GetString(hashmessage);
            return hashMessageString;
        }

        public static string ComputeSignature(String secret, String timeStamp, String method, String url, String body)
        {
            byte[] data = Convert.FromBase64String(secret);
            var prehash = timeStamp + method.ToUpper() + url + body;
            return HashString(prehash, data);
        }

        public static string HashString(string str, byte[] secret)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            using (var hmac = new HMACSHA256(secret))
            {
                byte[] hash = hmac.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

    }
}
