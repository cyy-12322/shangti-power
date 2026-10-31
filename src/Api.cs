using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace ShangtiPower
{
    /// <summary>访问校园安心付（axinfu.sus.edu.cn:8082）的接口客户端。</summary>
    public class ApiClient
    {
        private const string BaseUrl = "https://axinfu.sus.edu.cn:8082/electric";
        private const string UserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 " +
            "(KHTML, like Gecko) Mobile/15E148 MicroMessenger/8.0.40";

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        // ---------------------------------------------------------------- 底层请求
        private string Post(string path, string form, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            req.UserAgent = UserAgent;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.Headers["X-Requested-With"] = "XMLHttpRequest";
            req.Accept = "application/json, text/javascript, */*; q=0.01";
            req.KeepAlive = false;

            byte[] body = Encoding.UTF8.GetBytes(form == null ? "" : form);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream())
            {
                s.Write(body, 0, body.Length);
            }
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private static string Form(params string[] kv)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kv[i] ?? ""));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kv[i + 1] ?? ""));
            }
            return sb.ToString();
        }

        private Dictionary<string, object> ParseRoot(string json)
        {
            Dictionary<string, object> root = _json.Deserialize<Dictionary<string, object>>(json);
            if (root == null) throw new Exception("接口返回为空");
            object code;
            if (root.TryGetValue("respCode", out code) && Convert.ToString(code) != "00")
                throw new Exception("接口返回异常: " + Convert.ToString(code) + " " + GetString(root, "respDesc"));
            return root;
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return Convert.ToString(v);
            return "";
        }

        private static List<Dictionary<string, object>> GetObjectList(Dictionary<string, object> root, string key)
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            object raw;
            if (!root.TryGetValue(key, out raw) || raw == null) return list;
            object[] arr = raw as object[];
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    Dictionary<string, object> d = arr[i] as Dictionary<string, object>;
                    if (d != null) list.Add(d);
                }
                return list;
            }
            // 某些序列化器返回 ArrayList
            System.Collections.IEnumerable en = raw as System.Collections.IEnumerable;
            if (en != null)
            {
                foreach (object o in en)
                {
                    Dictionary<string, object> d = o as Dictionary<string, object>;
                    if (d != null) list.Add(d);
                }
            }
            return list;
        }

        // ---------------------------------------------------------------- 级联
        public List<LevelDef> GetLevels(int timeoutMs)
        {
            string json = Post("/level/getLevels", "", timeoutMs);
            Dictionary<string, object> root = ParseRoot(json);
            List<Dictionary<string, object>> items = GetObjectList(root, "data");
            List<LevelDef> result = new List<LevelDef>();
            for (int i = 0; i < items.Count; i++)
            {
                Dictionary<string, object> d = items[i];
                LevelDef lv = new LevelDef();
                lv.Rank = (int)Convert.ToDouble(GetString(d, "rank"), CultureInfo.InvariantCulture);
                lv.Type = GetString(d, "type");
                lv.Url = GetString(d, "url");
                lv.Remark = GetString(d, "remark");
                lv.End = GetString(d, "end") == "1";
                result.Add(lv);
            }
            result.Sort(delegate(LevelDef a, LevelDef b) { return a.Rank.CompareTo(b.Rank); });
            return result;
        }

        public List<RoomOption> GetOptions(LevelDef level, AppConfig cfg, int timeoutMs)
        {
            string form = Form(
                "areaId", cfg == null ? "" : cfg.CampusId,
                "architectureId", cfg == null ? "" : cfg.BuildingId,
                "floorId", cfg == null ? "" : cfg.FloorId,
                "roomId", cfg == null ? "" : cfg.RoomId);
            string json = Post("/getData" + level.Url, form, timeoutMs);
            Dictionary<string, object> root = ParseRoot(json);
            List<Dictionary<string, object>> items = GetObjectList(root, "data");
            List<RoomOption> opts = new List<RoomOption>();
            for (int i = 0; i < items.Count; i++)
            {
                RoomOption o = new RoomOption();
                o.Key = GetString(items[i], "key");
                object v;
                string val = items[i].TryGetValue("value", out v) && v != null ? Convert.ToString(v) : "";
                o.Name = val.Trim();
                if (o.Name.Length == 0) o.Name = o.Key;
                opts.Add(o);
            }
            return opts;
        }

        // ---------------------------------------------------------------- 查电量
        public PowerResult QueryPower(AppConfig cfg, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string form = Form(
                "areaId", cfg.CampusId,
                "architectureId", cfg.BuildingId,
                "floorId", cfg.FloorId,
                "roomId", cfg.RoomId);
            string json = Post("/getData/getReserveAM", form, timeoutMs);
            Dictionary<string, object> root = ParseRoot(json);

            object dataRaw;
            if (!root.TryGetValue("data", out dataRaw) || dataRaw == null)
                throw new Exception("查不到数据（房间编号可能已变化）");
            Dictionary<string, object> data = dataRaw as Dictionary<string, object>;
            if (data == null) throw new Exception("返回数据格式异常");

            object infoRaw;
            if (data.TryGetValue("resultInfo", out infoRaw) && infoRaw != null)
            {
                Dictionary<string, object> info = infoRaw as Dictionary<string, object>;
                if (info != null && GetString(info, "result") == "3")
                    throw new Exception("有订单正在处理中，请稍后再试");
            }

            string powerText = GetString(data, "remainPower");
            double power;
            if (!double.TryParse(powerText, NumberStyles.Float, CultureInfo.InvariantCulture, out power))
                throw new Exception("无法解析电量：" + powerText);

            string remainName = GetString(data, "remainName");
            string unit = remainName == "du" ? "度" : (remainName == "yuan" ? "元" : remainName);
            string serverTime = "";
            if (infoRaw != null)
            {
                Dictionary<string, object> info = infoRaw as Dictionary<string, object>;
                if (info != null) serverTime = GetString(info, "timeStamp");
            }

            PowerResult r = new PowerResult();
            r.Power = power;
            r.Unit = unit;
            r.ServerTime = serverTime;
            sw.Stop();
            r.ElapsedMs = sw.ElapsedMilliseconds;
            return r;
        }
    }
}
