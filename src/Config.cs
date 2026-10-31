using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ShangtiPower
{
    /// <summary>房间配置（首次运行向导写入，之后自动读取）。</summary>
    public class AppConfig
    {
        public string CampusId { get; set; }
        public string CampusName { get; set; }
        public string BuildingId { get; set; }
        public string BuildingName { get; set; }
        public string FloorId { get; set; }
        public string FloorName { get; set; }
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string DisplayName { get; set; }
        public double LowThreshold { get; set; }
        public bool AutoStart { get; set; }

        public AppConfig()
        {
            LowThreshold = 20;
            AutoStart = true;
        }

        public bool IsComplete()
        {
            return !string.IsNullOrEmpty(CampusId)
                && !string.IsNullOrEmpty(BuildingId)
                && !string.IsNullOrEmpty(FloorId)
                && !string.IsNullOrEmpty(RoomId);
        }

        public string BuildDisplayName()
        {
            string s = Trim(CampusName) + " · " + Trim(BuildingName) + " · " + Trim(FloorName);
            if (!string.IsNullOrEmpty(Trim(RoomName))) s += " · " + Trim(RoomName);
            return s;
        }

        public static string Trim(string s)
        {
            return (s == null) ? "" : s.Trim();
        }

        public void Save()
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            File.WriteAllText(Program.ConfigPath, ser.Serialize(this), Encoding.UTF8);
            Program.Log("保存配置: " + DisplayName);
        }

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(Program.ConfigPath)) return null;
                string json = File.ReadAllText(Program.ConfigPath, Encoding.UTF8);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                return ser.Deserialize<AppConfig>(json);
            }
            catch (Exception ex)
            {
                Program.Log("读取配置失败: " + ex.Message);
                return null;
            }
        }
    }

    public class LevelDef
    {
        public int Rank { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public string Remark { get; set; }
        public bool End { get; set; }
    }

    public class RoomOption
    {
        public string Key { get; set; }
        public string Name { get; set; }

        public override string ToString() { return Name; }
    }

    public class PowerResult
    {
        public double Power { get; set; }
        public string Unit { get; set; }
        public string ServerTime { get; set; }
        public long ElapsedMs { get; set; }
    }
}
