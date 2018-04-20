using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace B1SimplificationInterface
{
    public class Settings
    {
        string Path = AppDomain.CurrentDomain.BaseDirectory + "B1ISimplicationInterfaceSettings.ini";

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public string getB1Username()
        {
            return Read("B1 Username", "General");
        }

        public void setB1Username(string username)
        {
            Write("B1 Username", username, "General");
        }
        public string getB1Password()
        {
            return Read("B1 Password", "General");
        }
        public string getFilepath()
        {
            string filepath = Read("Filepath", "Item Cost");
            if (string.IsNullOrWhiteSpace(filepath))
            {
                filepath = AppDomain.CurrentDomain.BaseDirectory;
                setFilepath(filepath);
            }
            return filepath;
        }

        public void setFilepath(string filepath)
        {
            Write("Filepath", filepath, "Item Cost");
        }
        public void setB1Password(string password)
        {
            Write("B1 Password", password, "General");
        }

        public string getRproHostAddress()
        {
            string hostAddress = Read("Rpro Host", "General");
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                hostAddress = "localhost";
                setRproHostAddress(hostAddress);
            }
            return hostAddress;
        }

        public void setRproHostAddress(string address)
        {
            Write("Rpro Host", address, "General");
        }

        public string getB1HostAddress()
        {
            string hostAddress = Read("B1 IP Address", "General");
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                hostAddress = "localhost";
                setRproHostAddress(hostAddress);
            }
            return hostAddress;
        }

        public void setB1HostAddress(string address)
        {
            Write("B1 IP Address", address, "General");
        }

        public string getDays(MainController.Features feature)
        {
            string days = Read(feature.ToString() + " DAYS", feature.ToString());
            if (string.IsNullOrWhiteSpace(days))
            {
                days = "10";
                setDays(feature, days);
            }
            return days;
        }

        public void setItemCostCMDInstruction(string instruction)
        {
            Write("CMD", instruction, "Item Cost");
        }

        public string getItemCostCMDInstruction()
        {
            return Read("CMD", "Item Cost");
        }

        public void setECM(string ECM)
        {
            Write("ECM", ECM, "Item Cost");
        }

        public string getECM()
        {
            return Read("ECM", "Item Cost");
        }

        public string getSubsidiaries(MainController.Features feature)
        {
            string days = Read(feature.ToString() + " SUBSIDIARIES", feature.ToString());
            if (string.IsNullOrWhiteSpace(days))
            {
                days = "";
                setDays(feature, days);
            }
            return days;
        }

        public void setSubsidiaries(MainController.Features feature, string subsidiaries)
        {
            Write(feature.ToString() + " SUBSIDIARIES", subsidiaries, feature.ToString());
        }

        public void setDays(MainController.Features feature, string days)
        {
            Write(feature.ToString() + " DAYS", days, feature.ToString());
        }

        private string Read(string Key, string Section = null)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section ?? Path, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        private void Write(string Key, string Value, string Section = null)
        {
            WritePrivateProfileString(Section ?? Path, Key, Value, Path);
        }

        public void DeleteKey(string Key, string Section = null)
        {
            Write(Key, null, Section ?? Path);
        }

        public void DeleteSection(string Section = null)
        {
            Write(null, null, Section ?? Path);
        }

        public bool KeyExists(string Key, string Section = null)
        {
            return Read(Key, Section).Length > 0;
        }



        public string getEmailSetting(object section)
        {
            string setting = Read(section.ToString(), "EMAIL");
            if (section.ToString() == "EMAIL_ENABLE_SSL" && string.IsNullOrWhiteSpace(setting))
            {
                setting = true.ToString();
                setEmailSetting(section, setting);
            }
            return setting;
        }

        public void setEmailSetting(object section, string value)
        {
            Write(section.ToString(), value, "EMAIL");
        }
    }
}
