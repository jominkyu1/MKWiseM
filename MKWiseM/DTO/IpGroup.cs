using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MKWiseM.Properties;

namespace MKWiseM.DTO
{
    internal class IpGroup
    {
        public IpGroup(string IP, string ID, string Password)
        {
            this.IP = IP;
            this.ID = ID;
            this.Password = Password;
        }

        public string IP { get; private set; }
        public string ID { get; private set; }
        public string Password { get; private set; }

        public static List<IpGroup> GetIpGroups(StringCollection strCol)
        {
            var list = new List<IpGroup>();
            if (strCol.Count == 0)
                return list;

            foreach (string rawStr in strCol)
            {
                string decryptedStr = EncryptUtil.Decrypt(rawStr);
                string[] split = decryptedStr.Split(';');
                if (split.Length > 2)
                {
                    string ip = split[0];
                    string id = split[1];
                    string pw = split[2];

                    list.Add(new IpGroup(ip, id, pw));
                }
                else
                {
                    if (string.IsNullOrEmpty(split.FirstOrDefault()))
                        continue;
                    list.Add(new IpGroup(split.FirstOrDefault(), string.Empty, string.Empty));
                }
            }

            return list;
        }
    }
}
