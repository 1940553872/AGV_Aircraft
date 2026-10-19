using System;
using RivetSim.Core.Config;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 操作员初值通道（M6，t(1,8)）：执行器打开通道请求初值，演示界面在小地图上点选后经 <see cref="Submit"/> 写回；
    /// <see cref="AutoProvide"/> 为 true（自动演示）时，若操作员未在限定时间内点选，由模拟操作员给出。
    /// </summary>
    public sealed class OperatorPoseChannel
    {
        private PlanarPose _submitted;

        /// <summary>是否正在等待操作员输入。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>本次请求是否已收到操作员点选。</summary>
        public bool HasSubmission { get; private set; }

        /// <summary>未点选时是否允许模拟操作员在超时后给出（自动演示模式为 true）。</summary>
        public bool AutoProvide { get; set; } = true;

        /// <summary>模拟操作员给出前的等待时长，单位 s（打开通道时写入，供界面显示倒计时）。</summary>
        public float AutoDelayS { get; private set; }

        /// <summary>打开通道时的仿真时间，单位 s。</summary>
        public double OpenedAtS { get; private set; }

        /// <summary>通道打开（参数为模拟操作员等待时长 s）。</summary>
        public event Action<float> Opened;

        /// <summary>通道关闭（参数为 true 表示采用了操作员点选）。</summary>
        public event Action<bool> Closed;

        /// <summary>打开通道，等待操作员点选。</summary>
        public void Open(double nowS, float autoDelayS)
        {
            IsOpen = true;
            HasSubmission = false;
            AutoDelayS = autoDelayS;
            OpenedAtS = nowS;
            Opened?.Invoke(autoDelayS);
        }

        /// <summary>操作员点选的初值；通道未打开时忽略并返回 false。</summary>
        public bool Submit(PlanarPose pose)
        {
            if (!IsOpen || HasSubmission) return false;
            _submitted = pose;
            HasSubmission = true;
            return true;
        }

        /// <summary>是否应结束等待：已点选，或允许自动给出且已超时。</summary>
        public bool IsResolved(double nowS) => HasSubmission || (AutoProvide && nowS - OpenedAtS >= AutoDelayS);

        /// <summary>取出操作员点选的初值（未点选返回 false）。</summary>
        public bool TryTake(out PlanarPose pose)
        {
            pose = _submitted;
            return HasSubmission;
        }

        /// <summary>关闭通道。</summary>
        public void Close()
        {
            if (!IsOpen) return;
            bool used = HasSubmission;
            IsOpen = false;
            HasSubmission = false;
            Closed?.Invoke(used);
        }
    }
}
