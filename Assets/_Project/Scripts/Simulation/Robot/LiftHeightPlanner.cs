using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 升降柱目标高度规划：机械臂安装面 = 工位铆钉平均高度 − 舒适偏移（CFG_Arm），再换算为升降柱伸出量并限制在行程内（CFG_Agv）。
    /// 可达性的正式验证在 M2 第三部分（采样逆解）与 M4（正式逆解）完成。
    /// </summary>
    public static class LiftHeightPlanner
    {
        /// <summary>按工位铆钉离地高度列表计算升降柱目标。</summary>
        public static LiftHeightPlan Plan(IReadOnlyList<float> rivetHeightsM, AgvConfig agv, ArmConfig arm)
        {
            if (rivetHeightsM == null || rivetHeightsM.Count == 0)
                throw new ArgumentException("铆钉高度列表不能为空。", nameof(rivetHeightsM));
            if (agv == null) throw new ArgumentNullException(nameof(agv));
            if (arm == null) throw new ArgumentNullException(nameof(arm));

            float sum = 0f;
            for (int i = 0; i < rivetHeightsM.Count; i++) sum += rivetHeightsM[i];
            float mean = sum / rivetHeightsM.Count;

            float idealExtension = agv.ExtensionForMountHeightM(mean - arm.comfortTargetAboveMountM);
            float extension = Mathf.Clamp(idealExtension, 0f, agv.liftStrokeM);
            bool clamped = Mathf.Abs(extension - idealExtension) > agv.liftPositionToleranceM;
            return new LiftHeightPlan(mean, agv.ArmMountHeightM(extension), extension, clamped);
        }
    }
}
