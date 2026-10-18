using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// A320 外层预制体的实测外形（单位 m，坐标为预制体本地坐标：原点在主起落架中点地面投影，机头朝 +Z）。
    /// </summary>
    public struct AircraftMeasurement
    {
        /// <summary>机长（Z 向）。</summary>
        public float LengthM;
        /// <summary>翼展（X 向）。</summary>
        public float WingspanM;
        /// <summary>机高（地面到最高点）。</summary>
        public float HeightM;
        /// <summary>最低点离地高度（应接近 0；负值表示有部件低于机轮底部）。</summary>
        public float LowestPointM;
        /// <summary>机身直径（机身外壳 X 向宽度）。</summary>
        public float FuselageDiameterM;
        /// <summary>机身中线离地高度。</summary>
        public float FuselageCenterlineHeightM;
        /// <summary>机身下缘离地高度。</summary>
        public float FuselageBottomHeightM;
        /// <summary>包围盒中心相对原点的地面投影（x = X，y = Z）。</summary>
        public Vector2 BoundsCenterOffsetM;
        /// <summary>主起落架每侧机轮组中心到机身中线的横向距离。</summary>
        public float MainGearSideOffsetM;
        /// <summary>主机轮半径。</summary>
        public float MainWheelRadiusM;
        /// <summary>可见三角面总数。</summary>
        public long Triangles;

        /// <inheritdoc />
        public override string ToString() =>
            $"实测：机长 {LengthM:0.00} m、翼展 {WingspanM:0.00} m、机高 {HeightM:0.00} m、最低点 {LowestPointM:0.000} m、机身直径 {FuselageDiameterM:0.00} m、" +
            $"机身中线离地 {FuselageCenterlineHeightM:0.00} m、下缘离地 {FuselageBottomHeightM:0.00} m；" +
            $"包围盒中心相对主起落架 ({BoundsCenterOffsetM.x:0.00}, {BoundsCenterOffsetM.y:0.00}) m；" +
            $"主起落架半轮距 {MainGearSideOffsetM:0.00} m、主机轮半径 {MainWheelRadiusM:0.00} m；可见三角面 {Triangles}";
    }
}
