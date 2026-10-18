using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RivetSim.Editor.Importers
{
    /// <summary>
    /// UR 机械臂 yaml（default_kinematics / joint_limits / visual_parameters）的最小读取器：只支持“键: 值”的缩进嵌套，
    /// 键路径用点号连接（如 kinematics.shoulder.z）；数值可带 <c>!degrees</c> 标记。不引入第三方 yaml 库。
    /// </summary>
    public sealed class UrYamlReader
    {
        private const string DegreesTag = "!degrees";
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>全部键路径。</summary>
        public IEnumerable<string> Keys => _values.Keys;

        /// <summary>读取文件。</summary>
        public static UrYamlReader Load(string path) => Parse(File.ReadAllText(path));

        /// <summary>解析文本。</summary>
        public static UrYamlReader Parse(string text)
        {
            var reader = new UrYamlReader();
            var stack = new List<(int Indent, string Key)>();
            foreach (string rawLine in text.Split('\n'))
            {
                string line = StripComment(rawLine).TrimEnd('\r', ' ', '\t');
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("-")) continue;
                int indent = line.Length - line.TrimStart().Length;
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string key = line.Substring(indent, colon - indent).Trim();
                string value = line.Substring(colon + 1).Trim();
                while (stack.Count > 0 && stack[stack.Count - 1].Indent >= indent) stack.RemoveAt(stack.Count - 1);
                string path = stack.Count == 0 ? key : $"{string.Join(".", stack.ConvertAll(s => s.Key))}.{key}";
                if (value.Length == 0) stack.Add((indent, key));
                else reader._values[path] = value;
            }
            return reader;
        }

        /// <summary>是否存在键路径。</summary>
        public bool Has(string path) => _values.ContainsKey(path);

        /// <summary>读取原始字符串。</summary>
        public string GetString(string path) =>
            _values.TryGetValue(path, out string v) ? v : throw new KeyNotFoundException($"yaml 缺少键：{path}");

        /// <summary>读取角度并换算为弧度（带 !degrees 标记时按度解读，否则按弧度）。</summary>
        public float GetRadians(string path)
        {
            string v = GetString(path);
            return IsDegrees(v) ? ParseNumber(v) * Mathf.Deg2Rad : ParseNumber(v);
        }

        /// <summary>读取角度并换算为度（带 !degrees 标记时按度解读，否则按弧度）。</summary>
        public float GetDegrees(string path)
        {
            string v = GetString(path);
            return IsDegrees(v) ? ParseNumber(v) : ParseNumber(v) * Mathf.Rad2Deg;
        }

        /// <summary>读取普通数值（长度、质量等）。</summary>
        public float GetNumber(string path) => ParseNumber(GetString(path));

        private static bool IsDegrees(string v) => v.StartsWith(DegreesTag, StringComparison.Ordinal);

        private static float ParseNumber(string v)
        {
            string s = IsDegrees(v) ? v.Substring(DegreesTag.Length).Trim() : v;
            return (float)double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string StripComment(string line)
        {
            int hash = line.IndexOf('#');
            return hash < 0 ? line : line.Substring(0, hash);
        }
    }
}
