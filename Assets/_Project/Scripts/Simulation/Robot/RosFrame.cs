using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// ROS（右手系，x 前、y 左、z 上）与 Unity（左手系，x 右、y 上、z 前）的坐标换算。
    /// 位置：Unity = (−y, z, x)；旋转：M_Unity = C · R_ROS · Cᵀ（C 为上述换算矩阵，含一次镜像，结果仍是正常旋转）。
    /// 绕 ROS 轴 a 转 θ（右手定则）对应 Unity 中绕换算后的轴转 −θ（Unity 为左手定则）。
    /// </summary>
    public static class RosFrame
    {
        /// <summary>ROS 位置 → Unity 位置。</summary>
        public static Vector3 ToUnityPosition(Vector3 ros) => new Vector3(-ros.y, ros.z, ros.x);

        /// <summary>ROS 方向向量 → Unity 方向向量（与位置换算相同）。</summary>
        public static Vector3 ToUnityDirection(Vector3 ros) => ToUnityPosition(ros);

        /// <summary>URDF 的 rpy（固定轴 X→Y→Z，即 R = Rz(yaw)·Ry(pitch)·Rx(roll)，单位 rad）→ Unity 旋转。</summary>
        public static Quaternion ToUnityRotation(float rollRad, float pitchRad, float yawRad)
        {
            Matrix4x4 r = RotZ(yawRad) * RotY(pitchRad) * RotX(rollRad);
            Vector3 forward = ToUnityDirection(r.MultiplyVector(FromUnity(Vector3.forward)));
            Vector3 up = ToUnityDirection(r.MultiplyVector(FromUnity(Vector3.up)));
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>Unity 方向向量 → ROS 方向向量。</summary>
        public static Vector3 FromUnity(Vector3 unity) => new Vector3(unity.z, -unity.x, unity.y);

        private static Matrix4x4 RotX(float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            var m = Matrix4x4.identity;
            m.m11 = c; m.m12 = -s; m.m21 = s; m.m22 = c;
            return m;
        }

        private static Matrix4x4 RotY(float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            var m = Matrix4x4.identity;
            m.m00 = c; m.m02 = s; m.m20 = -s; m.m22 = c;
            return m;
        }

        private static Matrix4x4 RotZ(float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            var m = Matrix4x4.identity;
            m.m00 = c; m.m01 = -s; m.m10 = s; m.m11 = c;
            return m;
        }
    }
}
