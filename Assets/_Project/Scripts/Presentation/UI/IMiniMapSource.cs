using System.Collections.Generic;
using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 小地图数据源（M6）：栅格贴图、世界范围、AGV 位姿与规划路径；M8 增加工位、充电位与铆钉结果点（虚拟禁行区画进栅格贴图）。
    /// 主场景取自 RobotRuntime，Sandbox_UI 用假数据。
    /// </summary>
    public interface IMiniMapSource
    {
        /// <summary>栅格贴图（像素 (0,0) 对应世界范围左下角），可为空。</summary>
        Texture2D GridTexture { get; }

        /// <summary>贴图覆盖的世界范围（水平面 x / z），单位 m。</summary>
        Rect WorldRectM { get; }

        /// <summary>当前 AGV 位姿。</summary>
        PlanarPose AgvPose { get; }

        /// <summary>当前规划路径（世界水平面坐标，单位 m）。</summary>
        IReadOnlyList<Vector2> PathPoints { get; }

        /// <summary>路径版本号（路径变化时递增）。</summary>
        int PathRevision { get; }

        /// <summary>检测工位停靠位姿（M8）。</summary>
        IReadOnlyList<PlanarPose> StationPoses { get; }

        /// <summary>充电位位姿（M8）。</summary>
        PlanarPose ChargePose { get; }

        /// <summary>铆钉水平位置（世界 x / z，单位 m，M8）。</summary>
        IReadOnlyList<Vector2> RivetPointsM { get; }

        /// <summary>铆钉结果，与 <see cref="RivetPointsM"/> 一一对应：−1 未检测，否则为 InspectionVerdict 的整数值（M8）。</summary>
        IReadOnlyList<int> RivetResults { get; }

        /// <summary>标记版本号（铆钉结果变化时递增，M8）。</summary>
        int MarksRevision { get; }

        /// <summary>每帧由入口调用一次：按间隔刷新栅格贴图。参数为实际时间步长，单位 s。</summary>
        void Refresh(float realDeltaS);
    }
}
