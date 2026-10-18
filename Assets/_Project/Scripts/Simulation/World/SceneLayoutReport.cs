using System.Collections.Generic;
using System.Text;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 场景布局校验结果：错误列表与关键间距（最窄通道、AGV 到机身最小距离等），单位 m。
    /// </summary>
    public sealed class SceneLayoutReport
    {
        /// <summary>校验错误（为空表示布局合法）。</summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>布局是否合法。</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>最窄通道宽度（障碍物之间、障碍物与墙之间的最小净距），单位 m。</summary>
        public float NarrowestPassageM { get; internal set; } = float.PositiveInfinity;

        /// <summary>最窄通道位置说明。</summary>
        public string NarrowestPassageLabel { get; internal set; } = "";

        /// <summary>AGV 车宽，单位 m。</summary>
        public float AgvWidthM { get; internal set; }

        /// <summary>巡游路线上 AGV 外轮廓到机身投影的最小距离，单位 m。</summary>
        public float RouteToFuselageM { get; internal set; } = float.PositiveInfinity;

        /// <summary>巡游路线上 AGV 外轮廓到模型机包围盒（含机翼）的最小距离，单位 m。</summary>
        public float RouteToAircraftBoundsM { get; internal set; } = float.PositiveInfinity;

        /// <summary>巡游路线上 AGV 外轮廓到障碍物的最小距离，单位 m。</summary>
        public float RouteToObstacleM { get; internal set; } = float.PositiveInfinity;

        /// <summary>模型机外轮廓到障碍物的最小距离（作业通道），单位 m。</summary>
        public float AircraftToObstacleM { get; internal set; } = float.PositiveInfinity;

        /// <summary>模型机外轮廓到作业区边界的最小距离，单位 m。</summary>
        public float AircraftToWorkZoneEdgeM { get; internal set; } = float.PositiveInfinity;

        /// <summary>停靠时 AGV 底盘到机身的设计距离（取自 CFG_Agv），单位 m。</summary>
        public float DockingFuselageClearanceM { get; internal set; }

        /// <summary>巡游路线总长，单位 m。</summary>
        public float RouteLengthM { get; internal set; }

        /// <summary>充电位到作业区边界的距离，单位 m。</summary>
        public float ChargingDockToWorkZoneM { get; internal set; }

        /// <summary>演示台前缘到演示区边界的观众站位进深，单位 m。</summary>
        public float DemoAudienceDepthM { get; internal set; }

        /// <summary>生成多行中文摘要。</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append($"布局校验：{(IsValid ? "通过" : $"发现 {Errors.Count} 个问题")}").Append('\n');
            sb.Append($"最窄通道：{NarrowestPassageM:0.00} m（{NarrowestPassageLabel}），AGV 车宽 {AgvWidthM:0.00} m，单侧余量 {(NarrowestPassageM - AgvWidthM) * 0.5f:0.00} m").Append('\n');
            sb.Append($"巡游路线：全长 {RouteLengthM:0.0} m；AGV 外轮廓到机身最小 {RouteToFuselageM:0.00} m，到模型机包围盒最小 {RouteToAircraftBoundsM:0.00} m，到障碍物最小 {RouteToObstacleM:0.00} m").Append('\n');
            sb.Append($"停靠设计：AGV 底盘到机身 {DockingFuselageClearanceM:0.00} m（CFG_Agv）").Append('\n');
            sb.Append($"作业通道：模型机到障碍物最小 {AircraftToObstacleM:0.00} m，到作业区边界最小 {AircraftToWorkZoneEdgeM:0.00} m").Append('\n');
            sb.Append($"充电位到作业区：{ChargingDockToWorkZoneM:0.00} m；演示台前观众区进深：{DemoAudienceDepthM:0.00} m");
            foreach (string error in Errors) sb.Append("\n  × ").Append(error);
            return sb.ToString();
        }
    }
}
