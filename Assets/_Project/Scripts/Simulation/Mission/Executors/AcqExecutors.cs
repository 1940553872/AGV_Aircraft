using System.Threading;
using RivetSim.Core.Logging;
using RivetSim.Core.TaskMatrix;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// S4 贴合采集执行器（acq.*）：t(5,4) 接触采集并判定 C2～C8，t(6,4) 释放确认并判定 C9～C12（按 JSON conditionRefs 逐项，阈值取 JSON conditions）。
    /// 接触力、耦合与 A 扫均为教学简化模型。
    /// </summary>
    public static class AcqExecutors
    {
        /// <summary>注册本主体的执行器。</summary>
        public static void Register(ExecutorRegistry registry, MissionServices s)
        {
            registry.Register("acq.contactAcquire", new DelegateExecutor((cell, cursor, ct) => ContactAcquireAsync(s, cell, cursor, ct)));
            s.RegisterLogic(registry, "acq.releaseConfirm", a => ReleaseConfirm(s, a.Cell, a.Cursor));
        }

        private static async Awaitable<ExecutorResult> ContactAcquireAsync(MissionServices s, CellDef cell, MissionCursor cursor, CancellationToken ct)
        {
            Rivet rivet = s.Rivet(cursor);
            if (s.Conditions.Consume(FaultKind.AcquisitionInvalid))
            {
                s.Contact.CouplingFaultFactor = s.Settings.faultCouplingFactor;
                SimLog.Warning(LogCategory.UT, $"[故障注入] 采集无效：{rivet.RivetId} 耦合剂不足（耦合质量 × {s.Settings.faultCouplingFactor:0.00}）");
            }
            ArmMotionResult r = await s.Inspection.AcquireAsync(rivet, ct);
            if (!r.Success) return ExecutorResult.Fail(r.FailureReason);
            AcquisitionSample sample = s.Inspection.LastAcquisition;
            int passed = 0;
            foreach (string id in cell.ConditionRefs)
                if (CheckAcquisition(s, cell.Id, id, rivet, sample)) passed++;
            return ExecutorResult.Done($"耦合质量 {sample.Coupling:0.00}；{Range(cell)} 满足 {passed}/{cell.ConditionRefs.Count} 项");
        }

        private static ExecutorResult ReleaseConfirm(MissionServices s, CellDef cell, MissionCursor cursor)
        {
            Rivet rivet = s.Rivet(cursor);
            int passed = 0;
            foreach (string id in cell.ConditionRefs)
                if (CheckRelease(s, cell.Id, id)) passed++;
            s.Board.ReleaseConditionsPassed = passed;
            s.Board.ReleaseConditionsChecked = cell.ConditionRefs.Count;
            return ExecutorResult.Done($"{rivet.RivetId} 释放确认：{Range(cell)} 满足 {passed}/{cell.ConditionRefs.Count} 项");
        }

        private static bool CheckAcquisition(MissionServices s, string cellId, string id, Rivet rivet, AcquisitionSample sample)
        {
            ConditionEvaluator e = s.Evaluator;
            Pose tcp = s.Arm.TcpPoseWorld;
            switch (id)
            {
                case "C2":
                {
                    bool ok = s.Chassis.IsZeroSpeedLocked && s.Board.ArmPermit;
                    return e.Report(cellId, id, ok, ok ? 1f : 0f, $"零速锁定{(s.Chassis.IsZeroSpeedLocked ? "有效" : "无效")}，机械臂许可{(s.Board.ArmPermit ? "有效" : "无效")}");
                }
                case "C3":
                {
                    float deg = Vector3.Angle(tcp.rotation * Vector3.forward, -rivet.Normal);
                    return e.Report(cellId, id, deg <= e.Threshold(id, 0), deg, $"{deg:0.00}°");
                }
                case "C4":
                {
                    float mm = Vector3.ProjectOnPlane(tcp.position - rivet.PositionM, rivet.Normal).magnitude * 1000f;
                    return e.Report(cellId, id, mm <= e.Threshold(id, 0), mm, $"{mm:0.00} mm");
                }
                case "C5":
                {
                    float min = Mathf.Min(sample.ForceStartN, sample.ForceEndN), max = Mathf.Max(sample.ForceStartN, sample.ForceEndN);
                    bool ok = min >= e.Threshold(id, 0) && max <= e.Threshold(id, 1) && sample.DurationS >= e.Threshold(id, 2);
                    return e.Report(cellId, id, ok, sample.ForceEndN, $"{min:0.0}～{max:0.0} N，持续 {sample.DurationS:0.00} s");
                }
                case "C6":
                {
                    float fsh = sample.Gates.InterfaceAmplitude;
                    return e.Report(cellId, id, fsh >= e.Threshold(id, 0), fsh, $"界面回波 {fsh:P0} FSH");
                }
                case "C7":
                    return e.Report(cellId, id, s.Board.RecipeLoaded, s.Board.RecipeLoaded ? 1f : 0f, s.Board.RecipeLoaded ? "已载入" : "未载入");
                case "C8":
                {
                    float errMm = Mathf.Abs(BackwallThicknessMm(s, sample) - s.InspectionConfig.skinThicknessMm);
                    return e.Report(cellId, id, errMm <= e.Threshold(id, 0), errMm, $"厚度误差 {errMm:0.00} mm");
                }
                default:
                    return e.Report(cellId, id, false, 0f, "无判定实现");
            }
        }

        private static bool CheckRelease(MissionServices s, string cellId, string id)
        {
            ConditionEvaluator e = s.Evaluator;
            MissionBlackboard b = s.Board;
            switch (id)
            {
                case "C9":
                    return e.Report(cellId, id, b.RecordSaved, b.RecordSaved ? 1f : 0f, b.RecordSaved ? "波形、位姿、时间戳已保存" : "未保存");
                case "C10":
                    return e.Report(cellId, id, b.ConclusionReady, b.ConclusionReady ? 1f : 0f,
                        b.ConclusionReady && b.CommittedRecord != null ? $"结论：{RivetInspectionService.Label(b.CommittedRecord.Verdict)}" : "无结论");
                case "C11":
                {
                    float retreatMm = b.RetreatM * 1000f;
                    bool ok = s.Contact.ForceN <= e.Threshold(id, 0) && retreatMm >= e.Threshold(id, 1);
                    return e.Report(cellId, id, ok, retreatMm, $"接触力 {s.Contact.ForceN:0.0} N，回退 {retreatMm:0} mm");
                }
                case "C12":
                {
                    float mm = b.SurfaceDistanceAfterRetreatM * 1000f;
                    return e.Report(cellId, id, mm >= e.Threshold(id, 0), mm, $"距表面 {mm:0} mm");
                }
                default:
                    return e.Report(cellId, id, false, 0f, "无判定实现");
            }
        }

        // 由 A 扫包络估计首次底面回波声时对应的厚度：底面闸门内峰值与界面闸门内峰值的时差 × 声速 / 2。
        private static float BackwallThicknessMm(MissionServices s, AcquisitionSample sample)
        {
            AScanWaveform w = sample.Waveform;
            GateResult g = sample.Gates;
            float[] env = GateJudge.Envelope(w, s.InspectionConfig.probeFrequencyMHz, out _);
            float tInterface = PeakTimeUs(env, w, g.GateAStartUs, g.GateAEndUs);
            float tBackwall = PeakTimeUs(env, w, g.BackwallStartUs, g.BackwallEndUs);
            const float MmPerMPerUs = 1e-3f; // m/s × µs = 1e-6 m = 1e-3 mm
            return (tBackwall - tInterface) * s.InspectionConfig.longitudinalVelocityMps * MmPerMPerUs * 0.5f;
        }

        private static float PeakTimeUs(float[] env, AScanWaveform w, float startUs, float endUs)
        {
            int a = w.IndexAt(startUs), b = w.IndexAt(endUs), best = a;
            for (int i = a; i <= b; i++)
                if (env[i] > env[best]) best = i;
            return w.TimeUs(best);
        }

        private static string Range(CellDef cell) =>
            cell.ConditionRefs.Count == 0 ? "无条件" : $"{cell.ConditionRefs[0]}～{cell.ConditionRefs[cell.ConditionRefs.Count - 1]}";
    }
}
