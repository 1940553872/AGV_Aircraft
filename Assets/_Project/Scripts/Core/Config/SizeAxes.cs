using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 尺寸向量轴约定（ADR-008）。配置中的尺寸向量一律按（长, 宽, 高）存放在 (x, y, z) 分量，
    /// 转为 Unity 缩放时按物体类型映射：
    /// 场地与桌类（长沿左右）→ (X = 长, Y = 高, Z = 宽)；车辆与车载设备（长沿前后）→ (X = 宽, Y = 高, Z = 长)。
    /// </summary>
    public static class SizeAxes
    {
        /// <summary>长沿本地 X（大厅：世界 X；演示台：桌面左右方向）。</summary>
        public static Vector3 LengthAlongX(Vector3 lengthWidthHeightM)
        {
            return new Vector3(lengthWidthHeightM.x, lengthWidthHeightM.z, lengthWidthHeightM.y);
        }

        /// <summary>长沿本地 Z（车辆前后方向，如 AGV 车体与车载设备盒）。</summary>
        public static Vector3 LengthAlongZ(Vector3 lengthWidthHeightM)
        {
            return new Vector3(lengthWidthHeightM.y, lengthWidthHeightM.z, lengthWidthHeightM.x);
        }
    }
}
