using RivetSim.Core.Logging;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.World;

namespace RivetSim.Simulation.Mission.Executors
{
    /// <summary>
    /// 全部主体（S1～S8）执行器的注册入口，以及逐钉循环内阶段未恢复时的收尾处理（清除残留故障、本颗记为无效）。
    /// </summary>
    public static class MissionExecutorCatalog
    {
        /// <summary>注册 S1～S8 的全部执行器。</summary>
        public static void RegisterAll(ExecutorRegistry registry, MissionServices s)
        {
            NavExecutors.Register(registry, s);
            VisionExecutors.Register(registry, s);
            ArmExecutors.Register(registry, s);
            AcqExecutors.Register(registry, s);
            UtExecutors.Register(registry, s);
            SafetyExecutors.Register(registry, s);
            SchedExecutors.Register(registry, s);
            DataExecutors.Register(registry, s);
        }

        /// <summary>本颗铆钉的某阶段未恢复：清除注入的物理故障，未保存记录时写入一条“无效”记录。</summary>
        public static void HandleUnrecovered(MissionServices s, MissionCursor cursor, string phaseCode, string reason)
        {
            s.Contact.SurfaceOffsetFaultM = 0f;
            s.Contact.CouplingFaultFactor = 1f;
            Rivet rivet = s.Rivet(cursor);
            MissionBlackboard b = s.Board;
            if (b.RecordSaved) return;
            InspectionRecord record = s.Inspection.CommitUnrecovered(rivet, s.Station(cursor).StationId, $"{phaseCode} 未恢复（{reason}）");
            b.RecordSaved = true;
            b.CommittedRecord = record;
            SimLog.Warning(LogCategory.UT, $"{rivet.RivetId}：记为无效（{record.Reason}）");
        }
    }
}
